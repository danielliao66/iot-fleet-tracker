using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Proto;
using IotTracker.Messages;

namespace IotTracker.Infrastructure;

public class InfluxDbMiddleware : IDisposable
{
    readonly InfluxDBClient _client;
    readonly WriteApiAsync _writeApi;
    readonly string _bucket;
    readonly string _org;

    public InfluxDbMiddleware(string url, string token, string org, string bucket)
    {
        _org = org;
        _bucket = bucket;

        // Explicitly configure the options to enforce token authentication
        var options = new InfluxDBClientOptions.Builder()
            .Url(url)
            .AuthenticateToken(token)
            .Org(org)
            .Bucket(bucket)
            .Build();
        
        // Initialize InfluxDB Client
        _client = new InfluxDBClient(options);
        _writeApi = _client.GetWriteApiAsync();
    }

    // The Proto.Actor receiver interceptor method
    public Receiver Intercept(Receiver next)
    {
        return async (context, envelope) =>
        {
            // Process the message in the actor first (updates memory)
            await next(context, envelope);

            // Post-process: If the message was Telemetry, asynchronously log it to InfluxDB
            if (envelope.Message is SendTelemetry telemetry)
            {
                var point = PointData
                    .Measurement("vehicle_telemetry")
                    .Tag("device_id", telemetry.DeviceId)
                    .Field("latitude", telemetry.Latitude)
                    .Field("longitude", telemetry.Longtitude)
                    .Field("speed", telemetry.Speed)
                    .Field("battery_level", telemetry.BatteryLevel)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

                // Fire-and-forget write to the database (non-blocking for the actor)
                _ = _writeApi.WritePointAsync(point, _bucket, _org);
            }
        };
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
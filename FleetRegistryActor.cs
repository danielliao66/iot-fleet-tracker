using Proto;
using IotTracker.Messages;
using IotTracker.Infrastructure;

namespace IotTracker.Actors;

public class FleetRegistryActor : IActor
{
    // Tracks active children: Key = DeviceId, Value = Actor Process ID (PID)
    readonly Dictionary<string, PID> _devices = new();
    readonly InfluxDbMiddleware _influxMiddleware;

    public FleetRegistryActor(InfluxDbMiddleware influxMiddleware)
    {
        _influxMiddleware = influxMiddleware;
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            SendTelemetry msg => HandleTelemetry(context, msg),
            GetDeviceStatus msg => HandleQuery(context, msg),
            _ => Task.CompletedTask
        };
    }

    Task HandleTelemetry(IContext context, SendTelemetry msg)
    {
        var devicePid = GetOrCreateDeviceActor(context, msg.DeviceId);
        
        // Forward the telemetry message down to the specific device actor
        context.Forward(devicePid);
        return Task.CompletedTask;
    }

    Task HandleQuery(IContext context, GetDeviceStatus msg)
    {
        if (_devices.TryGetValue(msg.DeviceId, out var devicePid))
        {
            context.Forward(devicePid);
        }
        else
        {
            // If the device doesn't exist, respond with empty/default state
            context.Respond(new DeviceStatusResponse(msg.DeviceId, 0, 0, 0, 0, DateTime.MinValue));
        }
        return Task.CompletedTask;
    }

    PID GetOrCreateDeviceActor(IContext context, string deviceId)
    {
        if (!_devices.TryGetValue(deviceId, out var devicePid))
        {   
            // Define how to create a DeviceActor child instance
            // Apply the InfluxDB interceptor middleware to the DeviceActor configuration
            var props = Props.FromProducer(() => new DeviceActor())
                             .WithReceiverMiddleware(_influxMiddleware.Intercept);
            
            // Spawn the child under the registry's supervision hierarchy
            devicePid = context.Spawn(props);
            _devices[deviceId] = devicePid;
        }

        return devicePid;
    }
}
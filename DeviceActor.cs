using Proto;
using IotTracker.Messages;

namespace IotTracker.Actors;

public class DeviceActor : IActor
{
    // The internal state of the vehicle
    string _deviceId = string.Empty;
    double _lattitude;
    double _longtitude;
    double _speed;
    double _batteryLevel;
    DateTime _lastUpdated;

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            SendTelemetry msg => HandleTelemetry(msg),
            GetDeviceStatus => HandleQuery(context),
            _ => Task.CompletedTask
        };
    }

    async Task HandleTelemetry(SendTelemetry msg)
    {
        _deviceId = msg.DeviceId;
        _lattitude = msg.Lattitude;
        _longtitude = msg.Longtitude;
        _speed = msg.Speed;
        _batteryLevel = msg.BatteryLevel;
        _lastUpdated = DateTime.UtcNow;
    }

    Task HandleQuery(IContext context)
    {
        var response = new DeviceStatusResponse(
            _deviceId, 
            _lattitude, 
            _longtitude, 
            _speed, 
            _batteryLevel, 
            _lastUpdated
        );
        
        // Respond back to the sender
        context.Respond(response);
        return Task.CompletedTask;
    }
}
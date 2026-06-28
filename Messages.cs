namespace IotTracker.Messages;

// Telemetry data from physical devices
public record SendTelemetry(
    string DeviceId,
    double Lattitude,
    double Longtitude, 
    double Speed,
    double BatteryLevel
);

// Request for specific vehicle status
public record GetDeviceStatus(string DeviceId);

// Response containing the in-memory state
public record DeviceStatusResponse(
    string DeviceId, 
    double Lattitude, 
    double Longtitude, 
    double Speed, 
    double BatteryLevel, 
    DateTime LastUpdated
);

public record LiveMapMarker(
    string DeviceId,
    double Lattitude,
    double Longtitude,
    double Speed,
    double Battery
);
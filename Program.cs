using Proto;
using IotTracker.Actors;
using IotTracker.Messages;
using IotTracker.Infrastructure;
using Microsoft.Extensions.Configuration;

// Build configuration pipeline (Reads JSON + Environment Variables)
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables() // Automatically overrides JSON keys matching patterns
    .Build();

// Safely retrieve the token
string url = config["InfluxDb:Url"] ?? "http://localhost:8086";
string org = config["InfluxDb:Org"] ?? "fleet-org";
string bucket = config["InfluxDb:Bucket"] ?? "fleet-telemetry";

// Look for a distinct environment variable first, then fallback to JSON definition
string token = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN") 
               ?? config["InfluxDb:Token"] 
               ?? throw new InvalidOperationException("InfluxDB Secret Token is missing!");

// Initialize InfluxDB Middleware
using var influxDb = new InfluxDbMiddleware(url: url, token: token, org: org, bucket: bucket);

var system = new ActorSystem();
var context = system.Root;

// Pass the middleware via the factory producer
var registryProps = Props.FromProducer(() => new FleetRegistryActor(influxDb));

var registryPid = context.Spawn(registryProps);

/*
Console.WriteLine("Actor system started. Simulating data...");

// Test telemetry dispatch
context.Send(registryPid, new SendTelemetry("robot-01", 40.7128, -74.0060, 55.5, 92.0));

// Wait a moment for processing
await Task.Delay(500);

// 4. Query the status of "robot-01"
var status = await context.RequestAsync<DeviceStatusResponse>(
    registryPid, 
    new GetDeviceStatus("robot-01"), 
    TimeSpan.FromSeconds(2)
);

Console.WriteLine($"\n[Result Received]");
Console.WriteLine($"Device: {status.DeviceId}");
Console.WriteLine($"Location: {status.Latitude}, {status.Longtitude}");
Console.WriteLine($"Speed: {status.Speed} mph");
Console.WriteLine($"Updated At: {status.LastUpdated}");
*/
Console.WriteLine("=================================================");
Console.WriteLine("  Proto.Actor IoT Fleet Simulator Cluster Active");
Console.WriteLine("=================================================");
Console.WriteLine("-> Streaming live telemetry to InfluxDB...");
Console.WriteLine("-> Press [CTRL + C] anytime to shut down the sim.\n");

// Configure and Spawning the Dynamic Sim Pipeline
int vehicleCount = 100; 
var cancellationTokenSource = new CancellationTokenSource();

// Spawn parallel threads managing independent loops for every single vehicle tracking node
var simulationTasks = Enumerable.Range(1, vehicleCount).Select(i => 
{
    string deviceId = $"robot-{i:D3}";
    return Task.Run(() => RunSingleVehicleSimulationAsync(context!, registryPid, deviceId, cancellationTokenSource.Token));
}).ToArray();

// Keep main thread alive monitoring console metrics
var monitorTask = Task.Run(async () =>
{
    while (!cancellationTokenSource.Token.IsCancellationRequested)
    {
        await Task.Delay(5000);
        
        // Randomly audit a worker live via our actor message response workflow
        string randomTarget = $"robot-{Random.Shared.Next(1, vehicleCount + 1):D3}";
        try
        {
            var status = await context!.RequestAsync<DeviceStatusResponse>(
                registryPid, 
                new GetDeviceStatus(randomTarget), 
                TimeSpan.FromSeconds(1)
            );
            
            Console.WriteLine($"[AUDIT LOG] {status.DeviceId} | Latitude: {status.Latitude:F4}, Longtitude: {status.Longtitude:F4} | Speed: {status.Speed:F1} mph | Battery: {status.BatteryLevel:F1}%");
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[WARN] Audit timed out querying {randomTarget}. Cluster under load.");
        }
    }
});

// Wait for termination command
Console.CancelKeyPress += (sender, eventArgs) =>
{
    Console.WriteLine("\nShutting down simulation pipeline...");
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

await Task.WhenAll(simulationTasks.Append(monitorTask));
Console.WriteLine("Simulation ended safely.");

// --- Simulation Worker Blueprint Execution ---
static async Task RunSingleVehicleSimulationAsync(IRootContext context, PID registryPid, string deviceId, CancellationToken ct)
{
    // Start everyone with random baselines
    double latitude = 40.7128 + (Random.Shared.NextDouble() - 0.5) * 0.2;
    double longtitude = -74.0060 + (Random.Shared.NextDouble() - 0.5) * 0.2;
    double battery = Random.Shared.Next(70, 100);
    
    // Stagger boots slightly so database connections do not spike on exact identical clock ticks
    await Task.Delay(Random.Shared.Next(0, 2000), ct);

    while (!ct.IsCancellationRequested)
    {
        // Mutate real-world attributes
        double speed = Random.Shared.Next(45, 75) + Random.Shared.NextDouble();
        latitude += (Random.Shared.NextDouble() - 0.48) * 0.002; // drifting north-east
        longtitude += (Random.Shared.NextDouble() - 0.48) * 0.002;
        battery -= 0.05 * (speed / 60.0);

        if (battery <= 5) battery = 100; // auto-recharge simulation rule

        // Fire & Forget telemetry straight to the cluster tracking router
        context.Send(registryPid, new SendTelemetry(deviceId, latitude, longtitude, speed, battery));

        // Sleep for around 2 seconds
        await Task.Delay(TimeSpan.FromMilliseconds(1800 + Random.Shared.Next(0, 400)), ct);
    }
}
private const int ProcessingTimeoutSeconds = 10; // Longer timeout due to simulated delay

// Wait for connections to be ready
await Task.Delay(1000);

await Task.Delay(100); // Wait for subscription to be registered

await Task.Delay(100); // Wait for subscription to be registered

await Task.Delay(100); // Wait for subscription to be registered

// Verify metrics
var processingTime = endTime - startTime;
Assert.True(processingTime.TotalMilliseconds >= 2000); // At least simulated delay 
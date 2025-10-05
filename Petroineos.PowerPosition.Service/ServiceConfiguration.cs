public class ServiceConfiguration
{
    // Existing properties...
    public string OutputDirectory { get; set; } = @"C:\PowerPositionReports";
    public int IntervalMinutes { get; set; } = 5;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
    public bool EnableDetailedLogging { get; set; } = false;
    public int FileRetentionDays { get; set; } = 30;
    public bool EnableFileCleanup { get; set; } = true;
    public int CleanupIntervalHours { get; set; } = 24;

    // New production settings
    public bool EnableHealthMonitoring { get; set; } = true;
    public int HealthCheckIntervalMinutes { get; set; } = 5;
    public long LowDiskSpaceThresholdMB { get; set; } = 100;
    public string ServiceAccount { get; set; } = "LocalSystem";

    public void ValidateAndSetDefaults()
    {
        // Existing validation...
        if (string.IsNullOrEmpty(OutputDirectory))
            OutputDirectory = @"C:\PowerPositionReports";

        if (IntervalMinutes <= 0)
            IntervalMinutes = 5;

        // New validation
        if (LowDiskSpaceThresholdMB < 10)
            LowDiskSpaceThresholdMB = 100;

        if (HealthCheckIntervalMinutes < 1)
            HealthCheckIntervalMinutes = 5;
    }
}
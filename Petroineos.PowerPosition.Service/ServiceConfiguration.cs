using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Petroineos.PowerPosition.Service
{
    public class ServiceConfiguration
    {
        public string OutputDirectory { get; set; } = @"C:\PowerPositionReports";
        public int IntervalMinutes { get; set; } = 5;
        public int RetryCount { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 1000;
        public bool EnableDetailedLogging { get; set; } = false;

        // File retention settings
        public int FileRetentionDays { get; set; } = 30;
        public bool EnableFileCleanup { get; set; } = true;
        public int CleanupIntervalHours { get; set; } = 24; // Run cleanup once per day

        // Validation method to ensure reasonable values
        public void ValidateAndSetDefaults()
        {
            if (string.IsNullOrEmpty(OutputDirectory))
            {
                OutputDirectory = @"C:\PowerPositionReports";
            }

            if (IntervalMinutes <= 0)
            {
                IntervalMinutes = 5;
            }

            if (RetryCount <= 0)
            {
                RetryCount = 3;
            }

            if (RetryDelayMilliseconds <= 0)
            {
                RetryDelayMilliseconds = 1000;
            }

            // File retention validation
            if (FileRetentionDays < 1)
            {
                FileRetentionDays = 30; // Minimum 1 day retention
            }
            else if (FileRetentionDays > 365)
            {
                FileRetentionDays = 365; // Maximum 1 year retention
            }

            if (CleanupIntervalHours < 1)
            {
                CleanupIntervalHours = 24; // Minimum cleanup every hour
            }
            else if (CleanupIntervalHours > 24 * 7)
            {
                CleanupIntervalHours = 24 * 7; // Maximum cleanup once per week
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Petroineos.PowerPosition.Service;

namespace Petroineos.PowerPosition.Service
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Create and configure the host builder
            var builder = Host.CreateApplicationBuilder(args);

            // Configure Windows Service (only when NOT running as console)
            if (!args.Contains("--console") && !Debugger.IsAttached)
            {
                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "Petroineos Power Position Service";
                });
            }

            // Configure App Configuration
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            // Configure Services
            builder.Services.Configure<ServiceConfiguration>(
                builder.Configuration.GetSection("ServiceConfiguration"));

            // Register ServiceConfiguration as singleton for direct injection
            builder.Services.AddSingleton(provider =>
            {
                var config = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfiguration>() ?? new ServiceConfiguration();

                // Validate and set defaults
                if (string.IsNullOrEmpty(config.OutputDirectory))
                {
                    config.OutputDirectory = @"C:\PowerPositionReports";
                }

                if (config.IntervalMinutes <= 0)
                {
                    config.IntervalMinutes = 5;
                }

                return config;
            });

            // Configure Logging
            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.AddEventLog(settings =>
            {
                settings.SourceName = "Petroineos Power Position Service";
                settings.LogName = "Application";
            });

            // Add console logging when running in console mode
            if (args.Contains("--console") || Debugger.IsAttached)
            {
                builder.Logging.AddConsole();
                builder.Logging.AddDebug();
            }

            // Build and run the host
            var host = builder.Build();

            // Run the host (works for both console and service)
            await host.RunAsync();
        }
    }
}
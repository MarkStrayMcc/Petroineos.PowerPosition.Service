using System.Diagnostics;
using System.Runtime.InteropServices;
using Petroineos.PowerPosition.Service.Health;
using Petroineos.PowerPosition.Service.Interfaces;
using Petroineos.PowerPosition.Service.Metrics;

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

            builder.Services.Configure<ServiceConfiguration>(
              builder.Configuration.GetSection("ServiceConfiguration"));

            // Register ServiceConfiguration as singleton for direct injection
            builder.Services.AddSingleton(provider =>
            {
                var config = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfiguration>() ?? new ServiceConfiguration();

                // Use the new validation method
                config.ValidateAndSetDefaults();

                return config;
            });

            // Register the actual PowerService from the provided DLL
            builder.Services.AddSingleton<Services.IPowerService, Services.PowerService>();

            // Register our services with interfaces
            builder.Services.AddTransient<IPowerPositionWorker, PowerPositionWorker>();
            builder.Services.AddSingleton<IHealthMonitor, ServiceHealthMonitor>();
            builder.Services.AddSingleton<IMetricsService, ServiceMetrics>();

            // Register the background service
            builder.Services.AddHostedService<PowerPositionBackgroundService>();

            // Configure Logging
            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));


            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if WINDOWS
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "Petroineos Power Position Service";
        settings.LogName = "Application";
    });
#else
                builder.Logging.AddEventLog();
#endif
            }

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
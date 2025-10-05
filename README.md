# Petroineos Power Position Service

## Overview

This Windows service generates intra-day power position reports for Petroineos traders.  
It retrieves trade data using the provided `PowerService.dll` and outputs aggregated hourly volumes into CSV files.

The service:
- Runs on a configurable interval.
- Aggregates trade volumes per local (London) hour.
- Produces timestamped CSV reports.
- Logs all operations and errors for support diagnostics.
- Retries transient failures using exponential backoff.

---

## Features

✅ Implemented as a Windows Worker Service (using .NET 8).  
✅ Reads configuration on startup from `appsettings.json`.  
✅ Generates a new report immediately at startup, and then every configured interval.  
✅ Uses the official `PowerService.dll` interface for fetching power trades.  
✅ Handles errors gracefully with retries and logging.  
✅ Supports London local time aggregation (starting from 23:00 of the previous day).  


# Prerequisites
- .NET 8.0 Runtime
- Windows Server 2016+ or Windows 10/11
- Administrator privileges for service installation

# Installation
Run powershell as an Administrator and navigate to C:\Users\{{YOURSELF}}\source\repos\Petroineos\Petroineos.PowerPosition.Service\Scripts or wherever you cloned the repo to, you need to be in the scripts drectory. Then run 

	.\deploy.ps1

and hit enter

Validate the installation by running the following command in the same powershell termjinal

	Get-Service "Petroineos Power Position Service"

You should see the following:

Status   Name               DisplayName
------   ----               -----------
Running  Petroineos Powe... Petroineos Power Position Service

# Validating the output
The files will be generated and written to the directory C:\PowerPositionReports in the following format:
- Normal: PowerPosition_YYYYMMDD_HHMM.csv
- Error: PowerPosition_YYYYMMDD_HHMM_ERROR.csv

- Normal
Local Time,Volume
23:00,150.5
00:00,200.0
01:00,175.3
...
22:00,180.1

- Error
# This report contains placeholder data due to service error
# ERROR: PowerServiceException: Error retrieving power volumes
# Extract Time: 2025-10-05 13:30:00
# Generated: 2025-10-05 13:32:15
Local Time,Volume
23:00,ERROR
00:00,ERROR
...
22:00,ERROR

# Uninstall
Run powershell as an Administrator and navigate to C:\Users\{{YOURSELF}}\source\repos\Petroineos\Petroineos.PowerPosition.Service\Scripts or wherever you cloned the repo to, you need to be in the scripts drectory. Then run 

	.\uninstall-service.ps1

and hit enter

Validate the service is removed by running the following command in the same powershell terminal

	Get-Service "Petroineos Power Position Service"

You should see the following:

Get-Service : Cannot find any service with service name 'Petroineos Power Position Service'.
At line:1 char:1
+ Get-Service "Petroineos Power Position Service"
+ ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : ObjectNotFound: (Petroineos Power Position Service:String) [Get-Service], ServiceCommandException
    + FullyQualifiedErrorId : NoServiceFoundForGivenName,Microsoft.PowerShell.Commands.GetServiceCommand
# Installation script for Logisnext project

# Store the current directory
$scriptPath = $MyInvocation.MyCommand.Path
$currentDir = (Get-Location).Path

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script requires administrator privileges for metrics collection." -ForegroundColor Yellow
    Write-Host "Restarting with elevated privileges..." -ForegroundColor Yellow
    
    # Restart script with admin rights and original path
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit", "-Command", "cd '$currentDir'; & '$scriptPath'"
    exit
}

Clear-Host
Write-Host @"
Welcome to Logisnext Project Installation!

NOTE: This script requires administrator privileges.
      If not running as administrator, it will request elevation.

This script will:
1. Verify .NET SDK and Docker installations
2. Build .NET projects
3. Start required Docker containers:
   - MQTT Broker (Mosquitto)
   - Prometheus (Metrics)
   - Grafana (Monitoring)
4. Start the application services:
   - Order Processing Service
   - Order Submission Service

Requirements:
- .NET 8 SDK
- Docker Desktop
- Administrator privileges

"@ -ForegroundColor Cyan

$continue = Read-Host "Do you want to proceed with the installation? (Y/N)"
if ($continue -ne "Y" -and $continue -ne "y") {
    Write-Host "Installation cancelled." -ForegroundColor Yellow
    exit 0
}

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8 SDK before continuing." -ForegroundColor Red
    exit 1
}

# Check Docker
try {
    $dockerVersion = docker --version
    Write-Host "Docker found: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Docker not found. Please install Docker before continuing." -ForegroundColor Red
    exit 1
}

# Build .NET projects
Write-Host "`nBuilding .NET projects..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore project dependencies." -ForegroundColor Red
    exit 1
}

dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Project build failed." -ForegroundColor Red
    exit 1
}

# Check project's docker-compose.yml
if (-not (Test-Path "docker-compose.yml")) {
    Write-Host "ERROR: docker-compose.yml file not found." -ForegroundColor Red
    exit 1
}

# Show project containers
Write-Host "`nProject containers:" -ForegroundColor Yellow
docker-compose config --services | ForEach-Object { Write-Host "- $_" }

# Check if project containers are already running
$runningContainers = docker-compose ps --services --filter "status=running"
if ($runningContainers) {
    Write-Host "`nNote: Project Docker containers are already running:" -ForegroundColor Yellow
    $runningContainers | ForEach-Object { Write-Host "- $_" }
    $restart = Read-Host "Do you want to restart project containers? (Y/N)"
    if ($restart -eq "Y" -or $restart -eq "y") {
        Write-Host "Stopping project containers..." -ForegroundColor Yellow
        docker-compose down
        Write-Host "Starting project containers..." -ForegroundColor Yellow
        docker-compose up -d
    } else {
        Write-Host "Continuing with existing containers." -ForegroundColor Green
    }
} else {
    Write-Host "`nStarting project containers..." -ForegroundColor Yellow
    docker-compose up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to start Docker containers." -ForegroundColor Red
        exit 1
    }
}

# Verify containers are running
Start-Sleep -Seconds 5
$containers = docker-compose ps --format json
if ($LASTEXITCODE -ne 0 -or -not $containers) {
    Write-Host "ERROR: Failed to verify container status." -ForegroundColor Red
    exit 1
}

Write-Host "`nProject services are running:" -ForegroundColor Green
Write-Host "- Prometheus: http://localhost:9090"
Write-Host "- Grafana: http://localhost:3000"
Write-Host "- MQTT Broker: localhost:1883"

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "`nStarting service terminals..." -ForegroundColor Yellow

# Create commands for both services
$processingCommand = "cd $((Get-Location).Path)\src\OrderProcessingService; Write-Host 'Starting Order Processing Service...' -ForegroundColor Cyan; dotnet run; Read-Host 'Press Enter to exit'"
$submissionCommand = "cd $((Get-Location).Path)\src\OrderSubmissionService; Write-Host 'Starting Order Submission Service...' -ForegroundColor Cyan; dotnet run; Read-Host 'Press Enter to exit'"

# Ask if user wants to start the services
$startServices = Read-Host @"
`nExample workflow. once the system is started:
1. Both services will start in separate administrator terminal windows
2. Wait for services to fully initialize:
   - Order Processing Service should show "Connected to MQTT broker"
   - Order Submission Service should be ready for commands

3. Submit a order in the Order Submission Service terminal:
   ```
   dotnet run order "Test Customer" "Product 123"
   ```

Please note that this window will give more info after terminals have been opened.

Do you want to open terminals for both services now? (Y/N)

"@ 
if ($startServices -eq "Y" -or $startServices -eq "y") {
    Write-Host "`nStarting Order Processing Service..." -ForegroundColor Cyan
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit", "-Command", $processingCommand
    
    Write-Host "Waiting for Order Processing Service to initialize..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    Write-Host "Starting Order Submission Service..." -ForegroundColor Cyan
    Start-Process powershell -Verb RunAs -ArgumentList "-NoExit", "-Command", $submissionCommand
    
    Write-Host "`nService terminals have been opened!" -ForegroundColor Green
    Write-Host "Please wait a moment for both services to fully initialize." -ForegroundColor Yellow
}

Write-Host @"
`nSystem is ready!

Monitor the process through:
- Console output of both services
- Metrics: http://localhost:9090 (Prometheus)
- Dashboards: http://localhost:3000 (Grafana)

Manual start commands (if needed):
Note: These commands require administrator privileges

1. Order Processing Service:
   cd $((Get-Location).Path)\src\OrderProcessingService
   dotnet run

2. Order Submission Service:
   cd $((Get-Location).Path)\src\OrderSubmissionService
   dotnet run

"@ 
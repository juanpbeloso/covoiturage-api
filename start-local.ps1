$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

docker compose up -d postgres | Out-Null

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://0.0.0.0:5178"

Write-Host "API en http://localhost:5178"
dotnet run --launch-profile http

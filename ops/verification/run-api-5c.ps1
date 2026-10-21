# Runs the ScaleDesk API on the HOST against the compose data-plane (postgres on :5433 via override,
# kafka :29092, redis :6379, jaeger :4317). Runtime connects as the least-privilege scaledesk_app role
# so PostgreSQL RLS is genuinely exercised; a superuser migrator applies migrations. Kept out of the
# compose file intentionally: host-run lets the failure scripts stop/start Kafka & Redis per scenario.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll  = Join-Path $root 'src\ScaleDesk.Api\bin\Debug\net9.0\ScaleDesk.Api.dll'

$env:ASPNETCORE_ENVIRONMENT              = 'Development'
$env:ASPNETCORE_URLS                     = 'http://localhost:5102'
$env:ConnectionStrings__Postgres         = 'Host=localhost;Port=5433;Database=scaledesk;Username=scaledesk_app;Password=scaledesk_app_pw'
$env:ConnectionStrings__PostgresMigrator = 'Host=localhost;Port=5433;Database=scaledesk;Username=scaledesk;Password=scaledesk'
$env:ConnectionStrings__Redis            = 'localhost:6379'
$env:Jwt__SecretKey                      = 'change-me-in-prod-this-is-a-dev-only-32byte-secret!!'
$env:Database__AutoMigrate               = 'true'
$env:Kafka__Enabled                      = 'true'
$env:Kafka__BootstrapServers             = 'localhost:29092'
$env:Kafka__ConsumerGroup                = 'scaledesk-notifications'
$env:Sla__Enabled                        = 'true'
$env:Sla__IntervalSeconds                = '10'
$env:Redis__Enabled                      = 'true'
$env:Redis__ConnectionString             = 'localhost:6379'
$env:Otel__Enabled                       = 'true'
$env:Otel__Endpoint                      = 'http://localhost:4317'

Write-Host "launching API from $dll"
& dotnet exec $dll

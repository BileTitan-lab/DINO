$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "desktop\DinoDash.Desktop.csproj"
$releaseFolder = Join-Path $projectRoot "release"
$env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet"
$env:NUGET_PACKAGES = Join-Path $projectRoot ".nuget"
$env:APPDATA = Join-Path $projectRoot ".appdata"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

New-Item -ItemType Directory -Path (Join-Path $env:APPDATA "NuGet") -Force | Out-Null

dotnet restore $projectFile `
  --configfile (Join-Path $projectRoot "NuGet.Config") `
  --runtime win-x64 `
  -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Runtime restore failed. No new EXE was created." }

dotnet publish $projectFile `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --no-restore `
  --output $releaseFolder
if ($LASTEXITCODE -ne 0) { throw "Build or publish failed. No new EXE was created." }

$publishedExe = Get-ChildItem -LiteralPath $releaseFolder -Filter '*.exe' -File |
  Select-Object -First 1
if ($null -eq $publishedExe) { throw "Published EXE was not found." }
Copy-Item -LiteralPath $publishedExe.FullName -Destination (Join-Path $projectRoot $publishedExe.Name) -Force

Write-Host ""
Write-Host "Build complete. EXE updated in $projectRoot and $releaseFolder"

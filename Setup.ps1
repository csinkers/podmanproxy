# This script is used to setup the PodmanProxy service on Windows
$exePath    = Join-Path $PSScriptRoot "PodmanProxy.exe"
$configPath = Join-Path $PSScriptRoot "podmanproxy.json"

Copy-Item $publishPath $exePath

# Error if exePath doesn't exist
if (-not (Test-Path $exePath))    { Write-Error "PodmanProxy.exe not found at $exePath" exit 1 }
if (-not (Test-Path $configPath)) { Write-Error "podmanproxy.json not found at $configPath" exit 1 }

if (Get-Service   PodmanProxy -ErrorAction SilentlyContinue) {
    Stop-Service  PodmanProxy
    sc.exe delete PodmanProxy
}

New-Service                   `
  -Name "PodmanProxy"         `
  -DisplayName "Podman Proxy" `
  -BinaryPathName "$exePath"  `
  -Description "A proxy service to provide access to UDP and TCP ports on podman containers from a particular host interface" `
  -StartupType Automatic > $null

Start-Service PodmanProxy
Get-Service   PodmanProxy

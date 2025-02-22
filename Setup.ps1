param([Parameter(mandatory=$true)][String] $configPath)

$absConfigPath = Resolve-Path -Path $configPath

if (Get-Service PodmanProxy -ErrorAction SilentlyContinue) {
    Stop-Service PodmanProxy
    sc.exe delete PodmanProxy
}

dotnet publish

$exePath = Join-Path $PSScriptRoot "build\PodmanProxy\bin\Release\net8.0\win-x64\publish\PodmanProxy.exe"
New-Service `
  -Name "PodmanProxy" `
  -DisplayName "Podman Proxy" `
  -BinaryPathName "$exePath --ConfigPath=`"$absConfigPath`"" `
  -Description "A proxy service to provide access to UDP and TCP ports on podman containers from a particular host interface" `
  -StartupType Automatic > $null

Start-Service PodmanProxy
Get-Service PodmanProxy

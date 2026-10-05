param(
  [Parameter(Mandatory=$true)]
  [string]$ExtensionId
)
$ErrorActionPreference='Stop'
if($ExtensionId -cnotmatch '^[a-p]{32}$') {
  throw 'Chrome extension ID must contain exactly 32 lowercase letters a-p. Copy it from chrome://extensions.'
}
$setupDir=Split-Path -Parent $MyInvocation.MyCommand.Path
$root=Split-Path -Parent $setupDir
$hostExe=Join-Path $root 'Runtime\UvdNativeHost.exe'
$uvdExe=Join-Path $root 'UniversalVideoDownloader.exe'
if(!(Test-Path $hostExe) -or !(Test-Path $uvdExe)) {
  throw 'Keep the UVD package folder structure intact. Runtime\UvdNativeHost.exe or UniversalVideoDownloader.exe is missing.'
}
$manifestPath=Join-Path $setupDir 'com.uvd.bridge.json'
$manifest=[ordered]@{
  name='com.uvd.bridge'
  description='UVD Source Capture native bridge'
  path=$hostExe
  type='stdio'
  allowed_origins=@("chrome-extension://$ExtensionId/")
}
[IO.File]::WriteAllText($manifestPath,
  ($manifest|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$registry='HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.uvd.bridge'
New-Item -Path $registry -Force | Out-Null
Set-Item -Path $registry -Value $manifestPath
Write-Host 'UVD Native Messaging registered.'
Write-Host 'Extension ID:' $ExtensionId
Write-Host 'Host:' $hostExe
Write-Host 'Manifest:' $manifestPath

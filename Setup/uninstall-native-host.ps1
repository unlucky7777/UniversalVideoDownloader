$ErrorActionPreference='Stop'

$setupDir=Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath=Join-Path $setupDir 'com.uvd.bridge.json'
$registry='HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.uvd.bridge'

if(Test-Path $registry){
  Remove-Item -LiteralPath $registry -Recurse -Force
}
if(Test-Path $manifestPath){
  Remove-Item -LiteralPath $manifestPath -Force
}

Write-Host 'UVD Native Messaging registration removed for the current user.'

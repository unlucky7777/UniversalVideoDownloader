param(
  [string]$Configuration='Release',
  [string]$RuntimeIdentifier='win-x64',
  [string]$OutputDirectory=''
)

$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if([string]::IsNullOrWhiteSpace($OutputDirectory)){
  $OutputDirectory=Join-Path $repo 'artifacts'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

[xml]$props=Get-Content (Join-Path $repo 'Directory.Build.props')
$version=[string]$props.Project.PropertyGroup.AppVersion
if([string]::IsNullOrWhiteSpace($version)){throw 'AppVersion not found in Directory.Build.props'}

$manifest=Get-Content (Join-Path $repo 'runtime-manifest.json') -Raw | ConvertFrom-Json
$runtimeNames=@('N_m3u8DL-RE.exe','yt-dlp.exe','ffmpeg.exe')
foreach($name in $runtimeNames){
  $entry=$manifest.components | Where-Object {$_.name -eq $name} | Select-Object -First 1
  if(!$entry){throw "runtime-manifest.json is missing $name"}
  $path=Join-Path $repo $name
  if(!(Test-Path $path)){throw "Missing $name. Obtain the pinned runtime artifact described in runtime-manifest.json."}
  $actual=(Get-FileHash $path -Algorithm SHA256).Hash
  if($actual -ne $entry.sha256){throw "$name SHA256 mismatch. Expected $($entry.sha256), got $actual"}
}

$dotnet=(Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source
$hasSdk=$false
if($dotnet){
  $sdkList=@(& $dotnet --list-sdks 2>$null)
  $hasSdk=$sdkList.Count -gt 0
}
if(!$hasSdk){
  $localDotnet=Join-Path (Split-Path $repo -Parent) '.tools\dotnet\dotnet.exe'
  if(Test-Path $localDotnet){
    $dotnet=$localDotnet
    $hasSdk=@(& $dotnet --list-sdks 2>$null).Count -gt 0
  }
}
if(!$dotnet -or !$hasSdk){throw 'A .NET SDK is required to build a release.'}

$temp=Join-Path ([IO.Path]::GetTempPath()) ('uvd-release-'+[guid]::NewGuid().ToString('N'))
$appOut=Join-Path $temp 'app'
$hostOut=Join-Path $temp 'host'
$stage=Join-Path $temp 'package'

try{
  New-Item -ItemType Directory -Force -Path $appOut,$hostOut,$stage | Out-Null

  Push-Location $repo
  try{
    & $dotnet publish 'N_m3u8DL_RE_GUI\N_m3u8DL_RE_GUI.csproj' -c $Configuration -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $appOut
    if($LASTEXITCODE -ne 0){throw 'GUI publish failed'}

    & $dotnet publish 'UvdNativeHost\UvdNativeHost.csproj' -c $Configuration -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $hostOut
    if($LASTEXITCODE -ne 0){throw 'Native Host publish failed'}
  }
  finally{
    Pop-Location
  }

  $runtimeDir=Join-Path $stage 'Runtime'
  $extDir=Join-Path $stage 'Extension\UVD-Source-Capture'
  $setupDir=Join-Path $stage 'Setup'
  $licensesDir=Join-Path $stage 'Licenses'
  New-Item -ItemType Directory -Force -Path $runtimeDir,$extDir,$setupDir,$licensesDir | Out-Null

  Copy-Item (Join-Path $appOut 'UniversalVideoDownloader.exe') $stage
  Copy-Item (Join-Path $hostOut 'UvdNativeHost.exe') $runtimeDir

  foreach($name in $runtimeNames){
    Copy-Item (Join-Path $repo $name) $runtimeDir
  }

  foreach($name in @('curl-hls.ps1','curl-mp4.ps1','eporner-downloader.cjs')){
    Copy-Item (Join-Path $repo ('N_m3u8DL_RE_GUI\Services\'+$name)) $runtimeDir
  }
  Copy-Item (Join-Path $repo 'N_m3u8DL_RE_GUI\range-mp4.cjs') $runtimeDir

  Get-ChildItem (Join-Path $repo 'UVD-Source-Capture') -File | Where-Object {
    $_.Name -notin @('package-lock.json') -and $_.Extension -ne '.map'
  } | ForEach-Object {
    Copy-Item $_.FullName $extDir
  }

  Copy-Item (Join-Path $repo 'Setup\install-native-host.ps1') $setupDir
  Copy-Item (Join-Path $repo 'Setup\uninstall-native-host.ps1') $setupDir
  Copy-Item (Join-Path $repo 'LICENSE') $stage
  Copy-Item (Join-Path $repo 'THIRD_PARTY_NOTICES.md') $stage
  Copy-Item (Join-Path $repo 'runtime-manifest.json') $stage
  Copy-Item (Join-Path $repo 'README.md') $stage
  if(Test-Path (Join-Path $repo 'SUPPORTED-SITES.txt')){
    Copy-Item (Join-Path $repo 'SUPPORTED-SITES.txt') $stage
  }
  Copy-Item (Join-Path $repo 'THIRD_PARTY_LICENSES\*') $licensesDir

  $forbidden=@(
    'com.uvd.bridge.json','config.json','config.txt','.env','cookies.txt',
    'credentials.json','secrets.json'
  )
  $bad=@(Get-ChildItem $stage -Recurse -File | Where-Object {
    $forbidden -contains $_.Name -or
    $_.FullName -match '\\(Downloads|Logs|NativeInbox|\.webview2-profile|\.nre-tmp)\\'
  })
  if($bad.Count){throw ('Forbidden release content: '+($bad.FullName -join '; '))}

  $zip=Join-Path $OutputDirectory ("UVD-v$version-$RuntimeIdentifier.zip")
  if(Test-Path $zip){Remove-Item $zip -Force}
  Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal

  $sha=(Get-FileHash $zip -Algorithm SHA256).Hash
  Set-Content -LiteralPath ($zip+'.sha256') -Value ($sha+'  '+[IO.Path]::GetFileName($zip)) -Encoding ascii
  Write-Host "RELEASE_ZIP=$zip"
  Write-Host "SHA256=$sha"
}
finally{
  if(Test-Path $temp){
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
  }
}

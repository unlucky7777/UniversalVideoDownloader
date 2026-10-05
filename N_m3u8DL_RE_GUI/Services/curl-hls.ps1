param(
  [Parameter(Mandatory=$true)]
  [string]$Job
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2

function Safe-Name([string]$value) {
  if([string]::IsNullOrWhiteSpace($value)){ return '' }
  foreach($c in [IO.Path]::GetInvalidFileNameChars()){ $value=$value.Replace([string]$c,'_') }
  return $value.Trim().TrimEnd('.')
}
function Resolve-Http([string]$base,[string]$relative) {
  $u=[Uri]::new([Uri]$base,$relative)
  if($u.Scheme -notin @('http','https')){ throw 'Playlist referenced a non-HTTP resource.' }
  return $u.AbsoluteUri
}
function Curl-Args($headers) {
  $a=[System.Collections.Generic.List[string]]::new()
  foreach($h in @($headers)){
    if($null -eq $h){continue}
    $name=[string]$h.name;$value=[string]$h.value
    if($name -notin @('Referer','Origin','User-Agent','Accept','Accept-Language')){continue}
    if([string]::IsNullOrWhiteSpace($value) -or $value.Length -gt 2048 -or
       $value.IndexOfAny([char[]]([char]13,[char]10,[char]0)) -ge 0){continue}
    $a.Add('-H');$a.Add($name+': '+$value)
  }
  return $a
}
function Fetch-Text([string]$url,[string]$path,$headers) {
  $curlArgs=[System.Collections.Generic.List[string]]::new()
  @('-sS','--fail','--location','--connect-timeout','15','--max-time','60','--max-filesize','2097152','--output',$path) | ForEach-Object {$curlArgs.Add($_)}
  foreach($x in (Curl-Args $headers)){$curlArgs.Add($x)}
  $curlArgs.Add($url)
  & curl.exe @curlArgs
  if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $path)){ throw "curl manifest fetch failed ($LASTEXITCODE)." }
  return [IO.File]::ReadAllText($path)
}
function Parse-Master([string]$text) {
  $lines=$text -split "\r?\n"
  $rows=@()
  for($i=0;$i -lt $lines.Count;$i++){
    $line=$lines[$i].Trim()
    if(!$line.StartsWith('#EXT-X-STREAM-INF:')){continue}
    $attrs=$line.Substring(18)
    $w=0;$h=0;$bw=0;$fps=0.0
    if($attrs -match '(?:^|,)RESOLUTION=(\d+)x(\d+)'){ $w=[int]$matches[1];$h=[int]$matches[2] }
    if($attrs -match '(?:^|,)(?:AVERAGE-)?BANDWIDTH=(\d+)'){ $bw=[long]$matches[1] }
    if($attrs -match '(?:^|,)FRAME-RATE=([0-9.]+)'){ $fps=[double]$matches[1] }
    $uri=$null
    for($j=$i+1;$j -lt $lines.Count;$j++){
      $next=$lines[$j].Trim()
      if(!$next){continue}
      if($next.StartsWith('#')){break}
      $uri=$next;break
    }
    if($uri){$rows += [pscustomobject]@{width=$w;height=$h;bandwidth=$bw;fps=$fps;uri=$uri}}
  }
  return @($rows)
}
function Select-Variant($rows,[string]$quality) {
  if(!$rows -or $rows.Count -eq 0){return $null}
  $ordered=@($rows|Sort-Object @{Expression='height';Descending=$true},@{Expression='bandwidth';Descending=$true})
  if($quality -eq 'best'){return $ordered[0]}
  $cap=0
  if(![int]::TryParse($quality,[ref]$cap)){return $ordered[0]}
  $fit=@($ordered|Where-Object{$_.height -gt 0 -and $_.height -le $cap})
  if($fit.Count){return $fit[0]}
  return @($ordered|Sort-Object @{Expression='height';Descending=$false},@{Expression='bandwidth';Descending=$false})[0]
}
function Config-Escape([string]$v){ return $v.Replace('\','\\').Replace('"','\"') }
function Unique-Output([string]$path){
  if(!(Test-Path -LiteralPath $path)){return $path}
  $dir=[IO.Path]::GetDirectoryName($path);$stem=[IO.Path]::GetFileNameWithoutExtension($path);$ext=[IO.Path]::GetExtension($path)
  for($i=1;$i -lt 10000;$i++){ $p=[IO.Path]::Combine($dir,($stem+' ('+$i+')'+$ext)); if(!(Test-Path -LiteralPath $p)){return $p} }
  throw 'Could not choose an unused output filename.'
}


function Check-Ts([string]$path){
 if(!(Test-Path -LiteralPath $path)){return $false}
 $f=[IO.File]::OpenRead($path)
 try{
  if($f.Length -lt 376 -or $f.Length%188 -ne 0){return $false}
  $a=$f.ReadByte();$f.Position=188;$b=$f.ReadByte()
  return $a -eq 0x47 -and $b -eq 0x47
 }finally{$f.Dispose()}
}
function Save-Checkpoint([string]$path,$map){
 $tmp=$path+'.new'
 $values=@{};foreach($entry in $map.GetEnumerator()){$values[$entry.Key]=[long]$entry.Value}
 [IO.File]::WriteAllText($tmp,($values|ConvertTo-Json -Compress -Depth 3),[Text.UTF8Encoding]::new($false))
 Move-Item -LiteralPath $tmp -Destination $path -Force
}
function Verify-LegacyFiles($rows,[string]$dir,$headerLines,$confirmed){
 $pending=[System.Collections.Generic.List[object]]::new()
 foreach($row in $rows){
  if($confirmed.ContainsKey($row.file)){continue}
  $target=Join-Path $dir $row.file
  $previous=if($row.isTs){Join-Path $dir ([IO.Path]::ChangeExtension($row.file,'.jpeg'))}else{$target}
  if(!(Test-Path -LiteralPath $previous)){continue}
  $file=Get-Item -LiteralPath $previous
  if($file.Length -le 0 -or ($row.isTs -and !(Check-Ts $previous))){continue}
  $pending.Add([pscustomobject]@{row=$row;path=$previous;length=$file.Length})
 }
 if($pending.Count -eq 0){return}
 Write-Output ('[12%] Validating '+$pending.Count+' prior segments before reuse.')
 for($start=0;$start -lt $pending.Count;$start+=48){
  $slice=@($pending|Select-Object -Skip $start -First 48)
  $cfg=Join-Path $dir 'verify-head.cfg'
  $lines=[System.Collections.Generic.List[string]]::new()
  foreach($opt in @('silent','location','head','connect-timeout = 10','max-time = 25',
   'parallel','parallel-immediate','parallel-max = 16',
   'write-out = "%{urlnum}|%{http_code}|%header{content-length}\n"')){$lines.Add($opt)}
  foreach($h in $headerLines){$lines.Add($h)}
  foreach($entry in $slice){
   $lines.Add('url = "'+(Config-Escape ([string]$entry.row.url))+'"')
   $lines.Add('output = "NUL"')
  }
  [IO.File]::WriteAllLines($cfg,$lines,[Text.UTF8Encoding]::new($false))
  $result=@(& curl.exe --config $cfg 2>$null)
  foreach($line in $result){
   if($line -notmatch '^(\d+)\|200\|(\d+)$'){continue}
   $index=[int]$matches[1];$length=[long]$matches[2]
   if($index -ge $slice.Count){continue}
   $entry=$slice[$index]
   if($length -le 0 -or $length -ne $entry.length){continue}
   $dest=Join-Path $dir $entry.row.file
   if($entry.path -ne $dest){
    if(Test-Path -LiteralPath $dest){Remove-Item -LiteralPath $dest -Force}
    Move-Item -LiteralPath $entry.path -Destination $dest
   }
   $confirmed[$entry.row.file]=$length
  }
  Write-Output ('[12%] Checked '+[Math]::Min($start+48,$pending.Count)+'/'+$pending.Count+
    ', reusable: '+$confirmed.Count)
 }
}

$jobData=Get-Content -LiteralPath $Job -Raw | ConvertFrom-Json
$url=[string]$jobData.url
$uri=$null
if($url.Length -gt 8192 -or ![Uri]::TryCreate($url,[UriKind]::Absolute,[ref]$uri) -or
 $uri.Scheme -notin @('http','https') -or $uri.UserInfo){throw 'Invalid HLS URL.'}
$quality=[string]$jobData.maxQuality
if($quality -notin @('best','4320','2880','2160','1440','1080','720','480','360','240')){$quality='best'}
$saveDir=[string]$jobData.saveDir
if(!$saveDir){throw 'Save directory is required.'}
[IO.Directory]::CreateDirectory($saveDir)|Out-Null
$ffmpeg=[string]$jobData.ffmpeg
if(!(Test-Path -LiteralPath $ffmpeg)){throw 'FFmpeg is missing.'}
$headers=@($jobData.headers)
$headerLines=[System.Collections.Generic.List[string]]::new()
foreach($h in $headers){
 if($null -eq $h){continue}
 $name=[string]$h.name;$value=[string]$h.value
 if($name -in @('Referer','Origin','User-Agent','Accept','Accept-Language') -and
  $value.Length -le 2048 -and $value.IndexOfAny([char[]]([char]13,[char]10,[char]0)) -lt 0){
  $headerLines.Add('header = "'+(Config-Escape ($name+': '+$value))+'"')
 }
}
$identity=$uri.GetLeftPart([UriPartial]::Path)+'|'+$quality
$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($identity))).Replace('-','').ToLowerInvariant()}
finally{$sha.Dispose()}
$base=Join-Path $env:LOCALAPPDATA 'UniversalVideoDownloader\CurlHls'
[IO.Directory]::CreateDirectory($base)|Out-Null
$temp=Join-Path $base ('resume-'+$hash.Substring(0,24))
# Adopt only a legacy temp folder for the same host/video/rendition.
if(!(Test-Path -LiteralPath $temp)){
 $prefix=$uri.AbsolutePath.Substring(0,$uri.AbsolutePath.LastIndexOf('/')+1)
 $best=$null;$bestCount=0
 foreach($dir in (Get-ChildItem $base -Directory|Where-Object{$_.Name -match '^[a-f0-9]{32}$'})){
  $cfg=Join-Path $dir.FullName 'curl.cfg'
  if(!(Test-Path -LiteralPath $cfg)){continue}
  $first=Select-String -LiteralPath $cfg -Pattern '^url = "'|Select-Object -First 1
  if(!$first){continue}
  $raw=$first.Line.Substring(7).TrimEnd('"')
  $old=$null
  if(![Uri]::TryCreate($raw,[UriKind]::Absolute,[ref]$old)){continue}
  if($old.Host -ne $uri.Host -or !$old.AbsolutePath.StartsWith($prefix,[StringComparison]::Ordinal)){continue}
  $count=@(Get-ChildItem $dir.FullName -File -Filter 'seg-*').Count
  if($count -gt $bestCount){$best=$dir;$bestCount=$count}
 }
 if($best -and $bestCount -gt 0){
  Move-Item -LiteralPath $best.FullName -Destination $temp
  Write-Output ('[3%] Found '+$bestCount+' interrupted fragments; validating before reuse.')
 }
}
[IO.Directory]::CreateDirectory($temp)|Out-Null
$lock=[IO.File]::Open((Join-Path $temp '.transfer.lock'),[IO.FileMode]::OpenOrCreate,
 [IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$success=$false
try{
 Write-Output '[5%] Reading HLS playlists with Windows curl.'
 $bandwidth=0L
 $current=$url
 for($depth=0;$depth -lt 3;$depth++){
  $body=Fetch-Text $current (Join-Path $temp ('playlist-'+$depth+'.m3u8')) $headers
  if(!$body.TrimStart().StartsWith('#EXTM3U')){throw 'Not an HLS playlist.'}
  $variants=@(Parse-Master $body)
  if($variants.Count -eq 0){break}
  $chosen=Select-Variant $variants $quality
  $bandwidth=[long]$chosen.bandwidth
  $current=Resolve-Http $current ([string]$chosen.uri)
  if($depth -eq 2){throw 'HLS master nesting too deep.'}
 }
 $media=Fetch-Text $current (Join-Path $temp 'media-original.m3u8') $headers
 if(@(Parse-Master $media).Count -gt 0){throw 'Expected a media playlist.'}
 $resourceList=[System.Collections.Generic.List[object]]::new()
 $localLines=[System.Collections.Generic.List[string]]::new()
 $index=0
 foreach($raw in ($media -split "\r?\n")){
  $line=$raw
  if($line -match '^#EXT-X-(?:MAP|KEY):.*URI="([^"]+)"'){
   $remote=$matches[1];$resolved=Resolve-Http $current $remote
   $ext=[IO.Path]::GetExtension(([Uri]$resolved).AbsolutePath)
   if(!$ext){$ext='.bin'}
   $local=('asset-{0:D5}{1}' -f $index,$ext);$index++
   $resourceList.Add([pscustomobject]@{url=$resolved;file=$local;isTs=$false})
   $line=$line.Replace('URI="'+$remote+'"','URI="'+$local+'"')
  }elseif($line.Trim() -and !$line.TrimStart().StartsWith('#')){
   $resolved=Resolve-Http $current $line.Trim()
   $ext=[IO.Path]::GetExtension(([Uri]$resolved).AbsolutePath).ToLowerInvariant()
   $isTs=$ext -in @('.jpeg','.jpg','.ts')
   if($isTs){$ext='.ts'}
   if(!$ext){$ext='.seg'}
   $local=('seg-{0:D5}{1}' -f $index,$ext);$index++
   $resourceList.Add([pscustomobject]@{url=$resolved;file=$local;isTs=$isTs})
   $line=$local
  }
  $localLines.Add($line)
 }
 if($resourceList.Count -lt 1){throw 'No HLS segments found.'}
 [IO.File]::WriteAllLines((Join-Path $temp 'local.m3u8'),$localLines,[Text.UTF8Encoding]::new($false))
 $checkFile=Join-Path $temp 'verified.json'
 $confirmed=[System.Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
 if(Test-Path -LiteralPath $checkFile){
  try{
   $saved=Get-Content -LiteralPath $checkFile -Raw|ConvertFrom-Json
   foreach($entry in $saved.PSObject.Properties){
    if($entry.Name -match '^(seg|asset)-[0-9]{5}\.[a-z0-9]+$'){$confirmed[$entry.Name]=[long]$entry.Value}
   }
  }catch{$confirmed.Clear()}
 }
 foreach($entry in @($confirmed.GetEnumerator())){
  $full=Join-Path $temp $entry.Key
  $row=$resourceList|Where-Object file -eq $entry.Key|Select-Object -First 1
  if(!$row -or !(Test-Path -LiteralPath $full) -or
   (Get-Item -LiteralPath $full).Length -ne $entry.Value -or
   ($row.isTs -and !(Check-Ts $full))){$confirmed.Remove($entry.Key)|Out-Null}
 }
 Verify-LegacyFiles $resourceList $temp $headerLines $confirmed
 Save-Checkpoint $checkFile $confirmed
 $missing=@($resourceList|Where-Object{-not $confirmed.ContainsKey($_.file)})
 $initialPercent=20+[int][Math]::Floor(65*$confirmed.Count/$resourceList.Count)
 Write-Output ('['+$initialPercent+'%] HLS resources: '+$confirmed.Count+'/'+$resourceList.Count+
  ' verified; '+$missing.Count+' remaining.')
 $reusedBytes=[long](($confirmed.Values|Measure-Object -Sum).Sum)
 $estimatedTotal=if($confirmed.Count -gt 0){[long][Math]::Max($reusedBytes,
    [Math]::Ceiling([double]$reusedBytes*$resourceList.Count/$confirmed.Count))}else{0L}
 if($estimatedTotal -le 0){
  if($bandwidth -gt 0){
   $seconds=[double]0
   foreach($line in ($media -split '\r?\n')){if($line -match '^#EXTINF:([0-9.]+)'){$seconds+=[double]::Parse($matches[1],[Globalization.CultureInfo]::InvariantCulture)}}
   if($seconds -gt 0){$estimatedTotal=[long][Math]::Ceiling($seconds*$bandwidth/8d)}
  }
  if($estimatedTotal -le 0){
   $segments=@($resourceList|Where-Object{!$_.file.StartsWith('asset-')})
   $positions=@(0,[int]($segments.Count/4),[int]($segments.Count/2),[int](3*$segments.Count/4),[Math]::Max(0,$segments.Count-1))|Select-Object -Unique
   $sizes=[Collections.Generic.List[long]]::new()
   foreach($i in $positions){
    if($i -ge $segments.Count){continue}
    $headArgs=@('-sS','-I','-L','--connect-timeout','5','--max-time','9','--output','NUL','--write-out','%{http_code}|%header{content-length}')
    $headArgs+=@(Curl-Args $headers);$headArgs+=@([string]$segments[$i].url)
    $result=(& curl.exe @headArgs 2>$null)-join ''
    if($LASTEXITCODE -eq 0 -and $result -match '^200\|(\d+)$'){$sizes.Add([long]$matches[1])}
   }
   if($sizes.Count -ge 2){$estimatedTotal=[long][Math]::Ceiling(($sizes|Measure-Object -Average).Average*$segments.Count)}
  }
 }
 if($estimatedTotal -gt 0){
  $verifiedPct=[Math]::Min(95,[Math]::Round(100d*$reusedBytes/$estimatedTotal,2))
  Write-Output ('[UVD_METRICS] '+$verifiedPct+'|'+$reusedBytes+'|'+$estimatedTotal+'|0|-1|1')
 }
 $connections=[Math]::Min(24,[Math]::Max(1,[int]$jobData.threads))
 $batchSize=32;$started=Get-Date;$newBytes=0L
 $lastRateTick=$started;$lastRateBytes=0L
 for($pos=0;$pos -lt $missing.Count;$pos+=$batchSize){
  $slice=@($missing|Select-Object -Skip $pos -First $batchSize)
  $done=$false
  for($attempt=1;$attempt -le 3;$attempt++){
   foreach($row in $slice){
    Remove-Item -LiteralPath (Join-Path $temp ($row.file+'.part')) -Force -ErrorAction SilentlyContinue
   }
   $cfg=Join-Path $temp 'curl-batch.cfg'
   $lines=[System.Collections.Generic.List[string]]::new()
   $parallel=[Math]::Max(2,[int][Math]::Floor($connections/[Math]::Pow(2,$attempt-1)))
   foreach($opt in @('silent','show-error','fail','location','connect-timeout = 15',
    'max-time = 180','retry = 3','retry-delay = 1','retry-all-errors',
    'parallel','parallel-immediate',('parallel-max = '+$parallel),
    ('parallel-max-host = '+$parallel),
    'write-out = "UVD_HLS|%{urlnum}|%{http_code}|%{size_download}\n"')){$lines.Add([string]$opt)}
   foreach($h in $headerLines){$lines.Add($h)}
   foreach($row in $slice){
    $lines.Add('url = "'+(Config-Escape ([string]$row.url))+'"')
    $lines.Add('output = "'+(Config-Escape ([string]($row.file+'.part')))+'"')
   }
   [IO.File]::WriteAllLines($cfg,$lines,[Text.UTF8Encoding]::new($false))
   $stdout=Join-Path $temp 'curl-batch-stdout.txt';$stderr=Join-Path $temp 'curl-batch-stderr.txt'
   Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue
   $proc=Start-Process -FilePath curl.exe -ArgumentList ('--config "'+$cfg+'" --output-dir "'+$temp+'"') -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
   $previousTick=Get-Date;$previousPartBytes=0L
   while(!$proc.HasExited){
    Start-Sleep -Milliseconds 950
    $proc.Refresh()
    $partBytes=0L;$activeCount=0
    foreach($row in $slice){
     $part=Join-Path $temp ($row.file+'.part')
     if(Test-Path -LiteralPath $part){$partBytes+=(Get-Item -LiteralPath $part).Length;$activeCount++}
    }
    $now=Get-Date
    $instant=[Math]::Max(0,($partBytes-$previousPartBytes)/[Math]::Max(.1,($now-$previousTick).TotalSeconds))
    $previousPartBytes=$partBytes;$previousTick=$now
    $observed=[long]($reusedBytes+$newBytes+$partBytes)
    $guess=if($estimatedTotal -gt 0){[long][Math]::Max($estimatedTotal,$observed)}
      elseif($activeCount -gt 0){[long][Math]::Max($observed,[Math]::Ceiling($partBytes/$activeCount*$resourceList.Count))}
      else{0L}
    if($guess -gt 0){
     $transferPct=[Math]::Min(95,[Math]::Round(100d*$observed/$guess,2))
     $seconds=if($instant -gt 1024){[long][Math]::Ceiling(($guess-$observed)/$instant)}else{-1}
     Write-Output ('[UVD_METRICS] '+$transferPct+'|'+$observed+'|'+$guess+'|'+[long]$instant+'|'+$seconds+'|1')
    }
   }
   $proc.WaitForExit()
   $reports=@(Get-Content -LiteralPath $stdout -ErrorAction SilentlyContinue|Where-Object{$_ -match '^UVD_HLS\|'})
   $valid=$reports.Count -eq $slice.Count
   foreach($line in $reports){
    if($line -notmatch '^UVD_HLS\|(\d+)\|200\|(\d+)$'){$valid=$false;continue}
    $index=[int]$matches[1];$reported=[long]$matches[2]
    if($index -ge $slice.Count -or $reported -le 0){$valid=$false;continue}
    $part=Join-Path $temp ($slice[$index].file+'.part')
    if(!(Test-Path -LiteralPath $part) -or (Get-Item -LiteralPath $part).Length -ne $reported){$valid=$false}
   }
   if($valid){
    foreach($row in $slice){
     $partial=Join-Path $temp ($row.file+'.part')
     if(!(Test-Path -LiteralPath $partial) -or
      (Get-Item -LiteralPath $partial).Length -le 0 -or
      ($row.isTs -and !(Check-Ts $partial))){$valid=$false;break}
    }
    if($valid){$done=$true;break}
   }
   Write-Output ('HLS batch retry '+$attempt+'/3; partial bytes are not reused.')
  }
  if(!$done){throw ('HLS batch could not complete ('+($pos+1)+'/'+$missing.Count+').')}
  foreach($row in $slice){
   $part=Join-Path $temp ($row.file+'.part')
   $dest=Join-Path $temp $row.file
   Move-Item -LiteralPath $part -Destination $dest -Force
   $size=(Get-Item -LiteralPath $dest).Length
   $confirmed[$row.file]=$size;$newBytes+=$size
  }
  Save-Checkpoint $checkFile $confirmed
  $pct=20+[int][Math]::Floor(65*$confirmed.Count/$resourceList.Count)
  $elapsed=[Math]::Max(1,((Get-Date)-$started).TotalSeconds)
  $now=Get-Date
  $instant=[Math]::Max(0,($newBytes-$lastRateBytes)/
    [Math]::Max(.1,($now-$lastRateTick).TotalSeconds))
  $lastRateTick=$now;$lastRateBytes=$newBytes
  $actual=[long](($confirmed.Values|Measure-Object -Sum).Sum)
  $estimated=[long][Math]::Max($actual,
      [Math]::Ceiling([double]$actual*$resourceList.Count/$confirmed.Count))
  $eta=if($instant -gt 1024){[long][Math]::Ceiling(($estimated-$actual)/$instant)}else{-1}
  Write-Output ('[UVD_METRICS] '+$pct+'|'+$actual+'|'+$estimated+'|'+
    [long]$instant+'|'+$eta+'|1')
  Write-Output ('['+$pct+'%] Verified '+$confirmed.Count+'/'+$resourceList.Count+
   ' resources; '+[Math]::Round($instant/1048576,2)+' MiB/s; resumable checkpoint saved.')
 }
 if($confirmed.Count -ne $resourceList.Count){throw 'Not all resources verified.'}
 Write-Output '[90%] All HLS resources verified. Remuxing to MP4.'
 $name=Safe-Name ([string]$jobData.saveName)
 if(!$name){$name=Safe-Name ([IO.Path]::GetFileName(([Uri]$current).AbsolutePath.TrimEnd('/')))}
 if(!$name){$name='UVD-'+(Get-Date -Format 'yyyyMMdd-HHmmss')}
 if([IO.Path]::GetExtension($name)){$name=[IO.Path]::GetFileNameWithoutExtension($name)}
 $output=Unique-Output (Join-Path $saveDir ($name+'.mp4'))
 $staging=$output+'.partial.mp4'
 & $ffmpeg -hide_banner -loglevel warning -protocol_whitelist file,crypto,data -allowed_extensions ALL -i (Join-Path $temp 'local.m3u8') -map '0:v?' -map '0:a?' -c copy -movflags +faststart -y $staging
 if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $staging) -or
   (Get-Item -LiteralPath $staging).Length -lt 1024){throw 'Local FFmpeg remux failed; fragments retained.'}
 Move-Item -LiteralPath $staging -Destination $output
 $finalBytes=(Get-Item -LiteralPath $output).Length
 Write-Output ('[UVD_METRICS] 100|'+$finalBytes+'|'+$finalBytes+'|0|0|0')
 Write-Output '[100%] HLS download and remux complete.'
 Write-Output ('Saved: '+$output)
 $success=$true
}finally{
 $lock.Dispose()
 Remove-Item -LiteralPath $Job -Force -ErrorAction SilentlyContinue
 if($success){Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}
 else{Write-Output 'Stopped or incomplete. Verified fragments retained for safe resume.'}
}

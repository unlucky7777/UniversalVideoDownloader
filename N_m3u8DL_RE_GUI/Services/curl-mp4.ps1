param([Parameter(Mandatory=$true)][string]$Job)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
function Esc([string]$v){return $v.Replace('\','\\').Replace('"','\"')}
function OutMetric([long]$bytes,[long]$total,[double]$rate){
 $rate=[Math]::Max(0,$rate);$pct=100*$bytes/[Math]::Max(1,$total)
 $eta=if($rate -gt 1024){[long][Math]::Ceiling(($total-$bytes)/$rate)}else{-1}
 Write-Output ('[UVD_METRICS] '+$pct.ToString('F2',[Globalization.CultureInfo]::InvariantCulture)+'|'+$bytes+'|'+$total+'|'+[long]$rate+'|'+$eta+'|0')
}
function CleanName([string]$n){
 foreach($c in [IO.Path]::GetInvalidFileNameChars()){$n=$n.Replace([string]$c,'_')}
 return $n.Trim().TrimEnd('.')
}
$j=Get-Content -LiteralPath $Job -Raw | ConvertFrom-Json
$url=[string]$j.url;$u=$null
if($url.Length -gt 8192 -or ![uri]::TryCreate($url,[UriKind]::Absolute,[ref]$u) -or
 $u.Scheme -ne 'https' -or ($u.Host -ne 'pstatic.net' -and !$u.Host.EndsWith('.pstatic.net')) -or
 !$u.AbsolutePath.EndsWith('.mp4',[StringComparison]::OrdinalIgnoreCase) -or $u.UserInfo){
 throw 'Direct Range transport accepts observed HTTPS Naver MP4 URLs only.'
}
$hdr=[Collections.Generic.List[string]]::new()
$argHdr=[Collections.Generic.List[string]]::new()
foreach($h in @($j.headers)){
 if($null -eq $h){continue}
 $n=[string]$h.name;$v=[string]$h.value
 if($n -notin @('Referer','Origin','User-Agent','Accept','Accept-Language') -or
    $v.Length -gt 2048 -or $v.IndexOfAny([char[]]([char]13,[char]10,[char]0)) -ge 0){continue}
 $hdr.Add('header = "'+(Esc ($n+': '+$v))+'"')
 $argHdr.Add('-H');$argHdr.Add($n+': '+$v)
}
$argsList=@('-sS','--location','--range','0-0','--connect-timeout','8','--max-time','18','--output','NUL',
 '--write-out','%{http_code}|%header{content-range}')
$argsList+=@($argHdr);$argsList+=$url
$reply=(& curl.exe @argsList 2>$null)-join ''
if($LASTEXITCODE -ne 0 -or $reply -notmatch '^206\|bytes\s+0-0/(\d+)'){
 throw 'HTTP 206 Content-Range was not confirmed.'
}
$total=[long]$matches[1]
if($total -lt 1024 -or $total -gt 34359738368){throw 'Invalid server MP4 size.'}
$dir=[string]$j.saveDir
[IO.Directory]::CreateDirectory($dir)|Out-Null
$name=CleanName ([string]$j.saveName)
if(!$name){$name=CleanName ([IO.Path]::GetFileNameWithoutExtension($u.AbsolutePath))}
if(!$name){$name='UVD-Naver'}
if($name.EndsWith('.mp4',[StringComparison]::OrdinalIgnoreCase)){$name=$name.Substring(0,$name.Length-4)}
$target=Join-Path $dir ($name+'.mp4')
if(Test-Path $target){
 if((Get-Item $target).Length -eq $total){OutMetric $total $total 0;exit 0}
 $target=Join-Path $dir ($name+'-'+(Get-Date -Format yyyyMMddHHmmss)+'.mp4')
}
$sha=[Security.Cryptography.SHA256]::Create()
try{$key=$u.GetLeftPart([UriPartial]::Path)+'|'+$total
 $hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key))).Replace('-','').ToLowerInvariant()}
finally{$sha.Dispose()}
$temp=Join-Path $env:LOCALAPPDATA ('UniversalVideoDownloader\CurlMp4\'+$hash.Substring(0,24))
[IO.Directory]::CreateDirectory($temp)|Out-Null
$lock=[IO.File]::Open((Join-Path $temp '.transfer.lock'),[IO.FileMode]::OpenOrCreate,
 [IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$success=$false
try{
 $chunk=8MB;$count=[int][Math]::Ceiling($total/$chunk)
 $rows=@(for($i=0;$i -lt $count;$i++){
  $begin=[long]$i*$chunk;$end=[Math]::Min($total-1,$begin+$chunk-1)
  [pscustomobject]@{Begin=$begin;End=$end;Size=($end-$begin+1);Name=('chunk-{0:D5}.bin' -f $i)}
 })
 $complete=0L
 foreach($row in $rows){
  $p=Join-Path $temp $row.Name
  if((Test-Path $p) -and (Get-Item $p).Length -eq $row.Size){$complete+=$row.Size}
  else{Remove-Item $p -Force -ErrorAction SilentlyContinue}
 }
 OutMetric $complete $total 0
 $missing=@($rows|Where-Object {!(Test-Path (Join-Path $temp $_.Name))})
 if($j.PSObject.Properties.Name -contains 'smoke' -and $j.smoke){$missing=@($missing|Select-Object -First 2)}
 $connections=[Math]::Min(24,[Math]::Max(1,[int]$j.connections))
 Write-Output ('Naver MP4: '+$count+' Range chunks, concurrency '+$connections+', '+$missing.Count+' remaining.')
 for($offset=0;$offset -lt $missing.Count;$offset+=$connections){
  $batch=@($missing|Select-Object -Skip $offset -First $connections)
  $ok=$false
  for($attempt=1;$attempt -le 3;$attempt++){
   foreach($row in $batch){Remove-Item (Join-Path $temp ($row.Name+'.part')) -Force -ErrorAction SilentlyContinue}
   $cfg=Join-Path $temp 'curl-range.cfg'
   $lines=[Collections.Generic.List[string]]::new()
   foreach($s in @('parallel','parallel-immediate',
    ('parallel-max = '+[Math]::Max(1,[int]($connections/$attempt))))){$lines.Add([string]$s)}
   $first=$true
   foreach($row in $batch){
    if(!$first){$lines.Add('next')}
    $first=$false
    foreach($s in @('silent','show-error','fail','location','connect-timeout = 12',
     'max-time = 180','retry = 2','retry-delay = 1',
     'write-out = "UVD_RANGE|%{urlnum}|%{http_code}|%{size_download}|%header{content-range}\n"')){$lines.Add([string]$s)}
    foreach($h in $hdr){$lines.Add($h)}
    $lines.Add('url = "'+(Esc $url)+'"')
    $lines.Add('range = "'+$row.Begin+'-'+$row.End+'"')
    $lines.Add('output = "'+(Esc (Join-Path $temp ($row.Name+'.part')))+'"')
   }
   [IO.File]::WriteAllLines($cfg,$lines,[Text.UTF8Encoding]::new($false))
   $stdout=Join-Path $temp 'curl-out.txt';$stderr=Join-Path $temp 'curl-err.txt'
   Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue
   $proc=Start-Process -FilePath curl.exe -ArgumentList ('--config "'+$cfg+'"') -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
   $previousBytes=$complete;$previousTime=Get-Date;$oldLive=0L
   while(!$proc.HasExited){
    Start-Sleep -Milliseconds 900
    $proc.Refresh()
    $live=0L
    foreach($row in $batch){
     $p=Join-Path $temp ($row.Name+'.part')
     if(Test-Path $p){$live+=(Get-Item $p).Length}
    }
    $interval=[Math]::Max(.5,((Get-Date)-$previousTime).TotalSeconds)
    $speed=[Math]::Max(0,($live-$oldLive)/$interval)
    OutMetric ($complete+$live) $total $speed
    $previousTime=Get-Date;$oldLive=$live
   }
   $proc.WaitForExit()
   # Windows PowerShell 5.1 Start-Process can omit ExitCode from its
   # returned Process object when stdout/stderr are redirected. Verify each
   # HTTP response plus exact chunk length instead of treating null as failure.
   $valid=$true
   $reports=@(Get-Content $stdout -ErrorAction SilentlyContinue | Where-Object {$_ -match '^UVD_RANGE\|'})
   if($reports.Count -ne $batch.Count){$valid=$false}
   foreach($line in $reports){
    if($line -notmatch '^UVD_RANGE\|(\d+)\|206\|(\d+)\|bytes\s+(\d+)-(\d+)/(\d+)$'){$valid=$false;continue}
    $i=[int]$matches[1];$n=[long]$matches[2]
    $from=[long]$matches[3];$to=[long]$matches[4];$all=[long]$matches[5]
    if($i -ge $batch.Count -or $n -ne $batch[$i].Size -or
       $from -ne $batch[$i].Begin -or $to -ne $batch[$i].End -or $all -ne $total){$valid=$false}
   }
   foreach($row in $batch){
    $p=Join-Path $temp ($row.Name+'.part')
    if(!(Test-Path $p) -or (Get-Item $p).Length -ne $row.Size){$valid=$false}
   }
   if($valid){$ok=$true;break}
   Write-Output ('Range retry '+$attempt+'/3. Incomplete chunks will not be reused.')
  }
  if(!$ok){throw 'A Range batch failed. Existing verified chunks were preserved.'}
  foreach($row in $batch){
   Move-Item (Join-Path $temp ($row.Name+'.part')) (Join-Path $temp $row.Name) -Force
   $complete+=$row.Size
  }
  OutMetric $complete $total 0
 }
 if($j.PSObject.Properties.Name -contains 'smoke' -and $j.smoke){
  Write-Output ('UVD_MP4_RANGE_SMOKE_PASS; confirmed '+$complete+' bytes.')
  exit 0
 }
 if($complete -ne $total){throw 'Incomplete Range download.'}
 Write-Output 'Combining verified MP4 chunks locally...'
 $partial=$target+'.partial'
 $out=[IO.File]::Create($partial)
 try{
  foreach($row in $rows){
   $inp=[IO.File]::OpenRead((Join-Path $temp $row.Name))
   try{$inp.CopyTo($out)}finally{$inp.Dispose()}
  }
  $out.Flush($true)
 }finally{$out.Dispose()}
 if((Get-Item $partial).Length -ne $total){throw 'Assembled MP4 length mismatch.'}
 Move-Item $partial $target
 OutMetric $total $total 0
 Write-Output ('MP4 complete: '+[IO.Path]::GetFileName($target))
 $success=$true
}finally{
 $lock.Dispose()
 Remove-Item $Job -Force -ErrorAction SilentlyContinue
 if($success){Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue}
 else{Write-Output 'Verified MP4 chunks preserved for a later resume.'}
}

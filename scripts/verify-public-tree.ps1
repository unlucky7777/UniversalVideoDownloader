param(
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-CandidateFiles {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($git -and (Test-Path (Join-Path $repo '.git'))) {
        Push-Location $repo
        try {
            return @(& $git.Source ls-files).ForEach({ Join-Path $repo $_ })
        } finally { Pop-Location }
    }

    return @(Get-ChildItem $repo -Recurse -File -Force |
        Where-Object {
            $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' -and
            $_.Name -notin @('ffmpeg.exe','yt-dlp.exe','N_m3u8DL-RE.exe','suite-version.json')
        }).FullName
}

$files = @(Get-CandidateFiles)
$errors = [System.Collections.Generic.List[string]]::new()

$forbiddenNames = @(
    'com.uvd.bridge.json','config.json','config.txt','.env',
    'cookies.txt','credentials.json','secrets.json'
)
$forbiddenExtensions = @('.exe','.dll','.pfx','.p12','.pem','.mp4','.mkv','.webm','.mov','.ts')

foreach ($path in $files) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $file = Get-Item -LiteralPath $path
    $base = $repo.TrimEnd('\') + '\'
    $rel = if ($file.FullName.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) {
        $file.FullName.Substring($base.Length).Replace('\','/')
    } else {
        $file.FullName.Replace('\','/')
    }

    if ($forbiddenNames -contains $file.Name -or $forbiddenExtensions -contains $file.Extension.ToLowerInvariant()) {
        $errors.Add("forbidden tracked artifact: $rel")
    }
    if ($file.Length -gt 20MB) {
        $errors.Add("tracked file exceeds 20 MiB: $rel ($([math]::Round($file.Length/1MB,1)) MiB)")
    }

    if ($file.Length -gt 5MB -or $file.Extension.ToLowerInvariant() -notin
        @('.cs','.js','.cjs','.mjs','.ps1','.py','.json','.md','.txt','.xml','.props','.csproj','.yml','.yaml','.html','.css','.gitignore')) {
        continue
    }

    $text = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $text) { continue }

    $secretPatterns = @(
        '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
        '\bgh[pousr]_[A-Za-z0-9_]{20,}\b',
        '\bsk-[A-Za-z0-9_-]{20,}\b',
        '\bAIza[0-9A-Za-z_-]{20,}\b',
        '(?i)(?:api[_-]?key|client[_-]?secret|access[_-]?token|refresh[_-]?token|password)\s*[:=]\s*["''][^"'']{12,}["'']'
    )
    foreach ($pattern in $secretPatterns) {
        if ($text -match $pattern) {
            $errors.Add("possible secret in: $rel")
            break
        }
    }

    if ($text -match '(?i)[A-Z]:\\Users\\[^\\\r\n]+') {
        $errors.Add("machine-specific user path in: $rel")
    }
}

foreach ($required in @('runtime-manifest.json','THIRD_PARTY_NOTICES.md','SECURITY.md')) {
    if (!(Test-Path (Join-Path $repo $required))) {
        $errors.Add("$required is missing")
    }
}

Write-Host "Public-tree check: $($files.Count) files inspected"
foreach ($error in $errors) { Write-Error $error }

if ($errors.Count) { exit 1 }
Write-Host 'PUBLIC_TREE_CHECK=PASS'

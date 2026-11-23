[CmdletBinding()]
param([string]$EditorPath = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity/HomelessPetHelper'
$batch = Join-Path $root 'art-source/bgm-placeholder-20261123'
$reports = Join-Path $root 'test-results/bgm-integration'
$snapshot = Join-Path $reports "author-$([Guid]::NewGuid().ToString('N'))"
$owned = @('Assets/Settings/Game/GameContent.asset',
    'Assets/Art/Audio/PlaceholderMusic.meta',
    'Assets/Settings/Audio/PlaceholderMusicCatalog.asset',
    'Assets/Settings/Audio/PlaceholderMusicCatalog.asset.meta',
    'Assets/Scripts/Game/MainMusicSession.cs.meta',
    'Assets/Scripts/Editor/Audio/PlaceholderMusicAssets.cs.meta',
    'Assets/Tests/Audio/EditMode/MusicAssetTests.cs.meta',
    'Assets/Tests/PlayMode/MainMusicTests.cs.meta')
$manifest = Get-Content -Raw -LiteralPath (Join-Path $batch 'manifest.json') | ConvertFrom-Json
foreach ($cue in $manifest.cues) {
    $owned += "Assets/Art/Audio/PlaceholderMusic/$($cue.assetName).wav"
    $owned += "Assets/Art/Audio/PlaceholderMusic/$($cue.assetName).wav.meta"
}
$before = @{}
foreach ($file in $owned) {
    $path = Join-Path $source $file
    $before[$file] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path).Hash } else { $null }
}
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $snapshot -Recurse
}
$cache = Join-Path $source 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Join-Path $snapshot 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $snapshot 'Library') -Recurse
}
$audio = Join-Path $snapshot 'Assets/Art/Audio/PlaceholderMusic'
New-Item -ItemType Directory -Path $audio -Force | Out-Null
foreach ($cue in $manifest.cues) {
    $inputFile = Join-Path $batch $cue.master.path
    if ((Get-FileHash -LiteralPath $inputFile).Hash.ToLowerInvariant() -ne $cue.master.sha256) {
        throw "Master changed: $($cue.id)"
    }
    $output = Join-Path $audio "$($cue.assetName).wav"
    if ((Test-Path -LiteralPath $output) -and
        (Get-FileHash -LiteralPath $output).Hash.ToLowerInvariant() -ne $cue.master.sha256) {
        throw "Refusing to replace a different clip: $output"
    }
    Copy-Item -LiteralPath $inputFile -Destination $output
}
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users/Public' }
$log = Join-Path $snapshot 'author.log'
Write-Output "Isolated music authoring: $snapshot"
$process = Start-Process -FilePath $EditorPath -ArgumentList @('-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$snapshot`"", '-logFile', "`"$log`"", '-executeMethod',
    'HomelessPetHelper.Editor.Audio.PlaceholderMusicAssets.Apply') -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Author failed ($($process.ExitCode)); no outputs copied. $log" }
foreach ($file in $owned) {
    $path = Join-Path $source $file
    $current = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path).Hash } else { $null }
    if ($current -ne $before[$file]) { throw "Workspace changed; no outputs copied: $file" }
    if (!(Test-Path -LiteralPath (Join-Path $snapshot $file))) { throw "Missing output: $file" }
}
foreach ($file in $owned) {
    $destination = Join-Path $source $file
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $snapshot $file) -Destination $destination -Force
}
$import = Get-Content -Raw -LiteralPath (Join-Path $snapshot 'BgmEvidence/import-report.json') | ConvertFrom-Json
$records = foreach ($cue in $manifest.cues) {
    [ordered]@{ id=$cue.suggestedCueId; runtimeFile="unity/HomelessPetHelper/Assets/Art/Audio/PlaceholderMusic/$($cue.assetName).wav";
        sourceSha256=$cue.master.sha256; status='temporary'; final=$false; useAuthorization='user-authorized-placeholder';
        listening='pending'; rights='pending'; targetDevice='pending'; loadTiming='first-play-static-dependency' }
}
$result = [ordered]@{ sourceManifest='manifest.json'; sourceManifestSha256=(Get-FileHash (Join-Path $batch 'manifest.json')).Hash.ToLowerInvariant();
    assets=@($records); nativeImport='Standalone:Streaming/Vorbis/32000Hz/stereo/quality0.30';
    browserDefault='CompressedInMemory/Vorbis/32000Hz/stereo/quality0.30-unverified-on-WeChat';
    catalogGain=0.65; audit=@{ target='StandaloneWindows64'; format='uncompressed AssetBundles containing compressed audio';
        totalBundleBytes=$import.totalBundleBytes; clips=$import.clips };
    snapshot=[IO.Path]::GetRelativePath($root,$snapshot).Replace('\','/');
    log=[IO.Path]::GetRelativePath($root,$log).Replace('\','/') }
[IO.File]::WriteAllText((Join-Path $batch 'integration.json'), ($result | ConvertTo-Json -Depth 8),
    [Text.UTF8Encoding]::new($false))
Write-Output "Music assets synchronized; compressed bundle audit: $($import.totalBundleBytes) bytes"

[CmdletBinding()]
param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [ValidatePattern('^[A-Za-z0-9-]+$')][string]$ReportSuffix = 'QuickAudio'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity\HomelessPetHelper'
$batch = Join-Path $root 'art-source\audio-basic-20261120'
$reports = Join-Path $root 'test-results\unity'
$snapshot = Join-Path $reports "isolated-AudioAssets-$([Guid]::NewGuid().ToString('N'))"
$owned = @('Assets/Prefabs/Panels/Panel_StationLoop.prefab', 'Assets/Prefabs/Panels/Panel_CityMap.prefab',
    'Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab', 'Assets/Scenes/Main.unity',
    'Assets/Settings/Game/GameContent.asset')
$before = @{}
foreach ($file in $owned) { $before[$file] = (Get-FileHash -LiteralPath (Join-Path $source $file)).Hash }
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings'))
{
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $snapshot -Recurse
}
$cache = Join-Path $source 'Library\PackageCache'
if (Test-Path -LiteralPath $cache)
{
    New-Item -ItemType Directory -Path (Join-Path $snapshot 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $snapshot 'Library') -Recurse
}
$names = @{ 'ui-tap' = 'UI_Tap'; 'ui-open' = 'UI_Open'; 'merge' = 'Merge';
    'rescue-success' = 'RescueSuccess'; 'reward' = 'Reward'; 'gentle-notice' = 'Notice' }
$manifest = Get-Content -LiteralPath (Join-Path $batch 'v2\manifest.json') -Raw | ConvertFrom-Json
$audioFolder = Join-Path $snapshot 'Assets\Art\Audio'
New-Item -ItemType Directory -Path $audioFolder -Force | Out-Null
foreach ($asset in $manifest.assets)
{
    $inputFile = Join-Path $batch ('v2/' + $asset.candidate.file)
    if ((Get-FileHash -LiteralPath $inputFile).Hash.ToLowerInvariant() -ne $asset.candidate.sha256)
        { throw "Reviewed source changed: $($asset.id)" }
    $output = Join-Path $audioFolder ("SFX_$($names[$asset.id]).wav")
    if ((Test-Path -LiteralPath $output) -and
        (Get-FileHash -LiteralPath $output).Hash.ToLowerInvariant() -ne $asset.candidate.sha256)
        { throw "Refusing to replace a different runtime clip: $output" }
    Copy-Item -LiteralPath $inputFile -Destination $output
}
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users\Public' }
$log = Join-Path $reports "AudioAssets-$ReportSuffix.log"
$arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', "`"$snapshot`"", '-logFile', "`"$log`"",
    '-executeMethod', 'HomelessPetHelper.Editor.Audio.BasicAudioAssets.Apply')
Write-Output "Isolated audio authoring: $snapshot"
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Audio authoring failed ($($process.ExitCode)). Log: $log" }
foreach ($file in $owned)
{
    if ((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -ne $before[$file])
        { throw "Workspace changed during authoring; no output synchronized: $file" }
}
# Sync only owned outputs; the Siege source prefab and all unrelated snapshot files remain untouched.
foreach ($file in $owned) { Copy-Item -LiteralPath (Join-Path $snapshot $file) -Destination (Join-Path $source $file) }
foreach ($folder in @('Assets/Art/Audio', 'Assets/Settings/Audio'))
{
    Copy-Item -LiteralPath (Join-Path $snapshot $folder) -Destination (Join-Path $source (Split-Path $folder -Parent)) -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $snapshot "$folder.meta") -Destination (Join-Path $source "$folder.meta") -Force
}
$newMetaRoots = @('Assets/Scripts/Framework/Audio/UI', 'Assets/Scripts/Editor/Audio', 'Assets/Tests/Audio')
foreach ($folder in $newMetaRoots)
{
    foreach ($meta in Get-ChildItem -LiteralPath (Join-Path $snapshot $folder) -Filter '*.meta' -File -Recurse)
    {
        $relative = $meta.FullName.Substring($snapshot.Length + 1)
        $destination = Join-Path $source $relative
        if (-not (Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath $meta.FullName -Destination $destination }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $source "$folder.meta")))
        { Copy-Item -LiteralPath (Join-Path $snapshot "$folder.meta") -Destination (Join-Path $source "$folder.meta") }
}
$cueMeta = 'Assets/Scripts/Game/GameAudioCues.cs.meta'
if (-not (Test-Path -LiteralPath (Join-Path $source $cueMeta)))
    { Copy-Item -LiteralPath (Join-Path $snapshot $cueMeta) -Destination (Join-Path $source $cueMeta) }
$records = foreach ($asset in $manifest.assets)
{
    [ordered]@{ id = $asset.id; sourceSha256 = $asset.candidate.sha256;
        runtimeFile = "unity/HomelessPetHelper/Assets/Art/Audio/SFX_$($names[$asset.id]).wav";
        status = 'temporary'; listening = 'accepted-by-user'; rights = 'pending'; targetDevice = 'pending';
        loadTiming = 'first-play-static-dependency' }
}
$integration = [ordered]@{ schemaVersion = 1; sourceManifest = 'v2/manifest.json'; assets = @($records) }
[IO.File]::WriteAllText((Join-Path $batch 'integration.json'),
    ($integration | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output "Audio assets synchronized. Preserved isolated snapshot and log: $log"

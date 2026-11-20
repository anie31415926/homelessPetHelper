[CmdletBinding()]
param(
    [ValidateSet('Create', 'Polish', 'Scene', 'ScenePolish')][string]$Action = 'Create',
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity\HomelessPetHelper'
$reports = Join-Path $root 'test-results\unity'
$snapshot = Join-Path $reports "isolated-Habitat-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings'))
{
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $snapshot -Recurse
}
$cache = Join-Path $source 'Library\PackageCache'
if (Test-Path -LiteralPath $cache)
{
    $library = Join-Path $snapshot 'Library'
    New-Item -ItemType Directory -Path $library | Out-Null
    Copy-Item -LiteralPath $cache -Destination $library -Recurse
}
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users\Public' }
$log = Join-Path $reports "StationHabitat-$Action.log"
$method = if ($Action -eq 'Create') { 'Apply' } else { 'Polish' }
$entry = switch ($Action)
{
    'Scene' { 'StationSceneAuthor.Apply' }
    'ScenePolish' { 'StationSceneAuthor.Polish' }
    default { "StationHabitatAuthor.$method" }
}
$arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', "`"$snapshot`"", '-logFile', "`"$log`"",
    '-executeMethod', "HomelessPetHelper.Editor.StationHabitat.$entry")
Write-Output "Isolated habitat snapshot: $snapshot"
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Habitat author exit: $($process.ExitCode). Log: $log"
if ($process.ExitCode -ne 0) { throw 'Habitat authoring failed; inspect log.' }
Write-Output 'Outputs stay isolated. Inspect, then sync only the owned actor prefab, station prefab, preview scene and metas.'

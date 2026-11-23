[CmdletBinding()]
param([ValidateSet('Apply','Refine','Immerse')][string]$Action = 'Apply',
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity/HomelessPetHelper'
$project = Join-Path $root "test-results/unity/isolated-StationUi-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $project -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $project -Recurse
}
$cache = Join-Path $source 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Join-Path $project 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $project 'Library') -Recurse
}
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users/Public' }
$log = Join-Path $project 'station-ui-author.log'
$method = if ($Action -eq 'Immerse') { 'StationImmersiveUiAuthor.Apply' } else { "StationUiAuthor.$Action" }
Write-Output "Isolated saved project: $project"
$process = Start-Process -FilePath $EditorPath -ArgumentList @('-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$project`"", '-logFile', "`"$log`"", '-executeMethod',
    "HomelessPetHelper.Editor.StationHabitat.$method") -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Unity exit=$($process.ExitCode); log=$log"
if ($process.ExitCode -ne 0) { throw 'Author failed. No source assets copied.' }
Write-Output 'Inspect and copy only the owned station prefab. Shared assets are read-only.'

[CmdletBinding()]
param([string]$EditorPath = $env:UNITY_EDITOR_PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity/HomelessPetHelper'
$reports = Join-Path $root 'test-results/unity'
$project = Join-Path $reports "isolated-StationRoom-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $project -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $project -Recurse
}
$cache = Join-Path $source 'Library/PackageCache'
if (Test-Path -LiteralPath $cache) {
    New-Item -ItemType Directory -Path (Join-Path $project 'Library') -Force | Out-Null
    Copy-Item -LiteralPath $cache -Destination (Join-Path $project 'Library') -Recurse
}
$art = Join-Path $root 'art-source/station-room-20261123'
$inputs = Join-Path $project 'StationRoomSource'
New-Item -ItemType Directory -Path $inputs | Out-Null
foreach ($file in @('unity-import.json', 'T_StationRoom_Background.jpg', 'T_StationRoom_Furniture.png')) {
    Copy-Item -LiteralPath (Join-Path $art $file) -Destination $inputs
}
$version = (Select-String -LiteralPath (Join-Path $source 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users/Public' }
$log = Join-Path $reports "StationRoom-$([Guid]::NewGuid().ToString('N')).log"
Write-Output "Isolated project: $project"
$process = Start-Process -FilePath $EditorPath -ArgumentList @('-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$project`"", '-logFile', "`"$log`"", '-executeMethod',
    'HomelessPetHelper.Editor.StationHabitat.StationRoomAuthor.Apply') -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Unity exit=$($process.ExitCode); log=$log"
if ($process.ExitCode -ne 0) { throw 'Station room author failed; inspect log, no source assets copied.' }
Write-Output "Author complete. Inspect and copy only owned assets and new metas from: $project"

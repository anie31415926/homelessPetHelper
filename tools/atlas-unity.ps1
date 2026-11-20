[CmdletBinding()]
param(
    [ValidateSet('Sync', 'Validate', 'Pack', 'Evaluate', 'BuildProbe')]
    [string]$Action = 'Validate',
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe',
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'unity\HomelessPetHelper'
$reports = Join-Path $root 'test-results\unity'
$snapshot = Join-Path $reports "isolated-Atlas-$([Guid]::NewGuid().ToString('N'))"
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
$method = switch ($Action)
{
    'Sync' { 'HomelessPetHelper.Editor.Atlas.UiAtlasTools.Sync' }
    'Pack' { 'HomelessPetHelper.Editor.Atlas.UiAtlasTools.PackPreview' }
    'Evaluate' { 'HomelessPetHelper.Editor.Atlas.UiAtlasEvaluation.Evaluate' }
    'BuildProbe' { 'HomelessPetHelper.Editor.Atlas.UiAtlasEvaluation.BuildStandaloneProbe' }
    default { 'HomelessPetHelper.Editor.Atlas.AtlasBuildGuard.ValidateAll' }
}
$log = Join-Path $reports "Atlas-$Action.log"
$arguments = @('-batchmode', '-quit', '-projectPath', "`"$snapshot`"", '-logFile', "`"$log`"", '-executeMethod', $method)
if (-not $Graphics) { $arguments += '-nographics' }
Write-Output "Isolated atlas snapshot: $snapshot"
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Atlas $Action exit: $($process.ExitCode). Log: $log"
if ($process.ExitCode -ne 0) { throw 'Atlas Unity command failed; inspect log.' }
Write-Output 'Outputs stay in the isolated snapshot. Sync only owned Atlas directories back after inspection.'

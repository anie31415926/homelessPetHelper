[CmdletBinding()]
param(
    [ValidateSet('Initialize', 'PreviewAssets', 'PreviewLayout', 'CaseAssets', 'CaseLayout',
        'StationAssets', 'StationArt',
        'EditMode', 'PlayMode', 'Build', 'BuildDevelopment')]
    [string]$Task = 'EditMode',
    [string]$EditorPath = $env:UNITY_EDITOR_PATH,
    [string]$TestFilter,
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$ReportSuffix,
    [switch]$Isolated,
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'unity\HomelessPetHelper'
$reportPath = Join-Path $repoRoot 'test-results\unity'
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$version = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value

if ([string]::IsNullOrWhiteSpace($EditorPath))
{
    $EditorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}

if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf))
{
    throw 'Unity editor not found. Set UNITY_EDITOR_PATH or pass -EditorPath.'
}

# The desktop task shell may omit these normal Windows variables; UPM requires them.
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE))
{
    $env:ALLUSERSPROFILE = $env:ProgramData
}

if ([string]::IsNullOrWhiteSpace($env:PUBLIC))
{
    $env:PUBLIC = Join-Path $env:SystemDrive 'Users\Public'
}

New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
$reportName = $Task
if ($Isolated)
{
    $sourceProject = $projectPath
    $projectPath = Join-Path $reportPath "isolated-$Task-$([Guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $projectPath | Out-Null
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings'))
    {
        Copy-Item -LiteralPath (Join-Path $sourceProject $folder) -Destination $projectPath -Recurse
    }

    # Reuse immutable package cache only, never the open editor's databases or lock files.
    $sourceCache = Join-Path $sourceProject 'Library\PackageCache'
    if (Test-Path -LiteralPath $sourceCache -PathType Container)
    {
        $libraryPath = Join-Path $projectPath 'Library'
        New-Item -ItemType Directory -Path $libraryPath | Out-Null
        Copy-Item -LiteralPath $sourceCache -Destination $libraryPath -Recurse
    }

    $reportName = "$Task-isolated"
    Write-Output "Isolated saved-project snapshot: $projectPath"
}

if (-not [string]::IsNullOrWhiteSpace($ReportSuffix))
{
    $reportName = "$reportName-$ReportSuffix"
}

$logPath = Join-Path $reportPath "$reportName.log"
$resultPath = Join-Path $reportPath "$reportName.xml"
$unityArguments = @('-batchmode', '-projectPath', "`"$projectPath`"", '-logFile', "`"$logPath`"")
if (-not $Graphics)
{
    $unityArguments += '-nographics'
}

switch ($Task)
{
    'Initialize'
    {
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.ProjectSetup.Initialize')
    }
    'PreviewAssets'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.OptionPreview.OptionPreviewAssets.CreateAssets')
    }
    'PreviewLayout'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.OptionPreview.OptionPreviewAssets.NormalizeLayout')
    }
    'CaseAssets'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.RescueCase.RescueCaseAssets.CreateAssets')
    }
    'CaseLayout'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.RescueCase.RescueCaseAssets.NormalizeLayout')
    }
    'StationAssets'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.StationPreview.StationPreviewAssets.CreateAssets')
    }
    'StationArt'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.StationPreview.StationPreviewAssets.NormalizeArt')
    }
    'Build'
    {
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.DesktopBuild.Build')
    }
    'BuildDevelopment'
    {
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.DesktopBuild.BuildDevelopment')
    }
    default
    {
        # -quit would terminate the test runner before its completion callback.
        if (Test-Path -LiteralPath $resultPath)
        {
            Remove-Item -LiteralPath $resultPath
        }

        $unityArguments += @('-runTests', '-testPlatform', $Task, '-testResults', "`"$resultPath`"")
        if (-not [string]::IsNullOrWhiteSpace($TestFilter))
        {
            $unityArguments += @('-testFilter', "`"$TestFilter`"")
        }
    }
}

$isTestRun = $Task -in @('EditMode', 'PlayMode')
$playerSettingsPath = Join-Path $projectPath 'ProjectSettings\ProjectSettings.asset'
$settingsBackup = Join-Path $reportPath "$reportName-ProjectSettings.before-tests.asset"
if ($isTestRun)
{
    Copy-Item -LiteralPath $playerSettingsPath -Destination $settingsBackup -Force
}

try
{
    $process = Start-Process -FilePath $EditorPath -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
}
finally
{
    # Unity Test Framework enables runInBackground; keep tests from changing player configuration.
    if ($isTestRun)
    {
        Copy-Item -LiteralPath $settingsBackup -Destination $playerSettingsPath -Force
    }
}

Write-Output "Unity $version / $Task exit code: $($process.ExitCode)"
Write-Output "Log: $logPath"
if ($process.ExitCode -ne 0)
{
    throw 'Unity command failed. Inspect the log before retrying.'
}

if ($isTestRun)
{
    if (-not (Test-Path -LiteralPath $resultPath))
    {
        throw 'Unity exited without a test report.'
    }

    [xml]$report = Get-Content -LiteralPath $resultPath -Raw
    $run = $report.'test-run'
    if ($run.result -ne 'Passed' -or [int]$run.total -eq 0 -or [int]$run.failed -gt 0)
    {
        throw "Tests did not pass: $($run.result), total=$($run.total), failed=$($run.failed)"
    }

    Write-Output "Tests: $($run.passed)/$($run.total) passed. Report: $resultPath"
}

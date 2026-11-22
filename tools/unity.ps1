[CmdletBinding()]
param(
    [ValidateSet('Initialize', 'PreviewAssets', 'PreviewLayout', 'CaseAssets', 'CaseLayout', 'CaseCompactLayout',
        'CaseCompactPolish',
        'StationAssets', 'StationArt', 'StationCareLayout', 'LoopAssets', 'ResourceAudit',
        'SiegeAssets', 'SiegeLayout', 'SiegeBuild', 'PetMotionAssets', 'PetMotionAudit', 'PetMotionBake',
        'RuntimeMove', 'MainAssets', 'ApproachAssets', 'MapInfoAssets', 'ForestEdgeAssets', 'YardFieldAssets', 'YardFieldRefine', 'LevelUiAssets', 'LevelUiStyle', 'LevelUiRefresh', 'LevelUiFullscreen', 'FieldPropAssets', 'LevelPresentation', 'EditMode', 'PlayMode', 'Build', 'BuildDevelopment')]
    [string]$Task = 'EditMode',
    [string]$EditorPath = $env:UNITY_EDITOR_PATH,
    [string]$TestFilter,
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$ReportSuffix,
    [string]$RunRecordPath,
    [switch]$Isolated,
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'unity\HomelessPetHelper'
$reportPath = Join-Path $repoRoot 'test-results\unity'
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$version = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value

if (-not [string]::IsNullOrWhiteSpace($RunRecordPath))
{
    if (-not $Isolated -or $Task -notin @('EditMode', 'PlayMode'))
    {
        throw 'Structured run records require an isolated test run.'
    }
    if (Test-Path -LiteralPath $RunRecordPath) { throw 'Run record already exists; use a unique path.' }
}

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
if ($Isolated -and $Task -eq 'PlayMode')
{
    $baseline = Join-Path $repoRoot 'test-results\pet-motion\baseline-rig.bundle'
    if (Test-Path -LiteralPath $baseline -PathType Leaf)
    {
        $baselineFolder = Join-Path $projectPath 'PetMotionBaseline'
        New-Item -ItemType Directory -Path $baselineFolder | Out-Null
        Copy-Item -LiteralPath $baseline -Destination (Join-Path $baselineFolder 'pet-motion')
    }
}
$resultPath = Join-Path $reportPath "$reportName.xml"
$unityArguments = @('-batchmode', '-projectPath', "`"$projectPath`"", '-logFile', "`"$logPath`"")
if (-not $Graphics)
{
    $unityArguments += '-nographics'
}

switch ($Task)
{
    'LevelPresentation'
    {
        if (-not $Isolated) { throw 'LevelPresentation requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.LevelUi.LevelPresentationAuthor.Apply')
        break
    }
    'FieldPropAssets'
    {
        if (-not $Isolated) { throw 'FieldPropAssets requires an isolated snapshot.' }
        $sourceFolder = Join-Path $projectPath 'FieldPropSource'
        New-Item -ItemType Directory -Path $sourceFolder | Out-Null
        $manifest = Join-Path $repoRoot 'art-source\field-props-20261122\unity-import.json'
        Copy-Item -LiteralPath $manifest -Destination (Join-Path $sourceFolder 'manifest.json')
        $imports = Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($page in $imports.pages)
        {
            Copy-Item -LiteralPath (Join-Path $repoRoot $page.source) `
                -Destination (Join-Path $sourceFolder (Split-Path -Leaf $page.texture))
        }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Field.FieldPropArtImport.Apply')
    }
    'LevelUiFullscreen'
    {
        if (-not $Isolated) { throw 'LevelUiFullscreen requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.LevelUi.LevelOverlayFullscreen.Apply')
    }
    'LevelUiRefresh'
    {
        if (-not $Isolated) { throw 'LevelUiRefresh requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.LevelUi.LevelOverlayRefresh.Apply')
    }
    'LevelUiStyle'
    {
        if (-not $Isolated) { throw 'LevelUiStyle requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.LevelUi.LevelOverlayStyler.Apply')
    }
    'LevelUiAssets'
    {
        if (-not $Isolated) { throw 'LevelUiAssets requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.LevelUi.LevelOverlayAuthor.Apply')
    }
    'YardFieldRefine'
    {
        if (-not $Isolated) { throw 'YardFieldRefine requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Field.YardFieldAuthor.Refine')
    }
    'YardFieldAssets'
    {
        if (-not $Isolated) { throw 'YardFieldAssets requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Field.YardFieldAuthor.Apply')
    }
    'ForestEdgeAssets'
    {
        if (-not $Isolated) { throw 'ForestEdgeAssets requires an isolated snapshot.' }
        $sourceFolder = Join-Path $projectPath 'ForestEdgeSource'
        New-Item -ItemType Directory -Path $sourceFolder | Out-Null
        $importManifest = Join-Path $repoRoot 'art-source\forest-edge-20261121\unity-import.json'
        Copy-Item -LiteralPath $importManifest -Destination (Join-Path $sourceFolder 'manifest.json')
        $imports = Get-Content -LiteralPath $importManifest -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($item in $imports.assets)
        {
            Copy-Item -LiteralPath (Join-Path $repoRoot $item.source) `
                -Destination (Join-Path $sourceFolder (Split-Path -Leaf $item.target))
        }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.CityMap.ForestEdgeArtImport.Apply')
    }
    'MapInfoAssets'
    {
        if (-not $Isolated) { throw 'MapInfoAssets requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.CityMap.CityMapInfoUpgrade.Apply')
    }
    'ApproachAssets'
    {
        if (-not $Isolated) { throw 'ApproachAssets requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Approach.ApproachPanelAuthor.Apply')
    }
    'RuntimeMove'
    {
        if (-not $Isolated) { throw 'RuntimeMove requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.RuntimeMigration.MoveAssets')
    }
    'MainAssets'
    {
        if (-not $Isolated) { throw 'MainAssets requires an isolated snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.MainSceneAuthor.Apply')
    }
    'PetMotionAssets'
    {
        if (-not $Isolated) { throw 'PetMotionAssets must run in an isolated saved-project snapshot.' }
        $sourceFolder = Join-Path $projectPath 'PetMotionSource'
        New-Item -ItemType Directory -Path $sourceFolder | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot 'art-source\pet-motion\unity-export.json') -Destination $sourceFolder
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.PetMotion.PetMotionAssets.CreateAssets')
    }
    'PetMotionBake'
    {
        if (-not $Isolated) { throw 'PetMotionBake must run in an isolated saved-project snapshot.' }
        $sourceFolder = Join-Path $projectPath 'PetMotionSource'
        New-Item -ItemType Directory -Path $sourceFolder | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot 'art-source\pet-motion\unity-export.json') -Destination $sourceFolder
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.PetMotion.PetMotionAssets.UpdateClips')
    }
    'PetMotionAudit'
    {
        if (-not $Isolated) { throw 'PetMotionAudit must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.PetMotion.PetMotionAudit.WriteReport')
    }
    'SiegeAssets'
    {
        if (-not $Isolated) { throw 'SiegeAssets must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Siege.SiegeTrialAssets.CreateAssets')
    }
    'SiegeBuild'
    {
        if (-not $Isolated) { throw 'SiegeBuild must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Siege.SiegeTrialAssets.BuildDevelopment')
    }
    'SiegeLayout'
    {
        if (-not $Isolated) { throw 'SiegeLayout must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.Siege.SiegeTrialAssets.RefineLayout')
    }
    'LoopAssets'
    {
        if (-not $Isolated) { throw 'LoopAssets must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.StationPreview.Loop.RescueLoopAssets.CreateSavedScene')
    }
    'CaseCompactPolish'
    {
        if (-not $Isolated) { throw 'CaseCompactPolish must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.RescueCase.RescueCompactLayoutUpgrade.RefineSavedPrefab')
    }
    'CaseCompactLayout'
    {
        if (-not $Isolated) { throw 'CaseCompactLayout must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.RescueCase.RescueCompactLayoutUpgrade.UpgradeSavedPrefab')
    }
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
    'StationCareLayout'
    {
        if (-not $Isolated) { throw 'StationCareLayout must run in an isolated saved-project snapshot.' }
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.StationPreview.StationCarePreviewUpgrade.UpgradeSavedPrefab')
    }
    'Build'
    {
        $unityArguments += @('-quit', '-executeMethod', 'HomelessPetHelper.Editor.DesktopBuild.Build')
    }
    'ResourceAudit'
    {
        $unityArguments += @('-quit', '-executeMethod',
            'HomelessPetHelper.Editor.ResourceAudit.ImageResourceAudit.WriteReport')
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

$record = $null
if (-not [string]::IsNullOrWhiteSpace($RunRecordPath))
{
    $inputFiles = @(
        foreach ($folder in @('Assets', 'Packages', 'ProjectSettings'))
        {
            foreach ($inputFile in Get-ChildItem -LiteralPath (Join-Path $projectPath $folder) -Recurse -File -Force)
            {
                [ordered]@{
                    path = $inputFile.FullName.Substring($projectPath.Length + 1).Replace('\', '/')
                    bytes = $inputFile.Length
                    sha256 = (Get-FileHash -LiteralPath $inputFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }
        }
    )
    $record = [ordered]@{
        schemaVersion = 1; task = $Task; status = 'running'
        startedAt = [DateTime]::UtcNow.ToString('o'); finishedAt = $null
        projectPath = $projectPath; isolated = [bool]$Isolated; graphics = [bool]$Graphics
        testFilter = $TestFilter; editorPath = $EditorPath; editorVersion = $version
        editorFileVersion = (Get-Item -LiteralPath $EditorPath).VersionInfo.FileVersion
        editorProductVersion = (Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion
        powershellVersion = $PSVersionTable.PSVersion.ToString()
        reportPath = $resultPath; logPath = $logPath; exitCode = $null
        inputFiles = $inputFiles; tests = $null; reportSha256 = $null; logSha256 = $null
    }
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RunRecordPath -Encoding UTF8
}

try
{
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
        if ($null -ne $record -and $null -ne $process) { $record.exitCode = $process.ExitCode }
    }

    Write-Output "Unity $version / $Task exit code: $($process.ExitCode)"
    Write-Output "Log: $logPath"
    if ($isTestRun -and (Test-Path -LiteralPath $resultPath))
    {
        [xml]$report = Get-Content -LiteralPath $resultPath -Raw
        $run = $report.'test-run'
        if ($null -ne $record)
        {
            $record.tests = [ordered]@{
                result = [string]$run.result; total = [int]$run.total; passed = [int]$run.passed
                failed = [int]$run.failed; skipped = [int]$run.skipped; inconclusive = [int]$run.inconclusive
            }
        }
    }
    if ($process.ExitCode -ne 0)
    {
        throw 'Unity command failed. Inspect the log before retrying.'
    }

    if ($isTestRun)
    {
        if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Unity exited without a test report.' }
        if ($run.result -ne 'Passed' -or [int]$run.total -eq 0 -or [int]$run.failed -gt 0)
        {
            throw "Tests did not pass: $($run.result), total=$($run.total), failed=$($run.failed)"
        }
        Write-Output "Tests: $($run.passed)/$($run.total) passed. Report: $resultPath"
    }
    if ($null -ne $record) { $record.status = 'passed' }
}
finally
{
    if ($null -ne $record)
    {
        if ($record.status -ne 'passed') { $record.status = 'failed' }
        $record.finishedAt = [DateTime]::UtcNow.ToString('o')
        if (Test-Path -LiteralPath $resultPath)
        { $record.reportSha256 = (Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash.ToLowerInvariant() }
        if (Test-Path -LiteralPath $logPath)
        { $record.logSha256 = (Get-FileHash -LiteralPath $logPath -Algorithm SHA256).Hash.ToLowerInvariant() }
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RunRecordPath -Encoding UTF8
    }
}

if ($Task -eq 'ResourceAudit')
{
    foreach ($extension in @('json', 'md'))
    {
        $source = Join-Path $projectPath "Library\ResourceAudit\image-resources.$extension"
        $destination = Join-Path $reportPath "$reportName.$extension"
        Copy-Item -LiteralPath $source -Destination $destination -Force
        Write-Output "Image resource report: $destination"
    }
}

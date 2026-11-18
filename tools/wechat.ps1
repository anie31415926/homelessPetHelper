[CmdletBinding()]
param(
    [ValidateSet('Check', 'Probe')]
    [string]$Task = 'Check',
    [string]$EditorPath = $env:UNITY_EDITOR_PATH,
    [string]$SdkCachePath,
    [switch]$BuildWebGL
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'unity/HomelessPetHelper'
$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
$version = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$sdkRevision = '915d4bf4f60ef0cd1c29316a8d67257185d609a7'
$sdkUrl = "https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git#$sdkRevision"
if ([string]::IsNullOrWhiteSpace($EditorPath))
{
    $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}

if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw 'Unity editor not found.' }
$webglPath = Join-Path (Split-Path -Parent $EditorPath) 'Data/PlaybackEngines/WebGLSupport'
$manifest = Get-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Raw | ConvertFrom-Json
$wechat = -join [char[]]@(0x5FAE, 0x4FE1)
$developerTools = -join [char[]]@(0x5F00, 0x53D1, 0x8005, 0x5DE5, 0x5177)
$devtools = Join-Path ${env:ProgramFiles(x86)} "Tencent/$($wechat)web$developerTools/$wechat$developerTools.exe"
[pscustomobject]@{
    UnityVersion = $version
    EditorPath = $EditorPath
    WebGLInstalled = Test-Path -LiteralPath (Join-Path $webglPath 'BuildTools') -PathType Container
    WeChatDevtoolsInstalled = Test-Path -LiteralPath $devtools -PathType Leaf
    WeChatDevtoolsPath = $devtools
    ProjectSdk = $manifest.dependencies.'com.qq.weixin.minigame'
    AssetBundleModule = $manifest.dependencies.'com.unity.modules.assetbundle'
    ExpectedSdkRevision = $sdkRevision
} | Format-List
if ($Task -eq 'Check') { return }
if (-not (Test-Path -LiteralPath $webglPath -PathType Container)) { throw 'Install WebGL for this editor first.' }

$reportRoot = Join-Path $repo 'test-results/wechat-environment'
$probe = Join-Path $reportRoot "probe-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $probe -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings'))
{
    Copy-Item -LiteralPath (Join-Path $project $folder) -Destination $probe -Recurse
}

# Only the isolated snapshot receives the candidate package and verification script.
$probeManifestPath = Join-Path $probe 'Packages/manifest.json'
$probeManifest = Get-Content -LiteralPath $probeManifestPath -Raw | ConvertFrom-Json
$probeManifest.dependencies | Add-Member -NotePropertyName 'com.qq.weixin.minigame' -NotePropertyValue $sdkUrl -Force
$probeManifest.dependencies | Add-Member -NotePropertyName 'com.unity.modules.assetbundle' -NotePropertyValue '1.0.0' -Force
$encoding = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($probeManifestPath, ($probeManifest | ConvertTo-Json -Depth 20) + "`n", $encoding)
New-Item -ItemType Directory -Path (Join-Path $probe 'Assets/Editor') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wechat/WeChatEnvironmentProbe.cs') -Destination (Join-Path $probe 'Assets/Editor/WeChatEnvironmentProbe.cs')

if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $env:ProgramData }
if ([string]::IsNullOrWhiteSpace($env:PUBLIC)) { $env:PUBLIC = Join-Path $env:SystemDrive 'Users/Public' }
$oldNodeOptions = $env:NODE_OPTIONS
$env:NODE_OPTIONS = $null
$oldGitConfigCount = $env:GIT_CONFIG_COUNT
$configIndex = 0
if (-not [string]::IsNullOrWhiteSpace($oldGitConfigCount)) { $configIndex = [int]$oldGitConfigCount }
if (-not [string]::IsNullOrWhiteSpace($SdkCachePath))
{
    $SdkCachePath = (Resolve-Path -LiteralPath $SdkCachePath).Path
    $revision = & git -C $SdkCachePath rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $revision -ne $sdkRevision) { throw 'SDK cache revision does not match.' }
    $origin = & git -C $SdkCachePath remote get-url origin
    if ($origin -ne 'https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk.git')
    {
        throw 'SDK cache origin does not match the official repository.'
    }
    $changes = & git -C $SdkCachePath status --porcelain
    if ($LASTEXITCODE -ne 0 -or $changes) { throw 'SDK cache must have a clean working tree.' }
    $localUrl = ([Uri]($SdkCachePath.Replace('\', '/') + '/')).AbsoluteUri
    Set-Item -Path "Env:GIT_CONFIG_KEY_$configIndex" -Value "url.$localUrl.insteadOf"
    Set-Item -Path "Env:GIT_CONFIG_VALUE_$configIndex" -Value $origin
    $env:GIT_CONFIG_COUNT = [string]($configIndex + 1)
    Write-Output 'Using verified official SDK checkout to seed the Git package cache; manifest keeps its pinned URL.'
}
$method = if ($BuildWebGL) { 'WeChatEnvironmentProbe.BuildWebGl' } else { 'WeChatEnvironmentProbe.Verify' }
$log = Join-Path $probe 'unity.log'
Write-Output "Isolated WeChat probe: $probe"
try
{
    $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$probe`"",
        '-logFile', "`"$log`"", '-executeMethod', $method)
    if ($BuildWebGL) { $arguments += @('-buildTarget', 'WebGL') }
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(1200000))
    {
        Stop-Process -Id $process.Id -Force
        throw 'Isolated probe timed out after 20 minutes; only its own Unity process was stopped.'
    }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity probe exit $($process.ExitCode). See $log" }
    $installation = Join-Path $probe 'ProbeReports/installation.json'
    if (-not (Test-Path -LiteralPath $installation)) { throw "Missing verification report. See $log" }
    Get-Content -LiteralPath $installation -Raw | Write-Output
    if ($BuildWebGL)
    {
        Get-Content -LiteralPath (Join-Path $probe 'ProbeReports/webgl-build.json') -Raw | Write-Output
    }
    Write-Output "Evidence: $probe"
}
finally
{
    $env:NODE_OPTIONS = $oldNodeOptions
    if (-not [string]::IsNullOrWhiteSpace($SdkCachePath))
    {
        Remove-Item -LiteralPath "Env:GIT_CONFIG_KEY_$configIndex" -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath "Env:GIT_CONFIG_VALUE_$configIndex" -ErrorAction SilentlyContinue
        $env:GIT_CONFIG_COUNT = $oldGitConfigCount
    }
}

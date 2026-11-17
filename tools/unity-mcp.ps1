[CmdletBinding()]
param(
    [ValidateSet('Start', 'Status')]
    [string]$Task = 'Status'
)

$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:8080'
$serverVersion = '10.0.0'
$repoRoot = Split-Path -Parent $PSScriptRoot
$reportPath = Join-Path $repoRoot 'test-results\unity-mcp'

function Get-ServerHealth
{
    try
    {
        return Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 3
    }
    catch
    {
        return $null
    }
}

$health = Get-ServerHealth
if ($Task -eq 'Start' -and $null -eq $health)
{
    $listeners = @(Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0)
    {
        throw 'Port 8080 belongs to another service. Nothing was stopped or replaced.'
    }

    $uvx = Get-Command uvx -ErrorAction Stop
    New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
    $env:UNITY_MCP_DISABLE_TELEMETRY = '1'
    $env:MCP_DISABLE_TELEMETRY = '1'
    $serverArguments = @(
        '--from', "mcpforunityserver==$serverVersion", 'mcp-for-unity',
        '--transport', 'http', '--http-host', '127.0.0.1', '--http-port', '8080',
        '--default-instance', 'HomelessPetHelper', '--project-scoped-tools'
    )
    $process = Start-Process -FilePath $uvx.Source -ArgumentList $serverArguments -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $reportPath 'server.stdout.log') `
        -RedirectStandardError (Join-Path $reportPath 'server.stderr.log') -PassThru

    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do
    {
        $health = Get-ServerHealth
        if ($null -ne $health)
        {
            break
        }

        $process.Refresh()
        if ($process.HasExited)
        {
            throw "MCP server exited. Inspect logs in $reportPath."
        }

        Start-Sleep -Milliseconds 500
    }
    while ([DateTime]::UtcNow -lt $deadline)
}

if ($null -eq $health)
{
    throw 'MCP server is offline. Run tools/unity-mcp.ps1 -Task Start.'
}

if ($health.status -ne 'healthy' -or $health.version -ne $serverVersion `
    -or $health.message -ne 'MCP for Unity server is running')
{
    throw 'Unexpected service or version on port 8080. Nothing was stopped or replaced.'
}

$sessions = Invoke-RestMethod -Uri "$baseUrl/api/instances" -TimeoutSec 10
if (-not $sessions.success)
{
    throw 'MCP server could not list Unity sessions.'
}

$projectInstances = @($sessions.instances | Where-Object { $_.project -eq 'HomelessPetHelper' })
[ordered]@{
    endpoint = "$baseUrl/mcp"
    serverVersion = $health.version
    project = 'HomelessPetHelper'
    editorConnected = $projectInstances.Count -eq 1
    instances = $projectInstances
} | ConvertTo-Json -Depth 6

if ($projectInstances.Count -ne 1)
{
    Write-Warning 'Expected one HomelessPetHelper session. In Unity, open Window > MCP for Unity and click Connect.'
    if ($Task -eq 'Status')
    {
        exit 1
    }
}

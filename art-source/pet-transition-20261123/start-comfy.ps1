param(
    [int]$Port = 8194,
    [string]$Backend = $env:COMFY_HOME,
    [string]$ModelPathsConfig = $env:COMFY_MODEL_PATHS
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Backend) -or -not (Test-Path -LiteralPath (Join-Path $Backend 'main.py') -PathType Leaf)) {
    throw 'Set -Backend or COMFY_HOME to an existing ComfyUI folder.'
}
$python = Join-Path $backend '.venv\Scripts\python.exe'
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { throw 'Backend .venv Python was not found.' }
if ($ModelPathsConfig -and -not (Test-Path -LiteralPath $ModelPathsConfig -PathType Leaf)) { throw 'Model paths config was not found.' }
if ($Port -lt 1024 -or $Port -gt 65535) { throw 'Invalid local port.' }
if (@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Port in use; do not replace an existing service.' }
$serverRoot = Join-Path $PSScriptRoot 'server'
foreach ($name in @('input', 'output', 'user', 'temp')) { New-Item -ItemType Directory -Path (Join-Path $serverRoot $name) -Force | Out-Null }
$arguments = @('-u', 'main.py', '--listen', '127.0.0.1', '--port', $Port, '--disable-auto-launch', '--disable-all-custom-nodes', '--disable-api-nodes',
    '--input-directory', ('"' + (Join-Path $serverRoot 'input') + '"'),
    '--output-directory', ('"' + (Join-Path $serverRoot 'output') + '"'),
    '--user-directory', ('"' + (Join-Path $serverRoot 'user') + '"'),
    '--temp-directory', ('"' + (Join-Path $serverRoot 'temp') + '"'))
if ($ModelPathsConfig) { $arguments += @('--extra-model-paths-config', ('"' + $ModelPathsConfig + '"')) }
$process = Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $backend -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $serverRoot 'stdout.log') -RedirectStandardError (Join-Path $serverRoot 'stderr.log')
@{ processId = $process.Id; port = $Port; executable = $python; startedAt = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $serverRoot 'owned-process.json') -Encoding utf8
Write-Output ('Started owned local ComfyUI PID ' + $process.Id + ' on port ' + $Port)

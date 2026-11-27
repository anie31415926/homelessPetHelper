$ErrorActionPreference = 'Stop'
$python = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI\.venv\Scripts\python.exe'
$backend = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI'
$server = Join-Path $PSScriptRoot 'server'
if (Get-NetTCPConnection -LocalPort 8193 -State Listen -ErrorAction SilentlyContinue)
{
    throw 'Port 8193 is occupied. Do not replace another process.'
}
foreach ($folder in @('input', 'output', 'user', 'temp'))
{
    New-Item -ItemType Directory -Path (Join-Path $server $folder) -Force | Out-Null
}
$arguments = @('-u', 'main.py', '--listen', '127.0.0.1', '--port', '8193',
    '--disable-auto-launch', '--disable-all-custom-nodes', '--disable-api-nodes',
    '--extra-model-paths-config', '"C:/Users/Admin/AppData/Roaming/Comfy Desktop/shared_model_paths.yaml"',
    '--input-directory', "`"$(Join-Path $server 'input')`"",
    '--output-directory', "`"$(Join-Path $server 'output')`"",
    '--user-directory', "`"$(Join-Path $server 'user')`"",
    '--temp-directory', "`"$(Join-Path $server 'temp')`""
)
$process = Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $backend `
    -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $PSScriptRoot 'backend.stdout.log') `
    -RedirectStandardError (Join-Path $PSScriptRoot 'backend.stderr.log')
@{ processId = $process.Id; executable = $python; port = 8193; startedAt = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'owned-process.json')
Write-Output "Owned station ComfyUI PID: $($process.Id), port 8193"

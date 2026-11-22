$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$python = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI\.venv\Scripts\python.exe'
$backend = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI'
$listeners = @(Get-NetTCPConnection -LocalPort 8188 -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -gt 0) { throw 'Port 8188 already in use; do not replace another service.' }
$args = @('-u', 'main.py', '--listen', '127.0.0.1', '--port', '8188', '--disable-auto-launch',
    '--extra-model-paths-config', '"C:/Users/Admin/AppData/Roaming/Comfy Desktop/shared_model_paths.yaml"',
    '--input-directory', 'C:/Users/Admin/ComfyUI-Shared/input',
    '--output-directory', 'C:/Users/Admin/ComfyUI-Shared/output')
$process = Start-Process -FilePath $python -ArgumentList $args -WorkingDirectory $backend -PassThru -WindowStyle Hidden `
    -RedirectStandardOutput (Join-Path $taskRoot 'backend.stdout.log') `
    -RedirectStandardError (Join-Path $taskRoot 'backend.stderr.log')
@{ processId = $process.Id; startedAt = [DateTime]::UtcNow.ToString('o'); executable = $python } |
    ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $taskRoot 'owned-process.json')
$process.Id

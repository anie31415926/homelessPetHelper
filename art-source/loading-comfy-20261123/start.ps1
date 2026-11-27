$ErrorActionPreference = 'Stop'
$port = 8195
$backend = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI'
$python = Join-Path $backend '.venv\Scripts\python.exe'
if (@(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).Count) {
    throw 'Port already in use; do not replace an existing service.'
}
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../test-results/loading-comfy/server'))
foreach ($name in @('input','output','user','temp')) {
    New-Item -ItemType Directory -Path (Join-Path $root $name) -Force | Out-Null
}
$arguments = @('-u','main.py','--listen','127.0.0.1','--port',"$port",'--disable-auto-launch',
    '--disable-all-custom-nodes','--disable-api-nodes','--extra-model-paths-config',
    '"C:/Users/Admin/AppData/Roaming/Comfy Desktop/shared_model_paths.yaml"',
    '--input-directory', ('"' + (Join-Path $root 'input') + '"'),
    '--output-directory', ('"' + (Join-Path $root 'output') + '"'),
    '--user-directory', ('"' + (Join-Path $root 'user') + '"'),
    '--temp-directory', ('"' + (Join-Path $root 'temp') + '"'))
$process = Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $backend -PassThru `
    -WindowStyle Hidden -RedirectStandardOutput (Join-Path $root 'stdout.log') `
    -RedirectStandardError (Join-Path $root 'stderr.log')
@{ processId=$process.Id; port=$port; executable=$python; startedAt=[DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'owned-process.json') -Encoding UTF8
Write-Output "Started owned ComfyUI PID $($process.Id) on $port"

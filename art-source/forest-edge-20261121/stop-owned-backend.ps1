$ErrorActionPreference = 'Stop'
$comfyBackendRoot = Get-CimInstance Win32_Process -Filter 'ProcessId=97964'
$comfyBackendChild = Get-CimInstance Win32_Process -Filter 'ProcessId=26272'
$expectedExecutable = 'C:\Users\Admin\ComfyUI-Installs\ComfyUI\ComfyUI\.venv\Scripts\python.exe'
if ($comfyBackendRoot.ExecutablePath -ne $expectedExecutable -or $comfyBackendRoot.CommandLine -notlike '*-u main.py --listen 127.0.0.1 --port 8188*') { throw 'Owned process identity changed' }
if ($comfyBackendChild.ParentProcessId -ne $comfyBackendRoot.ProcessId -or $comfyBackendChild.CommandLine -ne $comfyBackendRoot.CommandLine) { throw 'Owned child identity changed' }
Stop-Process -Id $comfyBackendChild.ProcessId
Stop-Process -Id $comfyBackendRoot.ProcessId -ErrorAction SilentlyContinue
Write-Output 'Stopped only the backend and launcher created by this image task: 26272 / 97964.'

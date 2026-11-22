$ErrorActionPreference = 'Stop'
$record = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'owned-process.json') -Raw | ConvertFrom-Json
$owned = Get-CimInstance Win32_Process -Filter ('ProcessId=' + $record.processId)
if ($null -eq $owned) { Write-Output 'Owned backend already stopped.'; exit 0 }
if ($owned.ExecutablePath -ne $record.executable -or $owned.CommandLine -notlike '*-u main.py --listen 127.0.0.1 --port 8188*') {
    throw 'Process identity differs from the task record; refusing to stop.'
}
if ((Get-Process -Id $owned.ProcessId).StartTime.ToUniversalTime() -gt ([DateTime]$record.startedAt).ToUniversalTime()) {
    throw 'PID was reused; refusing to stop.'
}
$queue = Invoke-RestMethod -Uri 'http://127.0.0.1:8188/queue' -TimeoutSec 15
if (@($queue.queue_running).Count -gt 0 -or @($queue.queue_pending).Count -gt 0) {
    throw 'ComfyUI queue is not empty; leave the backend running.'
}
$directChildren = @(Get-CimInstance Win32_Process -Filter ('ParentProcessId=' + $owned.ProcessId))
foreach ($child in $directChildren) {
    if ($child.ExecutablePath -eq 'C:\Windows\system32\conhost.exe') { continue }
    if ($child.CommandLine -ne $owned.CommandLine -or $child.ExecutablePath -notlike '*\python.exe') {
        throw 'Unexpected child process; refusing to stop the backend.'
    }
}
$children = @($directChildren | Where-Object { $_.ExecutablePath -like '*\python.exe' })
$allowed = @($owned.ProcessId) + @($children | ForEach-Object { $_.ProcessId })
$listeners = @(Get-NetTCPConnection -LocalPort 8188 -State Listen -ErrorAction Stop)
foreach ($listener in $listeners) {
    if ($listener.OwningProcess -notin $allowed) { throw 'Listener is not task-owned; refusing to stop.' }
}
foreach ($child in $children) { Stop-Process -Id $child.ProcessId -ErrorAction Stop }
Stop-Process -Id $owned.ProcessId -ErrorAction SilentlyContinue
@{ stoppedAt = [DateTime]::UtcNow.ToString('o'); processes = $allowed; queueWasEmpty = $true } |
    ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'backend-stop.json')
Write-Output ('Stopped only this task backend: ' + ($allowed -join ', '))

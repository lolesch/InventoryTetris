# Runs a program and waits for it, without letting it take keyboard focus from the foreground app.
#
#   powershell -NoProfile -File dev/launch-detached.ps1 <exe> [args...]
#
# Windows lets a process claim the foreground when its parent is the foreground app. Unity.exe does it
# even with -batchmode -nographics (an untitled window holds focus for the whole boot), which steals
# keystrokes mid-sentence. Starting it through WMI makes WmiPrvSE its parent, so it never qualifies.
# -WindowStyle Hidden does not help. Prints nothing and does not forward the exit code; run-tests.sh
# reads the results file instead.
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Exe,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)

function Quote([string]$s) { if ($s -match '[\s"]') { '"' + ($s -replace '"', '\"') + '"' } else { $s } }

$commandLine = (@($Exe) + $Rest | ForEach-Object { Quote $_ }) -join ' '
$created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $commandLine }
if ($created.ReturnValue -ne 0) { exit 1 }

$process = Get-Process -Id $created.ProcessId -ErrorAction SilentlyContinue
if ($process) { $process.WaitForExit() }

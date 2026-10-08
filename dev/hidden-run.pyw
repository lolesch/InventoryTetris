# Runs a command with no console window and records its output and exit status in files.
#
#   pythonw dev/hidden-run.pyw <output-file> <status-file> <command> [args...]
#
# .pyw runs under pythonw.exe (GUI subsystem), so starting this does not flash a window. The command
# gets a hidden console that its own children inherit; without one, every console program (robocopy,
# git, python, ...) started from a console-less parent opens a visible window and steals keyboard focus.
import subprocess
import sys

output_file, status_file, *command = sys.argv[1:]

with open(output_file, "wb") as out:
    status = subprocess.run(
        command,
        stdin=subprocess.DEVNULL,
        stdout=out,
        stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW,
    ).returncode

with open(status_file, "w") as f:
    f.write(str(status))

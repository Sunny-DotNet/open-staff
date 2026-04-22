---
name: run-powershell
description: Execute arbitrary PowerShell commands and return structured stdout, stderr, exit code, and duration details.
compatibility: Requires pwsh on PATH. This skill executes unrestricted PowerShell commands.
license: MIT
---

# Run PowerShell commands

Use this skill when you need direct shell access through PowerShell.

The skill exposes the script below:

- `scripts/run-command.ps1`

Pass a JSON object with these fields:

- `command` (string, required): the exact PowerShell command to execute
- `workingDirectory` (string, optional): directory to run the command in
- `timeoutSeconds` (number, optional): kill the child command after this timeout; omit for no timeout

The script returns JSON with these fields:

- `command`
- `workingDirectory`
- `timeoutSeconds`
- `exitCode`
- `timedOut`
- `stdout`
- `stderr`
- `durationMs`
- `hostError`

## Notes

- The command runs through `pwsh -EncodedCommand`, so pipelines, multiline commands, and advanced PowerShell syntax are supported.
- This skill has no allowlist or sandbox. Use it only when direct shell execution is appropriate.
- When file paths matter, prefer absolute paths or pass `workingDirectory` explicitly.

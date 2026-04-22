param(
    [string]$ArgumentsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$result = [ordered]@{
    command = $null
    workingDirectory = $null
    timeoutSeconds = $null
    exitCode = $null
    timedOut = $false
    stdout = ''
    stderr = ''
    durationMs = 0
    hostError = $null
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$tempRoot = $null

try {
    $payload = @{}
    if (-not [string]::IsNullOrWhiteSpace($ArgumentsJson)) {
        $payload = $ArgumentsJson | ConvertFrom-Json -AsHashtable -Depth 64
    }

    $command = ''
    if ($payload.ContainsKey('command') -and $null -ne $payload['command']) {
        $command = [string]$payload['command']
    }

    if ([string]::IsNullOrWhiteSpace($command)) {
        throw 'command is required.'
    }

    $result.command = $command

    $workingDirectory = (Get-Location).Path
    if ($payload.ContainsKey('workingDirectory') -and -not [string]::IsNullOrWhiteSpace([string]$payload['workingDirectory'])) {
        $workingDirectory = [string]$payload['workingDirectory']
    }

    $workingDirectory = [System.IO.Path]::GetFullPath($workingDirectory)
    if (-not [System.IO.Directory]::Exists($workingDirectory)) {
        throw "workingDirectory '$workingDirectory' does not exist."
    }

    $result.workingDirectory = $workingDirectory

    $timeoutSeconds = $null
    if ($payload.ContainsKey('timeoutSeconds') -and $null -ne $payload['timeoutSeconds'] -and -not [string]::IsNullOrWhiteSpace([string]$payload['timeoutSeconds'])) {
        $timeoutSeconds = [int]$payload['timeoutSeconds']
        if ($timeoutSeconds -le 0) {
            throw 'timeoutSeconds must be greater than 0 when provided.'
        }
    }

    $result.timeoutSeconds = $timeoutSeconds

    $encodedCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('run-powershell-skill-' + [guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null

    $stdoutFile = Join-Path $tempRoot 'stdout.txt'
    $stderrFile = Join-Path $tempRoot 'stderr.txt'
    $argumentList = @(
        '-NoLogo',
        '-NoProfile',
        '-NonInteractive',
        '-EncodedCommand',
        $encodedCommand
    )

    $process = Start-Process `
        -FilePath 'pwsh' `
        -ArgumentList $argumentList `
        -WorkingDirectory $workingDirectory `
        -RedirectStandardOutput $stdoutFile `
        -RedirectStandardError $stderrFile `
        -NoNewWindow `
        -PassThru

    try {
        if ($null -ne $timeoutSeconds) {
            if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
                $result.timedOut = $true
                try {
                    $process.Kill($true)
                }
                catch {
                }
                $process.WaitForExit()
            }
        }
        else {
            $process.WaitForExit()
        }

        $result.exitCode = if ($result.timedOut) { 124 } else { $process.ExitCode }
    }
    finally {
        $process.Dispose()
    }

    if (Test-Path -LiteralPath $stdoutFile) {
        $result.stdout = [System.IO.File]::ReadAllText($stdoutFile)
    }

    if (Test-Path -LiteralPath $stderrFile) {
        $result.stderr = [System.IO.File]::ReadAllText($stderrFile)
    }
}
catch {
    $result.hostError = $_.Exception.Message
}
finally {
    $stopwatch.Stop()
    $result.durationMs = [int][Math]::Round($stopwatch.Elapsed.TotalMilliseconds)

    if ($null -ne $tempRoot -and [System.IO.Directory]::Exists($tempRoot)) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    $result | ConvertTo-Json -Depth 16 -Compress
}

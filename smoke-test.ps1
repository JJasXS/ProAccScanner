param(
    [string]$BaseUrl = 'http://localhost:4883',
    [int]$StartupTimeoutSeconds = 30,
    [switch]$AutoStart
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$runDevScript = Join-Path $projectRoot 'run-dev.ps1'
$startedProcess = $null

function Write-Pass([string]$Message) {
    Write-Host "PASS: $Message" -ForegroundColor Green
}

function Write-Fail([string]$Message) {
    Write-Host "FAIL: $Message" -ForegroundColor Red
}

function Invoke-Endpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSec = 10
    )

    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        return [PSCustomObject]@{
            Ok = $true
            StatusCode = [int]$resp.StatusCode
            FinalUrl = $resp.BaseResponse.ResponseUri.AbsoluteUri
            ErrorMessage = $null
        }
    }
    catch {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            $finalUrl = $null
            try { $finalUrl = $_.Exception.Response.ResponseUri.AbsoluteUri } catch { }
            return [PSCustomObject]@{
                Ok = $false
                StatusCode = $status
                FinalUrl = $finalUrl
                ErrorMessage = $_.Exception.Message
            }
        }

        return [PSCustomObject]@{
            Ok = $false
            StatusCode = -1
            FinalUrl = $null
            ErrorMessage = $_.Exception.Message
        }
    }
}

function Wait-For-App {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSec = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)

    while ((Get-Date) -lt $deadline) {
        $result = Invoke-Endpoint -Url $Url -TimeoutSec 3
        if ($result.StatusCode -eq 200 -or $result.StatusCode -eq 302 -or $result.StatusCode -eq 301) {
            return $true
        }
    }

    return $false
}

$base = $BaseUrl.TrimEnd('/')
$rootUrl = "$base/"
$activateUrl = "$base/Activate"
$apiTestUrl = "$base/api/test"

$initialRoot = Invoke-Endpoint -Url $rootUrl -TimeoutSec 5
if (($initialRoot.StatusCode -lt 200 -or $initialRoot.StatusCode -ge 500) -and $AutoStart) {
    if (-not (Test-Path $runDevScript)) {
        Write-Fail "Cannot auto-start app because run-dev.ps1 was not found."
        exit 1
    }

    Write-Host "App seems down. Auto-starting with run-dev.ps1..." -ForegroundColor Yellow
    $startedProcess = Start-Process -FilePath 'powershell' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $runDevScript) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru

    if (-not (Wait-For-App -Url $rootUrl -TimeoutSec $StartupTimeoutSeconds)) {
        Write-Fail "App did not become ready within $StartupTimeoutSeconds seconds."
        exit 1
    }
}

$failures = 0

# Test 1: root should be reachable and typically redirect to /Activate before activation.
$root = Invoke-Endpoint -Url $rootUrl
if ($root.StatusCode -eq 200) {
    Write-Pass "GET / returned 200. Final URL: $($root.FinalUrl)"
}
else {
    Write-Fail "GET / expected 200 but got $($root.StatusCode). $($root.ErrorMessage)"
    $failures++
}

# Test 2: activation page should be reachable.
$activate = Invoke-Endpoint -Url $activateUrl
if ($activate.StatusCode -eq 200) {
    Write-Pass "GET /Activate returned 200."
}
else {
    Write-Fail "GET /Activate expected 200 but got $($activate.StatusCode). $($activate.ErrorMessage)"
    $failures++
}

# Test 3: API is intentionally blocked until activation.
$api = Invoke-Endpoint -Url $apiTestUrl
if ($api.StatusCode -eq 403) {
    Write-Pass "GET /api/test returned 403 before activation (expected)."
}
else {
    Write-Fail "GET /api/test expected 403 before activation, got $($api.StatusCode)."
    $failures++
}

if ($failures -eq 0) {
    Write-Host "`nSmoke test result: PASS" -ForegroundColor Green
    exit 0
}

Write-Host "`nSmoke test result: FAIL ($failures check(s) failed)" -ForegroundColor Red
exit 1

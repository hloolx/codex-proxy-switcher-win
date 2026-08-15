param(
    [ValidateSet("Native", "Vpn")]
    [string]$Mode = "Vpn",

    [string]$ProxyUrl = "",

    [string]$CodexExePath = "",

    [string]$NoProxy = "localhost,127.0.0.1,::1",

    [switch]$KeepExisting
)

$ErrorActionPreference = "Stop"

function Get-ConfiguredProxyUrl {
    if (-not [string]::IsNullOrWhiteSpace($ProxyUrl)) {
        return $ProxyUrl.Trim()
    }

    $proxyFile = Join-Path $PSScriptRoot "proxy-url.txt"
    if (Test-Path -LiteralPath $proxyFile) {
        $configured = Get-Content -LiteralPath $proxyFile -ErrorAction Stop |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1

        if (-not [string]::IsNullOrWhiteSpace($configured)) {
            return $configured.Trim()
        }
    }

    return "http://127.0.0.1:7897"
}

function Get-ConfiguredCodexExePath {
    if (-not [string]::IsNullOrWhiteSpace($CodexExePath)) {
        return $CodexExePath.Trim().Trim('"')
    }

    $codexPathFile = Join-Path $PSScriptRoot "codex-path.txt"
    if (Test-Path -LiteralPath $codexPathFile) {
        $configured = Get-Content -LiteralPath $codexPathFile -ErrorAction Stop |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1

        if (-not [string]::IsNullOrWhiteSpace($configured)) {
            return $configured.Trim().Trim('"')
        }
    }

    return ""
}

function Get-CodexExePath {
    $configuredCodexExePath = Get-ConfiguredCodexExePath
    if (-not [string]::IsNullOrWhiteSpace($configuredCodexExePath)) {
        if (-not (Test-Path -LiteralPath $configuredCodexExePath -PathType Leaf)) {
            throw "Configured Codex.exe path does not exist: $configuredCodexExePath"
        }

        if ([System.IO.Path]::GetFileName($configuredCodexExePath) -ine "Codex.exe") {
            throw "Configured Codex path must point to Codex.exe: $configuredCodexExePath"
        }

        return $configuredCodexExePath
    }

    $appxPackage = Get-AppxPackage -Name "OpenAI.Codex" -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if ($appxPackage) {
        $appxExePath = Join-Path $appxPackage.InstallLocation "app\Codex.exe"
        if (Test-Path -LiteralPath $appxExePath) {
            return $appxExePath
        }
    }

    $basePath = "C:\Program Files\WindowsApps"
    $packagePattern = "OpenAI.Codex_*_x64__2p2nqsd0c76g0"

    $package = Get-ChildItem -Path $basePath -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $packagePattern } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($package) {
        $windowsAppsExePath = Join-Path $package.FullName "app\Codex.exe"
        if (Test-Path -LiteralPath $windowsAppsExePath) {
            return $windowsAppsExePath
        }
    }

    throw "Codex Desktop install directory was not found. Please confirm the Windows Store Codex app is installed."
}

function Stop-ExistingCodex {
    Get-Process -Name "Codex" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    Start-Sleep -Milliseconds 500
}

function Start-CodexProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExePath,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Native", "Vpn")]
        [string]$LaunchMode
    )

    $processInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $ExePath
    $processInfo.WorkingDirectory = Split-Path -Parent $ExePath
    $processInfo.UseShellExecute = $false

    $proxyKeys = @(
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY",
        "http_proxy", "https_proxy", "all_proxy",
        "NO_PROXY", "no_proxy"
    )

    foreach ($key in $proxyKeys) {
        if ($processInfo.EnvironmentVariables.ContainsKey($key)) {
            $processInfo.EnvironmentVariables.Remove($key)
        }
    }

    if ($LaunchMode -eq "Vpn") {
        $configuredProxy = Get-ConfiguredProxyUrl
        $processInfo.EnvironmentVariables["HTTP_PROXY"] = $configuredProxy
        $processInfo.EnvironmentVariables["HTTPS_PROXY"] = $configuredProxy
        $processInfo.EnvironmentVariables["ALL_PROXY"] = $configuredProxy
        $processInfo.EnvironmentVariables["http_proxy"] = $configuredProxy
        $processInfo.EnvironmentVariables["https_proxy"] = $configuredProxy
        $processInfo.EnvironmentVariables["all_proxy"] = $configuredProxy
        $processInfo.EnvironmentVariables["NO_PROXY"] = $NoProxy
        $processInfo.EnvironmentVariables["no_proxy"] = $NoProxy

        Write-Host "Starting Codex with proxy: $configuredProxy"
    }
    else {
        Write-Host "Starting Codex without proxy environment variables."
    }

    [void][System.Diagnostics.Process]::Start($processInfo)
}

$codexExePath = Get-CodexExePath

if (-not $KeepExisting) {
    Stop-ExistingCodex
}

Write-Host "Codex path: $codexExePath"
Start-CodexProcess -ExePath $codexExePath -LaunchMode $Mode
Write-Host "Started Codex. Mode: $Mode"

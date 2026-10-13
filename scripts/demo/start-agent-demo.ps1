# Agent 最小演示：检查端口并（可选）拉起 Host + Agent
# Console 请另开窗口：cd client; pnpm dev
# 说明见 docs/agent-demo.md

param(
    [switch]$StartBackend,
    [switch]$SkipBrowser
)

$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "../..")

function Test-Port([int]$Port) {
    try {
        $c = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        return $null -ne $c
    } catch {
        return $false
    }
}

Write-Host ""
Write-Host "=== XW Agent 演示（简单三进程）===" -ForegroundColor Cyan
Write-Host "仓库: $root"
Write-Host ""
Write-Host "目标: 自然语言查库存 / 问售后；Console 右下角悬浮窗。"
Write-Host "文档: docs/agent-demo.md"
Write-Host ""

$hostUp = Test-Port 5295
$agentUp = Test-Port 5002
$consoleUp = Test-Port 3000

Write-Host ("Host  :5295  -> {0}" -f ($(if ($hostUp) { "已在监听" } else { "未启动" })))
Write-Host ("Agent :5002  -> {0}" -f ($(if ($agentUp) { "已在监听" } else { "未启动" })))
Write-Host ("Console:3000 -> {0}" -f ($(if ($consoleUp) { "已在监听" } else { "未启动" })))
Write-Host ""

if (-not $StartBackend) {
    Write-Host "手动启动（推荐复制到三个终端）:" -ForegroundColor Yellow
    Write-Host "  1) dotnet run --project src/XW.Host.API --launch-profile http"
    Write-Host "  2) dotnet run --project src/Services/XW.Agent.Host --launch-profile http"
    Write-Host "  3) cd client; pnpm dev"
    Write-Host ""
    Write-Host "若要本脚本自动打开 Host+Agent 窗口，加参数: -StartBackend"
    Write-Host ""
    Write-Host "演示话术:"
    Write-Host "  - 设备进水怎么办？"
    Write-Host "  - 查一下 NX100 还有多少库存"
    exit 0
}

if (-not $hostUp) {
    Write-Host "启动 Host (新窗口)..." -ForegroundColor Green
    Start-Process powershell -WorkingDirectory $root.Path -ArgumentList @(
        "-NoExit", "-Command",
        "dotnet run --project src/XW.Host.API --launch-profile http"
    )
} else {
    Write-Host "Host 已在运行，跳过。" -ForegroundColor DarkGray
}

if (-not $agentUp) {
    Write-Host "启动 Agent (新窗口)..." -ForegroundColor Green
    Start-Process powershell -WorkingDirectory $root.Path -ArgumentList @(
        "-NoExit", "-Command",
        "dotnet run --project src/Services/XW.Agent.Host --launch-profile http"
    )
} else {
    Write-Host "Agent 已在运行，跳过。" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "请另开终端启动 Console:" -ForegroundColor Yellow
Write-Host "  cd client; pnpm dev"
Write-Host ""

if (-not $SkipBrowser) {
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:5002/health"
    Start-Process "http://localhost:3000"
}

Write-Host "演示话术:「设备进水怎么办？」→「查一下 NX100 还有多少库存」"
Write-Host "完成。"

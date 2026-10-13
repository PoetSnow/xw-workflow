# 一键补齐：RabbitMQ + Postgres（MySQL/Redis 已有则不要动）
# 用法（仓库根目录）：
#   powershell -File deploy/start-infra.ps1
#   powershell -File deploy/start-infra.ps1 -Down
#   powershell -File deploy/start-infra.ps1 -Status

param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Logs
)

$ErrorActionPreference = "Stop"
$deployDir = $PSScriptRoot
$composeFile = Join-Path $deployDir "docker-compose.infra.yml"
$envExample = Join-Path $deployDir ".env.infra.example"
$envFile = Join-Path $deployDir ".env.infra"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "未找到 docker，请先安装 Docker Desktop。" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $envFile)) {
    Copy-Item $envExample $envFile
    Write-Host "已生成 deploy/.env.infra" -ForegroundColor Yellow
}

$composeArgs = @(
    "compose",
    "-f", $composeFile,
    "--env-file", $envFile
)

Push-Location $deployDir
try {
    if ($Down) {
        Write-Host "停止 RabbitMQ + Postgres ..." -ForegroundColor Cyan
        & docker @composeArgs down
        exit $LASTEXITCODE
    }

    if ($Status) {
        & docker @composeArgs ps
        exit $LASTEXITCODE
    }

    if ($Logs) {
        & docker @composeArgs logs -f --tail 80
        exit $LASTEXITCODE
    }

    Write-Host "启动 RabbitMQ + Postgres(pgvector) ..." -ForegroundColor Cyan
    Write-Host "（不会启动 MySQL / Redis — 请继续用你服务器已有的）" -ForegroundColor DarkGray
    & docker @composeArgs up -d --pull missing
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "等待健康检查（最多约 90 秒）..." -ForegroundColor Cyan
    $deadline = (Get-Date).AddSeconds(90)
    $names = @("xw-demo-rabbit", "xw-demo-pg")
    do {
        Start-Sleep -Seconds 5
        $allHealthy = $true
        foreach ($n in $names) {
            $h = docker inspect -f "{{.State.Health.Status}}" $n 2>$null
            if ($h -ne "healthy") {
                $allHealthy = $false
                Write-Host ("  {0}: {1}" -f $n, ($(if ($h) { $h } else { "starting" })))
            }
        }
        if ($allHealthy) { break }
    } while ((Get-Date) -lt $deadline)

    Write-Host ""
    & docker @composeArgs ps
    Write-Host ""

    $vars = @{}
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -match '^\s*$') { return }
        $p = $_.Split('=', 2)
        if ($p.Length -eq 2) { $vars[$p[0].Trim()] = $p[1].Trim() }
    }
    $rabbitUser = $vars["DEMO_RABBIT_USER"]
    $rabbitPwd = $vars["DEMO_RABBIT_PASSWORD"]
    $pgUser = $vars["DEMO_PG_USER"]
    $pgPwd = $vars["DEMO_PG_PASSWORD"]

    Write-Host "=== 已启动（仅补缺）===" -ForegroundColor Green
    Write-Host "RabbitMQ:  127.0.0.1:5672  用户=$rabbitUser  密码=$rabbitPwd"
    Write-Host "管理台:    http://127.0.0.1:15672"
    Write-Host "Postgres:  Host=127.0.0.1;Port=5432;Database=xw_agent;Username=$pgUser;Password=$pgPwd"
    Write-Host ""
    Write-Host "MySQL / Redis：继续用你现有服务，把 Host 连接串指过去即可。"
    Write-Host "应用栈：docker compose -f docker-compose.apps.yml --env-file .env.apps up -d --build"
    Write-Host "停止补缺：powershell -File deploy/start-infra.ps1 -Down"
} finally {
    Pop-Location
}

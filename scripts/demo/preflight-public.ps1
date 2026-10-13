# 外网演示预检：基础件 / Host / Agent / LLM 必须先绿
# 示例：
#   powershell -File scripts/demo/preflight-public.ps1 -HostBase http://127.0.0.1:5295 -AgentBase http://127.0.0.1:5002
# Docker Host 常用：
#   powershell -File scripts/demo/preflight-public.ps1 -HostBase http://127.0.0.1:8080 -AgentBase http://127.0.0.1:5002

param(
    [string]$HostBase = "http://127.0.0.1:5295",
    [string]$AgentBase = "http://127.0.0.1:5002",
    [string]$MysqlHost = "127.0.0.1",
    [int]$MysqlPort = 3306,
    [string]$RedisHost = "127.0.0.1",
    [int]$RedisPort = 6379,
    [string]$RabbitHost = "127.0.0.1",
    [int]$RabbitPort = 5672,
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [switch]$SkipPg,
    [switch]$SkipTcp
)

$ErrorActionPreference = "Continue"
$failed = 0

function Write-Pass($name, $detail) {
    Write-Host ("[PASS] {0} — {1}" -f $name, $detail) -ForegroundColor Green
}

function Write-Fail($name, $detail) {
    Write-Host ("[FAIL] {0} — {1}" -f $name, $detail) -ForegroundColor Red
    $script:failed++
}

function Test-Tcp([string]$Name, [string]$TargetHost, [int]$Port) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $iar = $client.BeginConnect($TargetHost, $Port, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(3000, $false)
        if (-not $ok) {
            $client.Close()
            Write-Fail $Name "${TargetHost}:${Port} 超时"
            return
        }
        $client.EndConnect($iar)
        $client.Close()
        Write-Pass $Name "${TargetHost}:${Port} 可连"
    } catch {
        Write-Fail $Name $_.Exception.Message
    }
}

function Test-Http([string]$Name, [string]$Url, [string]$Method = "GET", [hashtable]$Headers = $null, $Body = $null) {
    try {
        $params = @{
            Uri             = $Url
            Method          = $Method
            TimeoutSec      = 15
            UseBasicParsing = $true
        }
        if ($Headers) { $params.Headers = $Headers }
        if ($null -ne $Body) {
            $params.ContentType = "application/json"
            $params.Body = $Body
        }
        $resp = Invoke-WebRequest @params
        if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) {
            Write-Pass $Name "$Method $Url → $($resp.StatusCode)"
        } else {
            Write-Fail $Name "$Method $Url → $($resp.StatusCode)"
        }
    } catch {
        Write-Fail $Name "$Method $Url → $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== XW 外网演示预检 ===" -ForegroundColor Cyan
Write-Host "HostBase  = $HostBase"
Write-Host "AgentBase = $AgentBase"
Write-Host ""

if (-not $SkipTcp) {
    Write-Host "-- TCP 基础件 --" -ForegroundColor Yellow
    Test-Tcp "MySQL" $MysqlHost $MysqlPort
    Test-Tcp "Redis" $RedisHost $RedisPort
    Test-Tcp "RabbitMQ" $RabbitHost $RabbitPort
    if (-not $SkipPg) {
        Test-Tcp "Postgres(知识/审计)" $PgHost $PgPort
    } else {
        Write-Host "[SKIP] Postgres（知识库可选）" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Write-Host "-- HTTP 应用 --" -ForegroundColor Yellow
Test-Http "Agent /health" "$AgentBase/health"
Test-Http "Agent LLM probe" "$AgentBase/api/agent/llm/probe" "POST" $null "{}"

# Host 未必有统一 /health；尝试常见根或 swagger
try {
    $h = Invoke-WebRequest -Uri "$HostBase/swagger/index.html" -Method GET -TimeoutSec 10 -UseBasicParsing
    if ($h.StatusCode -ge 200) {
        Write-Pass "Host swagger" "$HostBase/swagger → $($h.StatusCode)"
    }
} catch {
    try {
        $h2 = Invoke-WebRequest -Uri $HostBase -Method GET -TimeoutSec 10 -UseBasicParsing
        Write-Pass "Host 根路径" "$HostBase → $($h2.StatusCode)"
    } catch {
        Write-Fail "Host 可达性" $_.Exception.Message
    }
}

Write-Host ""
if ($failed -eq 0) {
    Write-Host "全部通过。可以挂 Nginx / 开公网域名。" -ForegroundColor Green
    exit 0
} else {
    Write-Host ("有 {0} 项失败：先修基础件与 LLM，再对外发布。" -f $failed) -ForegroundColor Red
    Write-Host "说明见 docs/agent-demo-public.md"
    exit 1
}

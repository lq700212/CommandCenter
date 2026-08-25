# CommandCenter 自动化测试 - 进程级冒烟层
# 启动真实 exe，验证：进程存活 >= 8 秒 + 当日日志含"从站建站/上电初始化"关键字；
# 关闭后再启动第二轮，防"首次能跑、二次启动崩"。退出码：0=通过，1=失败。
# 注：现场设备（相机/扫码枪）不在线只产生 WARN 日志，不影响判定。
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# 仓库根 = 本脚本目录向上四级（scripts -> commandcenter-test -> skills -> .opencode -> 仓库根）
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$exe = Join-Path $repoRoot "CommandCenter\bin\Debug\CommandCenter.exe"
if (-not (Test-Path -LiteralPath $exe)) { Write-Host "[SMOKE-FAIL] 未找到 $exe（先跑 build.ps1）"; exit 1 }
$binDir = Split-Path -Parent $exe

function Invoke-SmokeRound([int]$round) {
    Write-Host ("--- 冒烟第 {0} 轮 ---" -f $round)
    # 工作目录设为 bin\Debug：程序按相对路径读写 Config/Logs
    $p = Start-Process -FilePath $exe -WorkingDirectory $binDir -PassThru
    Start-Sleep -Seconds 8   # 给足建站/协调器磨合期时间
    if ($p.HasExited) {
        Write-Host ("[SMOKE-FAIL] 第 {0} 轮进程提前退出 ExitCode={1}" -f $round, $p.ExitCode)
        return $false
    }
    Write-Host ("  进程存活 (PID={0})" -f $p.Id)

    # 找当日最新日志，检查建站与上电初始化关键字（UTF-8 读取）
    $logFile = Get-ChildItem (Join-Path $binDir "Logs\运行日志_*.log") -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $logFile) { Write-Host "[SMOKE-FAIL] 未找到当日运行日志"; Stop-Process -Id $p.Id -Force; return $false }
    $lines = [IO.File]::ReadAllLines($logFile.FullName, [Text.Encoding]::UTF8)
    $tail = ($lines | Select-Object -Last 40) -join "`n"
    foreach ($kw in @("PLC 从站监听已启动", "上电初始化")) {
        if ($tail.Contains($kw)) { Write-Host ("  日志命中: " + $kw) }
        else { Write-Host ("[SMOKE-FAIL] 最新日志缺少关键字: " + $kw); Stop-Process -Id $p.Id -Force; return $false }
    }

    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2   # 留时间释放端口/文件句柄
    Write-Host ("[SMOKE-ROUND{0}-OK]" -f $round)
    return $true
}

Write-Host "=== [3/3] 进程级冒烟 ==="
if (-not (Invoke-SmokeRound 1)) { exit 1 }
if (-not (Invoke-SmokeRound 2)) { exit 1 }
Write-Host "[SMOKE-OK] 两轮冒烟全部通过"
exit 0

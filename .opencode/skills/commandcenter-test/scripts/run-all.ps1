# CommandCenter 自动化测试 - 总入口
# 依次执行：构建 → 回归用例集 → UI 交互回归 → 进程冒烟；任一层失败立即非零退出。
# 用法：powershell -ExecutionPolicy Bypass -File run-all.ps1 [-SkipBuild] [-SkipUi] [-SkipSmoke]
param(
    [switch]$SkipBuild,
    [switch]$SkipUi,
    [switch]$SkipSmoke
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$sw = [Diagnostics.Stopwatch]::StartNew()
$failed = $false

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1")
    if ($LASTEXITCODE -ne 0) { Write-Host "`n>>> 构建层失败，终止（不跑后续层）"; exit 1 }
}

& (Join-Path $PSScriptRoot "tests.ps1")
if ($LASTEXITCODE -ne 0) { Write-Host "`n>>> 回归用例层失败"; $failed = $true }

if (-not $SkipUi -and -not $failed) {
    & (Join-Path $PSScriptRoot "uitests.ps1")
    if ($LASTEXITCODE -ne 0) { Write-Host "`n>>> UI 交互回归层失败"; $failed = $true }
}
elseif ($failed) { Write-Host ">>> 用例层已失败，跳过 UI 交互回归层" }

if (-not $SkipSmoke -and -not $failed) {
    & (Join-Path $PSScriptRoot "smoke.ps1")
    if ($LASTEXITCODE -ne 0) { Write-Host "`n>>> 冒烟层失败"; $failed = $true }
}
elseif ($failed) { Write-Host ">>> 前序层已失败，跳过冒烟层" }

$sw.Stop()
Write-Host ""
if ($failed) { Write-Host ("════ 验证未通过，耗时 {0:n1}s ════" -f $sw.Elapsed.TotalSeconds); exit 1 }
Write-Host ("════ 全部验证通过（构建 + 用例 + UI 交互 + 冒烟），耗时 {0:n1}s ════" -f $sw.Elapsed.TotalSeconds)
exit 0

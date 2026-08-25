# CommandCenter 自动化测试 - 回归用例集层
# 流程：Roslyn csc 把 TestRunner.cs 编译进 bin\Debug（依赖 dll/Logs 目录天然正确）
#       → 运行 → 汇总 PASS/FAIL → 删除 runner 临时产物。
# 退出码：0=全部通过，1=存在失败或编译失败。
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# 仓库根 = 本脚本目录向上四级（scripts -> commandcenter-test -> skills -> .opencode -> 仓库根）
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$binDir   = Join-Path $repoRoot "CommandCenter\bin\Debug"
$cs       = Join-Path $PSScriptRoot "TestRunner.cs"
$csc      = "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
foreach ($f in @($cs, $csc)) { if (-not (Test-Path -LiteralPath $f)) { Write-Host "[TESTS-FAIL] 缺文件：$f"; exit 1 } }
$exe      = Join-Path $binDir "CommandCenter.exe"
if (-not (Test-Path -LiteralPath $exe)) { Write-Host "[TESTS-FAIL] 未找到 CommandCenter.exe（先跑 build.ps1）"; exit 1 }

Write-Host "=== [2/3] 回归测试用例集 ==="

# ── 编译 runner 到 bin\Debug（BaseDirectory=bin\Debug，日志/依赖路径与真实程序一致）──
$runner   = Join-Path $binDir "cc_test_runner.exe"
$runnerPdb = Join-Path $binDir "cc_test_runner.pdb"
& $csc /nologo /target:exe /platform:AnyCPU /codepage:65001 /out:$runner `
    /r:$exe `
    /r:"$(Join-Path $binDir 'Newtonsoft.Json.dll')" `
    /r:"$(Join-Path $binDir 'NModbus.dll')" `
    /r:System.dll /r:System.Core.dll `
    $cs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $runner)) {
    Write-Host "[TESTS-FAIL] TestRunner 编译失败"; exit 1
}

# ── 运行（工作目录 bin\Debug；502 被主程序占用时用例自动降级为注入 DataStore 兜底）──
$output = & $runner 2>&1
$code   = $LASTEXITCODE
$output | ForEach-Object { $_ }    # 透传用例输出

# ── 清理临时产物（无论成败都删，防 bin 里残留非交付物）──
Remove-Item -LiteralPath $runner -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $runnerPdb -Force -ErrorAction SilentlyContinue

if ($code -ne 0) {
    Write-Host "[TESTS-FAIL] 存在失败用例（退出码 $code），逐条 FAIL 见上方"
    exit 1
}
Write-Host "[TESTS-OK] 全部用例通过"
exit 0

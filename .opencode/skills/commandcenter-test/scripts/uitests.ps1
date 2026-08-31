# CommandCenter 自动化测试 - UI 交互回归层（V2.15.x 新增，第 3 层）
# 定位：TestRunner.cs 只覆盖纯逻辑/服务层，控件层行为（DataGridView 下拉"值无效→回退成候选
#       第一项"这类）测不到。本层用 UiProbe.cs 拉起【真实产品窗体】模拟用户操作来守住 UI 行为。
# 流程：Roslyn csc 把 UiProbe.cs 编译进 bin\Debug（/r:CommandCenter.exe，同一份实现）
#       → 运行（[STAThread]，WinForms 要求单线程套间）→ 汇总 PASS/FAIL → 删除临时产物。
# 退出码：0=全部通过；1=存在失败或编译失败。
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# 仓库根 = 本脚本目录向上四级（scripts -> commandcenter-test -> skills -> .opencode -> 仓库根）
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$binDir   = Join-Path $repoRoot "CommandCenter\bin\Debug"
$cs       = Join-Path $PSScriptRoot "UiProbe.cs"
$csc      = "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
foreach ($f in @($cs, $csc)) { if (-not (Test-Path -LiteralPath $f)) { Write-Host "[UITESTS-FAIL] 缺文件：$f"; exit 1 } }
$exe = Join-Path $binDir "CommandCenter.exe"
if (-not (Test-Path -LiteralPath $exe)) { Write-Host "[UITESTS-FAIL] 未找到 CommandCenter.exe（先跑 build.ps1）"; exit 1 }

Write-Host "=== [3/4] UI 交互回归（真实窗体 WindowPointForm）==="

# ── 编译探针到 bin\Debug（BaseDirectory=bin\Debug，依赖 dll 路径与真实程序一致）──
$probe    = Join-Path $binDir "cc_ui_probe.exe"
$probePdb = Join-Path $binDir "cc_ui_probe.pdb"
& $csc /nologo /target:exe /platform:AnyCPU /codepage:65001 /out:$probe `
    /r:$exe `
    /r:"$(Join-Path $binDir 'Newtonsoft.Json.dll')" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $cs
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $probe)) {
    Write-Host "[UITESTS-FAIL] UiProbe 编译失败"; exit 1
}

# ── 运行（探针会把窗体放到屏幕外，不打断用户；不连设备、不落盘配置）──
$output = & $probe 2>&1
$code   = $LASTEXITCODE
$output | ForEach-Object { $_ }    # 透传用例输出

# ── 清理临时产物（无论成败都删，防 bin 里残留非交付物）──
Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $probePdb -Force -ErrorAction SilentlyContinue

if ($code -ne 0) {
    Write-Host "[UITESTS-FAIL] 存在失败用例（退出码 $code），逐条 FAIL 见上方"
    exit 1
}
Write-Host "[UITESTS-OK] UI 交互回归全部通过"
exit 0

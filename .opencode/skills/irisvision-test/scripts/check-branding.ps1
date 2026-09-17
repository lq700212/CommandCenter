# IrisVision 自动化测试 - 品牌/图标检查层（V2.16.5 新增）
# 验证"光阑视界 IrisVision"品牌接线：源图 → 多尺寸 ico → csproj → exe 版本资源 → exe 自带图标，
# 任一断裂（如下次有人误删 ApplicationIcon / 覆盖了 app.ico / 改错 AssemblyInfo）立即 FAIL。
# 由 build.ps1 在"产出 exe"之后自动调用；也可单独跑（要求 bin\Debug\IrisVision.exe 已存在）。
# 退出码：0=通过，1=失败。
# 注意：本文件含中文，必须存 UTF-8 with BOM（Windows PowerShell 5.1 对无 BOM 文件按 GBK 解析）。
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# 仓库根 = 本脚本目录向上四级（scripts -> irisvision-test -> skills -> .opencode -> 仓库根）
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$icoFile = Join-Path $repoRoot "IrisVision\Resources\app.ico"
$csproj   = Join-Path $repoRoot "IrisVision\IrisVision.csproj"
$exe      = Join-Path $repoRoot "IrisVision\bin\Debug\IrisVision.exe"

Write-Host "=== [1/3+] 品牌/图标检查（光阑视界 IrisVision） ==="

# ① app.ico 存在且为多尺寸（含任务栏 16 / 对话框 32 / 资源管理器 48 / 高分屏 256）
if (-not (Test-Path -LiteralPath $icoFile)) { Write-Host "[BRAND-FAIL] 缺少 $icoFile"; exit 1 }
$icoBytes = [IO.File]::ReadAllBytes($icoFile)
$count = [BitConverter]::ToUInt16($icoBytes, 4)
$widths = @()
for ($i = 0; $i -lt $count; $i++) {
    $w = $icoBytes[6 + $i * 16]
    if ($w -eq 0) { $w = 256 }
    $widths += $w
}
foreach ($need in @(16, 32, 48, 256)) {
    if ($widths -notcontains $need) { Write-Host ("[BRAND-FAIL] app.ico 缺少 {0}x{0} 尺寸层（现有：{1}）" -f $need, ($widths -join ",")); exit 1 }
}
Write-Host ("  app.ico 多尺寸 OK（{0} 层：{1}）" -f $count, ($widths -join ","))

# ② csproj 把 app.ico 声明为 exe 主图标（exe/快捷方式/任务栏图标的单一来源）
$csprojText = [IO.File]::ReadAllText($csproj, [Text.Encoding]::UTF8)
if (-not $csprojText.Contains("<ApplicationIcon>Resources\app.ico</ApplicationIcon>")) {
    Write-Host "[BRAND-FAIL] csproj 缺少 <ApplicationIcon>Resources\app.ico</ApplicationIcon>"
    exit 1
}
Write-Host "  csproj ApplicationIcon 接线 OK"

# ③ exe 版本资源：产品名 IrisVision + 中文名光阑视界（AssemblyInfo 改错立刻现形）
if (-not (Test-Path -LiteralPath $exe)) { Write-Host "[BRAND-FAIL] 未找到 $exe（先跑 build.ps1）"; exit 1 }
$ver = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($ver.ProductName -ne "IrisVision") {
    Write-Host ("[BRAND-FAIL] exe 产品名期望=IrisVision，实际={0}" -f $ver.ProductName)
    exit 1
}
if (-not $ver.FileDescription.Contains("光阑视界")) {
    Write-Host ("[BRAND-FAIL] exe 文件说明缺少中文名光阑视界，实际={0}" -f $ver.FileDescription)
    exit 1
}
Write-Host ("  exe 版本资源 OK（产品={0}，说明={1}）" -f $ver.ProductName, $ver.FileDescription)

# ④ exe 自带图标可提取（桌面快捷方式/任务栏/资源管理器就靠它；取不到说明图标没打进 exe）
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
if ($icon -eq $null) { Write-Host "[BRAND-FAIL] exe 内嵌图标提取失败（图标未编译进 exe）"; exit 1 }
$icon.Dispose()
Write-Host "  exe 内嵌图标可提取 OK"

Write-Host "[BRAND-OK] 品牌/图标检查全部通过"
exit 0

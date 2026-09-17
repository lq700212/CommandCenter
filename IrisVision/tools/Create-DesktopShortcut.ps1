# ============================================================
# 光阑视界 IrisVision 桌面快捷方式一键创建脚本
# ------------------------------------------------------------
# 作用：在当前用户桌面建"光阑视界 IrisVision.lnk"，目标指向
#   现场部署/开发机的 IrisVision.exe（exe 文件名保持不变，
#   只改显示名：中文"光阑视界"、英文"IrisVision"）。
# 快捷方式图标不用单独配：图标已编译进 exe 本体（csproj 的
#   ApplicationIcon = Resources\app.ico），快捷方式默认就取
#   目标 exe 的图标，重装/换图标后删了重建一次即可。
#
# 用法（仓库根目录）：
#   powershell -ExecutionPolicy Bypass -File ".\IrisVision\tools\Create-DesktopShortcut.ps1"
#   # 或显式指定 exe（默认按 混淆版 → Release → Debug 自动找）：
#   powershell -ExecutionPolicy Bypass -File ".\IrisVision\tools\Create-DesktopShortcut.ps1" `
#     -ExePath "E:\部署\IrisVision.exe"
#
# 注意：本文件含中文，必须存 UTF-8 with BOM（Windows PowerShell 5.1
#   对无 BOM 文件按 GBK 解析，中文注释会破坏语法）。
# ============================================================
param(
    # 目标 exe 完整路径；留空则按"混淆版 → Release → Debug"自动找第一个存在的。
    [string]$ExePath = ""
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path   # ...\IrisVision\tools
$ProjDir   = Split-Path -Parent $ScriptDir                     # ...\IrisVision

# ---- 确定目标 exe ----
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $candidates = @(
        (Join-Path $ProjDir "bin\Obfuscated\IrisVision.exe"),  # 现场部署版优先
        (Join-Path $ProjDir "bin\Release\IrisVision.exe"),
        (Join-Path $ProjDir "bin\Debug\IrisVision.exe")
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { $ExePath = $c; break }
    }
}
if ([string]::IsNullOrWhiteSpace($ExePath) -or -not (Test-Path -LiteralPath $ExePath)) {
    throw "找不到 IrisVision.exe（自动查找 混淆版/Release/Debug 均无结果），请用 -ExePath 显式指定。"
}
$ExePath = [IO.Path]::GetFullPath($ExePath)
$WorkDir  = Split-Path -Parent $ExePath

# ---- 建桌面快捷方式（中文名 + 英文名双语，exe 文件名不变） ----
$Desktop = [Environment]::GetFolderPath("Desktop")
$LnkPath = Join-Path $Desktop "光阑视界 IrisVision.lnk"
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($LnkPath)
$lnk.TargetPath = $ExePath
$lnk.WorkingDirectory = $WorkDir
$lnk.IconLocation = "$ExePath,0"   # 取 exe 内嵌主图标（Resources\app.ico 打进去的）
$lnk.Description = "光阑视界 IrisVision 上位机软件"
$lnk.Save()

Write-Host "桌面快捷方式已创建：$LnkPath" -ForegroundColor Green
Write-Host ("  目标：{0}" -f $ExePath)
Write-Host "  图标：取目标 exe 内嵌主图标（换图标后重跑本脚本即可刷新）。"

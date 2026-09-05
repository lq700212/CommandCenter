# ============================================================
# CommandCenter 代码混淆一键发布脚本（build-obfuscated.ps1）
# ------------------------------------------------------------
# 作用：产出"防反编译"的发布版程序。四步走：
#   1. MSBuild 构建 Release 版（混淆输入用）；
#   2. 调用 tools\Obfuscar\Obfuscar.Console.exe 按 obfuscar.xml
#      混淆 CommandCenter.exe（类/方法/字段/字符串全部打乱，
#      Models 配置模型跳过保持 json 兼容）；
#   3. 把运行必需的第三方 dll 与 exe.config 复制进发布目录，
#      并做一轮"启动保活"冒烟测试，确保混淆后程序能正常起来；
#   4. 自动打包成"可直接上传/部署"的 zip（纯 ASCII 文件名，
#      排除运行时 Logs），版本号取最近 git tag。
#
# 产物：
#   - 混淆目录：项目\CommandCenter\bin\Obfuscated\
#     （现场部署时整个目录拷过去即可，含 exe + 两个第三方 dll + config）
#   - 上传包：  bin\CommandCenter_{版本号}_obfuscated.zip（本条为 V2.16 起新增）
#
# 用法（本仓库根目录）：
#   & ".\CommandCenter\build-obfuscated.ps1"
#
# 注意事项：
#   - 混淆只改名字/字符串，不改功能；混淆后 exe 无法调试（PDB 会失配），
#     现场排查问题请用未混淆的 Debug 版 + 日志。
#   - 每次发布前请确保 git 工作区干净（脚本只碰 bin/，不入库）。
#   - 本脚本用 AGENTS.md 里约定的 VS 2018 企业版 MSBuild 路径；
#     换机器/版本请改下行 $MsBuild。
# ============================================================
$ErrorActionPreference = "Stop"

# ---- 路径 ----
$ScriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path   # ...\CommandCenter
$RepoRoot    = Split-Path -Parent $ScriptDir                     # 仓库根
$MsBuild     = "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
$ObfuscarExe = Join-Path $ScriptDir "tools\Obfuscar\Obfuscar.Console.exe"
$ObfuscarCfg = Join-Path $ScriptDir "obfuscar.xml"
$ReleaseDir  = Join-Path $ScriptDir "bin\Release"
$OutDir      = Join-Path $ScriptDir "bin\Obfuscated"
$LibsDir     = Join-Path $ScriptDir "libs"

# ---- 工具存在性 ----
if (-not (Test-Path $MsBuild))          { throw "找不到 MSBuild：$MsBuild（请检查 AGENTS.md 约定的 VS 路径）" }
if (-not (Test-Path $ObfuscarExe))      { throw "找不到 Obfuscar：$ObfuscarExe" }
if (-not (Test-Path $ObfuscarCfg))      { throw "找不到混淆配置：$ObfuscarCfg" }

Write-Host "[1/3] 构建 Release ..." -ForegroundColor Cyan
Push-Location $ScriptDir
try {
    & $MsBuild ".\CommandCenter.csproj" /p:Configuration=Release /p:Platform=AnyCPU /t:Build /nologo /v:m /m
    if ($LASTEXITCODE -ne 0) { throw "Release 构建失败（MSBuild 退出码 $LASTEXITCODE）" }
    if (-not (Test-Path (Join-Path $ReleaseDir "CommandCenter.exe"))) {
        throw "构建产物缺失：Release\CommandCenter.exe"
    }
} finally { Pop-Location }

Write-Host "[2/3] 运行 Obfuscar 混淆 ..." -ForegroundColor Cyan
# 输出目录清空重建（Obfuscar 不会自动清旧文件，防混入过期产物）
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
# 注意：Obfuscar 配置里的 InPath/OutPath 是相对于【当前工作目录】的，
# 脚本必须切到项目目录再执行，配置里的 "bin\Release" 才能对上。
Push-Location $ScriptDir
try {
    & $ObfuscarExe $ObfuscarCfg
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar 混淆失败（退出码 $LASTEXITCODE），详情见上方输出" }
} finally { Pop-Location }
if (-not (Test-Path (Join-Path $OutDir "CommandCenter.exe"))) {
    throw "混淆产物缺失：bin\Obfuscated\CommandCenter.exe"
}

Write-Host "[3/3] 补齐运行依赖并冒烟 ..." -ForegroundColor Cyan
# 混淆只处理了 exe，运行还要第三方 dll + 配置文件，一并拷进发布目录
Copy-Item (Join-Path $LibsDir "Newtonsoft.Json.dll") $OutDir -Force
Copy-Item (Join-Path $LibsDir "NModbus.dll")         $OutDir -Force
if (Test-Path (Join-Path $ReleaseDir "CommandCenter.exe.config")) {
    Copy-Item (Join-Path $ReleaseDir "CommandCenter.exe.config") $OutDir -Force
}

# 冒烟：启动混淆版 exe，5 秒后确认进程还活着（没崩在启动/反序列化阶段）
$p = Start-Process -FilePath (Join-Path $OutDir "CommandCenter.exe") -WorkingDirectory $OutDir -PassThru
Start-Sleep -Seconds 6
if ($p.HasExited) {
    throw "冒烟失败：混淆版程序启动即退出（ExitCode=$($p.ExitCode)），请检查日志 bin\Obfuscated\Logs\"
}
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue

Write-Host "`n[4/4] 打包上传用 zip ..." -ForegroundColor Cyan
# 打包前清掉冒烟产生的运行时 Logs（上传包只含程序文件，不含运行时日志/数据）
$SmokeLogs = Join-Path $OutDir "Logs"
if (Test-Path $SmokeLogs) { Remove-Item $SmokeLogs -Recurse -Force }
# 版本号取最近 git tag（去 v 前缀），取不到用日期兜底（如非 git 目录）
$VersionTag = (& git -C $RepoRoot describe --tags --abbrev=0 2>$null) -replace '^v', ''
if (-not $VersionTag) { $VersionTag = Get-Date -Format 'yyyyMMdd' }
# 纯 ASCII 文件名：防中文文件名跨机器/网盘/上传控件乱码
$ZipPath = Join-Path $ScriptDir "bin\CommandCenter_${VersionTag}_obfuscated.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
# 只打固定 5 个发布文件（不复用"限定通配"以免把 Logs 等运行时文件混进包）
$PackFiles = @(
    (Join-Path $OutDir "CommandCenter.exe"),
    (Join-Path $OutDir "CommandCenter.exe.config"),
    (Join-Path $OutDir "Mapping.txt"),
    (Join-Path $OutDir "Newtonsoft.Json.dll"),
    (Join-Path $OutDir "NModbus.dll")
)
foreach ($f in $PackFiles) { if (-not (Test-Path $f)) { throw "打包文件缺失：$f" } }
Compress-Archive -Path $PackFiles -DestinationPath $ZipPath -Force

Write-Host "`n混淆发布完成！`n  - 混淆目录：$OutDir`n  - 上传包：$ZipPath" -ForegroundColor Green
Write-Host "  上传包内含：CommandCenter.exe（已混淆）/ Newtonsoft.Json.dll / NModbus.dll / CommandCenter.exe.config"
Write-Host "  另含 obfuscar 的 Mapping.txt（原名↔混淆名对照表，仅内部排查崩溃栈用，勿对外泄露）"
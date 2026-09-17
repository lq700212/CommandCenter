# IrisVision 自动化测试 - 构建层
# 用 MSBuild 编译 Debug 版，零 error 且产出 exe 才算通过。退出码：0=成功，1=失败。
$ErrorActionPreference = "Stop"

# 仓库根 = 本脚本目录向上四级（scripts -> irisvision-test -> skills -> .opencode -> 仓库根）
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$msbuild = "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path -LiteralPath $msbuild)) { Write-Host "[BUILD-FAIL] 找不到 MSBuild：$msbuild"; exit 1 }

Write-Host "=== [1/3] 构建 IrisVision (Debug) ==="
& $msbuild "$repoRoot\IrisVision\IrisVision.csproj" /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m /m
if ($LASTEXITCODE -ne 0) { Write-Host "[BUILD-FAIL] MSBuild 退出码 $LASTEXITCODE"; exit 1 }
$exe = Join-Path $repoRoot "IrisVision\bin\Debug\IrisVision.exe"
if (-not (Test-Path -LiteralPath $exe)) { Write-Host "[BUILD-FAIL] 未产出 $exe"; exit 1 }
Write-Host "[BUILD-OK] $exe"
# 品牌/图标检查（V2.16.5）：多尺寸 app.ico → csproj 接线 → exe 版本资源与内嵌图标，不断裂才算构建通过
& (Join-Path $PSScriptRoot "check-branding.ps1")
if ($LASTEXITCODE -ne 0) { Write-Host "[BUILD-FAIL] 品牌/图标检查未通过"; exit 1 }
exit 0

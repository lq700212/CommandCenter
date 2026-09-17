<#
.SYNOPSIS
  光阑视界 IrisVision 工控机一键激活（与 AgingTestSystem/HJVision 同源同口径，与 tools/auto_activate.py 同逻辑）。
.DESCRIPTION
  把原来四步手工活（一：选项菜单【软件授权】抄设备ID/设备码；二：回办公室用《获取激活码》工具算码；
  三：回工控机输入点激活；四：出厂手写 MainSetting.ini 的 RunHash1 设备绑定）收成一次双击。
  做法：读本机 CPU 序列号 → 按产品内公式算出两键 → 备份后写入程序目录
  MainSetting.ini [RunHash] → 回读重算状态并打印结论。
  公式与 Services/SoftwareActivation.Encrypt 逐字节一致（MD5 取前 15 字节 hex，共 30 字符；
  输入全是 ASCII，编码无差异）；写盘走 kernel32 INI API（与产品同一条路，不破坏其它段）；
  RunHash1（设备绑定）和 RunHash2（永久/试用起点）一次写齐，新机不用再找厂商手写第一键。
  只用 Windows 自带功能（PowerShell 2.0+ / .NET Framework），工控机免装任何环境。
  同一套《获取激活码》工具通用：本脚本打印的四码与工具算出的完全一致。
.EXAMPLE
  双击 tools/auto_activate.bat 即永久激活。
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/auto_activate.ps1 -Mode trial
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/auto_activate.ps1 -DryRun
#>
param(
  [ValidateSet("permanent", "trial")][string]$Mode = "permanent",
  [string]$Ini = "",
  [string]$ExeDir = "",
  [switch]$DryRun,
  [switch]$NoPause
)

$ErrorActionPreference = "Continue"
$Section = "RunHash"
$KeyDevice = "RunHash1"
$KeyRuntime = "RunHash2"
$IniName = "MainSetting.ini"
$TotalSlots = 840
$ValidSlots = 768
# 脚本所在目录（必须在顶层抓：函数内的 $MyInvocation 指的是函数本身，Path 为空）
$script:ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NativeIni {
  [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern int GetPrivateProfileString(string s, string k, string d, StringBuilder r, int n, string p);
  [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool WritePrivateProfileString(string s, string k, string v, string p);
}
"@

function Get-Encrypt([string]$s) {
  # 与产品 SoftwareActivation.Encrypt /《获取激活码》Form1.Encrypt 逐字节一致
  $md5 = [System.Security.Cryptography.MD5]::Create()
  try {
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($s)
    $hash = $md5.ComputeHash($bytes)
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt ($hash.Length - 1); $i++) {
      [void]$sb.Append($hash[$i].ToString("x").PadLeft(2, '0'))
    }
    return $sb.ToString()
  }
  finally { $md5.Clear(); $md5.Dispose() }
}

function Get-CpuId {
  # 取第一块 CPU 的 ProcessorId（与产品 GetCpuSerialNumber 同口径；新老系统各走一条）
  try {
    $id = Get-CimInstance Win32_Processor -ErrorAction Stop |
      Select-Object -First 1 -ExpandProperty ProcessorId -ErrorAction Stop
    if ($id) { return $id.Trim() }
  }
  catch { }
  try {
    # 老工控机（PowerShell 2.0 / 未装 WMF）没有 Get-CimInstance，走 WMI 兼容路
    $id = Get-WmiObject Win32_Processor -ErrorAction Stop |
      Select-Object -First 1 -ExpandProperty ProcessorId -ErrorAction Stop
    if ($id) { return $id.Trim() }
  }
  catch { }
  return ""
}

function Find-Slot([string]$stored, [string]$cpuId) {
  # 找计数格 0..839（与主窗 LicenseTimer_Tick 的 for 循环一致），找不到返回 -1
  if ([string]::IsNullOrEmpty($stored)) { return -1 }
  for ($i = 0; $i -lt $TotalSlots; $i++) {
    if ($stored -eq (Get-Encrypt ($cpuId + $i.ToString()))) { return $i }
  }
  return -1
}

function Get-Status([string]$h1, [string]$h2, [string]$cpuId) {
  # 与产品 ComputeStatus 同判定：先设备绑定，再永久，再计数格；返回 "状态|格|剩余天"
  if ([string]::IsNullOrEmpty($h1) -or ($h1 -ne (Get-Encrypt ($cpuId + "A")))) {
    return "NewDevice|-1|0"
  }
  if ($h2 -eq (Get-Encrypt ($cpuId + "ALL"))) { return "Permanent|-1|0" }
  $slot = Find-Slot $h2 $cpuId
  if (($slot -lt 0) -or ($slot -ge $ValidSlots)) { return "Expired|$slot|0" }
  $days = 30 - [int]($slot / 24)
  return "InTrial|$slot|$days"
}

function Get-StatusText([string]$status, [int]$days) {
  if ($status -eq "Permanent") { return "永久使用" }
  if ($status -eq "InTrial") { return "试用中，剩余 $days 天" }
  if ($status -eq "NewDevice") { return "未绑定设备（新设备）" }
  return "已过期"
}

function Read-IniValue([string]$path, [string]$key) {
  try {
    $sb = New-Object System.Text.StringBuilder 1024
    [void][NativeIni]::GetPrivateProfileString($Section, $key, "", $sb, $sb.Capacity, $path)
    return $sb.ToString()
  }
  catch { return Read-IniValueText $path $key }
}

function Write-IniValue([string]$path, [string]$key, [string]$value) {
  try {
    if ([NativeIni]::WritePrivateProfileString($Section, $key, $value, $path)) { return $true }
  }
  catch { }
  return (Write-IniValueText $path $key $value)
}

function Read-AllLinesAuto([string]$path) {
  # 文本兜底：按 Unicode / UTF-8 / 系统默认依次试读，只为保住其它段
  $encs = @([System.Text.Encoding]::Unicode,
            [System.Text.Encoding]::UTF8,
            [System.Text.Encoding]::Default)
  foreach ($enc in $encs) {
    try { return @([System.IO.File]::ReadAllLines($path, $enc), $enc) }
    catch { }
  }
  return @($null, [System.Text.Encoding]::Unicode)
}

function Read-IniValueText([string]$path, [string]$key) {
  if (-not (Test-Path -LiteralPath $path)) { return "" }
  $r = Read-AllLinesAuto $path
  $lines = $r[0]
  if ($lines -eq $null) { return "" }
  $inSection = $false
  foreach ($line in $lines) {
    $s = $line.Trim()
    if ($s.StartsWith("[") -and $s.EndsWith("]")) {
      $inSection = ($s.Substring(1, $s.Length - 2).Trim() -eq $Section)
    }
    elseif ($inSection -and ($s.Contains("=")) -and (-not $s.StartsWith(";"))) {
      $k = $s.Substring(0, $s.IndexOf("=")).Trim()
      if ($k -eq $key) { return $s.Substring($s.IndexOf("=") + 1).Trim() }
    }
  }
  return ""
}

function Write-IniValueText([string]$path, [string]$key, [string]$value) {
  # 文本兜底：只改 [RunHash] 段内对应键，其它段原样保留
  try {
    $lines = @()
    $enc = [System.Text.Encoding]::Unicode
    if (Test-Path -LiteralPath $path) {
      $r = Read-AllLinesAuto $path
      if ($r[0] -ne $null) { $lines = $r[0] }
      $enc = $r[1]
    }
    $out = New-Object System.Collections.ArrayList
    $inSection = $false
    $done = $false
    foreach ($line in $lines) {
      $s = $line.Trim()
      if ($s.StartsWith("[") -and $s.EndsWith("]")) {
        if ($inSection -and (-not $done)) {
          [void]$out.Add("$key=$value")
          $done = $true
        }
        $inSection = ($s.Substring(1, $s.Length - 2).Trim() -eq $Section)
        [void]$out.Add($line)
      }
      elseif ($inSection -and ($s.Contains("=")) -and (-not $s.StartsWith(";")) -and
              ($s.Substring(0, $s.IndexOf("=")).Trim() -eq $key)) {
        [void]$out.Add("$key=$value")
        $done = $true
      }
      else { [void]$out.Add($line) }
    }
    if (-not $done) {
      $hasSection = $false
      foreach ($line in $out) {
        if ($line.Trim() -eq "[$Section]") { $hasSection = $true; break }
      }
      if (-not $hasSection) { [void]$out.Add("[$Section]") }
      [void]$out.Add("$key=$value")
    }
    $dir = Split-Path -Parent ([System.IO.Path]::GetFullPath($path))
    if (($dir -ne "") -and (-not (Test-Path -LiteralPath $dir))) {
      [void](New-Item -ItemType Directory -Path $dir -Force)
    }
    [System.IO.File]::WriteAllLines($path, [string[]]$out, $enc)
    return $true
  }
  catch { return $false }
}

function Resolve-IniPath {
  if ($Ini -ne "") { return [System.IO.Path]::GetFullPath($Ini) }
  if ($ExeDir -ne "") {
    return [System.IO.Path]::Combine([System.IO.Path]::GetFullPath($ExeDir), $IniName)
  }
  $cwd = (Get-Location).Path
  if ((Test-Path -LiteralPath (Join-Path $cwd $IniName)) -or
      (Test-Path -LiteralPath (Join-Path $cwd "IrisVision.exe"))) {
    return (Join-Path $cwd $IniName)
  }
  return (Join-Path $script:ScriptDir $IniName)
}

# ---------------- 主流程 ----------------
Write-Host "============================================================"
Write-Host "光阑视界 IrisVision 一键激活（与软件内【选项】→【软件授权】同口径）"
Write-Host "============================================================"

$cpuId = Get-CpuId
if ([string]::IsNullOrEmpty($cpuId)) {
  Write-Host "[失败] 读不到 CPU 序列号（WMI），请检查 WMI 服务后重试。"
  exit 1
}
Write-Host "[1/4] 本机设备ID：$cpuId"

$deviceCode = Get-Encrypt ($cpuId + "1")          # 激活窗显示的"设备码"
$idCode = Get-Encrypt ($cpuId + "A")              # 出厂写 RunHash1 的值
$code30 = Get-Encrypt ($deviceCode + "30")        # 工具"30天激活码"
$codePerm = Get-Encrypt ($deviceCode + "ALL")     # 工具"永久激活码"
$runHash1 = $idCode
$runHash2Perm = Get-Encrypt ($cpuId + "ALL")      # 永久：RunHash2 定格于此
$runHash2Trial = Get-Encrypt ($cpuId + "0")       # 30天：RunHash2 从第 0 格起
Write-Host "[2/4] 设备码：$deviceCode"
Write-Host "      设备ID码（RunHash1）：$idCode"
Write-Host "      30天激活码：$code30"
Write-Host "      永久激活码：$codePerm"
Write-Host "      （以上四码与《获取激活码》工具算出的完全一致，可存档备查）"

if ($Mode -eq "permanent") { $wantRunHash2 = $runHash2Perm; $modeText = "永久" }
else { $wantRunHash2 = $runHash2Trial; $modeText = "30天试用" }
Write-Host "[3/4] 本次写入：RunHash1=$runHash1 / RunHash2=$wantRunHash2（$modeText）"

$iniPath = Resolve-IniPath
Write-Host "      目标文件：$iniPath"

if ($DryRun) {
  Write-Host "[试算模式] 未写盘。若码值无误，去掉 -DryRun 重跑一次即写盘。"
  exit 0
}

if (Test-Path -LiteralPath $iniPath) {
  try {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $bak = "$iniPath.bak.$stamp"
    Copy-Item -LiteralPath $iniPath -Destination $bak -Force
    Write-Host "      已备份旧文件：$bak"
  }
  catch { }
}
if ((-not (Write-IniValue $iniPath $KeyDevice $runHash1)) -or
    (-not (Write-IniValue $iniPath $KeyRuntime $wantRunHash2))) {
  Write-Host "[失败] 写盘失败。程序若装在 C 盘请右键“以管理员身份运行”后重试。"
  exit 1
}

$h1 = Read-IniValue $iniPath $KeyDevice
$h2 = Read-IniValue $iniPath $KeyRuntime
$parts = (Get-Status $h1 $h2 $cpuId).Split("|")
$statusText = Get-StatusText $parts[0] ([int]$parts[2])
Write-Host "[4/4] 回读校验：$statusText"
$ok = (($parts[0] -eq "Permanent") -and ($Mode -eq "permanent")) -or
      (($parts[0] -eq "InTrial") -and ($Mode -eq "trial"))
if (-not $ok) {
  Write-Host "[失败] 回读状态与预期不符，请把上方码值发给维护人员排查。"
  exit 1
}

Write-Host "------------------------------------------------------------"
Write-Host "[成功] 已激活（$statusText）。请重启光阑视界 IrisVision，【选项】→【软件授权】应显示“$statusText”。"
Write-Host "      提示：激活前若软件开着，先关掉再跑本脚本，重启后生效。"
if (-not $NoPause -and [Environment]::UserInteractive) {
  Write-Host "按回车键退出..."
  [void][Console]::ReadLine()
}
exit 0

@echo off
rem 光阑视界 IrisVision 工控机一键激活（双击即永久激活；只用 Windows 自带 PowerShell，免装环境）
rem 改 30 天试用：auto_activate.bat -Mode trial
rem 只试算不写盘：auto_activate.bat -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto_activate.ps1" %*

@echo off
rem 本文件为 GBK 编码（中文 Windows 控制台默认代码页 936），不要切换 chcp 65001。
title FastBatchPlot 批打印 - 一键卸载
echo 正在启动 FastBatchPlot 一键卸载程序...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-All.ps1" -Uninstall
echo.
pause

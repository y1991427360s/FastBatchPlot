[CmdletBinding()]
param(
    [switch]$Uninstall
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-Banner([string]$title) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "          $title" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Ensure-CadClosed {
    $processes = @(Get-Process -Name ZWCAD, zwcad, acad -ErrorAction SilentlyContinue)
    if ($processes.Count -gt 0) {
        $visible = @($processes | Where-Object { $_.MainWindowHandle -ne 0 })
        if ($visible.Count -gt 0) {
            Write-Host "【提示】检测到 CAD 正在运行！" -ForegroundColor Yellow
            Write-Host "请先在 CAD 中按 Ctrl+S 保存手头图纸，并关闭 CAD 窗口后继续。" -ForegroundColor Yellow
            Write-Host "等待 CAD 关闭中 (按 Ctrl+C 可取消)..." -ForegroundColor DarkGray
            while ($true) {
                Start-Sleep -Seconds 1
                $active = @(Get-Process -Name ZWCAD, zwcad, acad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
                if ($active.Count -eq 0) { break }
            }
        }
        $headless = @(Get-Process -Name ZWCAD, zwcad, acad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -eq 0 })
        if ($headless.Count -gt 0) {
            Write-Host "正在清理 CAD 后台残留无窗口进程..." -ForegroundColor DarkGray
            $headless | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
    }
}

if ($Uninstall) {
    Write-Banner "FastBatchPlot 批打印插件 - 一键卸载"
    Ensure-CadClosed

    $hasAction = $false

    # 卸载 ZWCAD 2026
    $zwcadDest = Join-Path (Join-Path $env:LOCALAPPDATA 'FastBatchPlot') 'ZWCAD2026'
    $zwcadKey = 'HKCU:\Software\ZWSOFT\ZWCAD\2026'
    $hasZwKey = $false
    if (Test-Path -LiteralPath $zwcadKey) {
        $langs = Get-ChildItem -LiteralPath $zwcadKey | Where-Object { $_.PSChildName -match '^[a-z]{2}-[A-Z]{2}$' }
        foreach ($l in $langs) {
            if (Test-Path -LiteralPath (Join-Path $l.PSPath 'Applications\FastBatchPlot')) {
                $hasZwKey = $true
                break
            }
        }
    }
    if ((Test-Path -LiteralPath $zwcadDest) -or $hasZwKey) {
        Write-Host "正在卸载 ZWCAD 2026 插件..." -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'Install-ZWCAD.ps1') -Uninstall
        $hasAction = $true
    }

    # 卸载 AutoCAD 2018
    $acadDest = Join-Path (Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins') 'FastBatchPlot.bundle'
    if ((Test-Path -LiteralPath $acadDest) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Install-AutoCAD.ps1'))) {
        Write-Host "正在卸载 AutoCAD 2018 插件..." -ForegroundColor Yellow
        & (Join-Path $PSScriptRoot 'Install-AutoCAD.ps1') -Uninstall
        $hasAction = $true
    }

    if (-not $hasAction) {
        Write-Host "未检测到已安装的 FastBatchPlot 插件。" -ForegroundColor Gray
    } else {
        Write-Host ""
        Write-Host "【卸载完成】所有插件已安全移除（您的用户配置文件已保留）。" -ForegroundColor Green
    }
    return
}

Write-Banner "FastBatchPlot 批打印插件 - 一键安装"
Ensure-CadClosed

$zwcadKey = 'HKCU:\Software\ZWSOFT\ZWCAD\2026'
$zwcadInstalled = (Test-Path -LiteralPath $zwcadKey) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ZWCAD2026'))
$autocadPlugins = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
# 发布包默认只含中望版；包内没有 AutoCAD2018 目录时不安装 AutoCAD 版本。
$autocadInstalled = (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'AutoCAD2018')) -and
    ((Test-Path -LiteralPath 'HKCU:\Software\Autodesk\AutoCAD\R22.0') -or (Test-Path -LiteralPath $autocadPlugins))

if (-not $zwcadInstalled -and -not $autocadInstalled) {
    Write-Host "【错误】未检测到中望 CAD 2026（请先启动并关闭一次中望 CAD 2026 完成初始化），或发布包缺少 ZWCAD2026 目录。" -ForegroundColor Red
    return
}

$installedCount = 0

# 安装 ZWCAD 2026
if ($zwcadInstalled) {
    Write-Host ">> 检测到 中望 CAD 2026" -ForegroundColor Green
    $zwcadDest = Join-Path (Join-Path $env:LOCALAPPDATA 'FastBatchPlot') 'ZWCAD2026'
    $hasZwKey = $false
    $langs = Get-ChildItem -LiteralPath $zwcadKey | Where-Object { $_.PSChildName -match '^[a-z]{2}-[A-Z]{2}$' }
    foreach ($l in $langs) {
        if (Test-Path -LiteralPath (Join-Path $l.PSPath 'Applications\FastBatchPlot')) {
            $hasZwKey = $true
            break
        }
    }
    if ((Test-Path -LiteralPath $zwcadDest) -or $hasZwKey) {
        Write-Host "   检测到已存在旧版本，正在自动清理旧版..." -ForegroundColor DarkGray
        try { & (Join-Path $PSScriptRoot 'Install-ZWCAD.ps1') -Uninstall } catch {}
    }
    Write-Host "   正在安装 ZWCAD 2026 插件..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'Install-ZWCAD.ps1')
    Write-Host "   [✓] 中望 CAD 2026 插件安装成功！" -ForegroundColor Green
    $installedCount++
}

# 安装 AutoCAD 2018
if ($autocadInstalled) {
    Write-Host ">> 检测到 AutoCAD 2018" -ForegroundColor Green
    $acadDest = Join-Path $autocadPlugins 'FastBatchPlot.bundle'
    if (Test-Path -LiteralPath $acadDest) {
        Write-Host "   检测到已存在旧版本，正在自动清理旧版..." -ForegroundColor DarkGray
        try { & (Join-Path $PSScriptRoot 'Install-AutoCAD.ps1') -Uninstall } catch {}
    }
    Write-Host "   正在安装 AutoCAD 2018 插件..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'Install-AutoCAD.ps1')
    Write-Host "   [✓] AutoCAD 2018 插件安装成功！" -ForegroundColor Green
    $installedCount++
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "【安装成功】已为 $installedCount 个 CAD 环境完成配置！" -ForegroundColor Green
Write-Host "使用方法：" -ForegroundColor White
Write-Host "  1. 打开中望 CAD 2026" -ForegroundColor White
Write-Host "  2. 在命令行直接输入【BP】即可启动批打印！(无需手动 NETLOAD)" -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Green

try {
    Add-Type -AssemblyName System.Windows.Forms
    $msg = "🎉 FastBatchPlot 批打印插件安装成功！`n`n已为 $installedCount 个 CAD 环境完成配置：`n"
    if ($zwcadInstalled) { $msg += "  • 中望 CAD 2026 (已注册自动加载)`n" }
    if ($autocadInstalled) { $msg += "  • AutoCAD 2018 (已配置 Bundle 插件)`n" }
    $msg += "`n【使用方法】`n启动 CAD 后，在命令行直接输入【BP】即可打开批打印界面！"
    [System.Windows.Forms.MessageBox]::Show($msg, "FastBatchPlot 批打印 - 安装成功", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
} catch {}

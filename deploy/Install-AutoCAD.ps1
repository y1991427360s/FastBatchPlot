[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('2018')][string]$TargetVersion = '2018',
    [switch]$Uninstall
)
. (Join-Path $PSScriptRoot 'Install-Common.ps1')
$parent = Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins'
$destination = Join-Path $parent 'FastBatchPlot.bundle'
if ($Uninstall) {
    if (-not (Test-Path -LiteralPath $destination)) { Write-Output '未找到本插件安装目录。'; return }
    Assert-OwnedDirectory $destination
    if ($PSCmdlet.ShouldProcess($destination, '卸载当前用户的 AutoCAD 2018 插件')) {
        Assert-CadClosed 'acad'
        Remove-OwnedDirectory $destination $parent
        Write-Output '已卸载插件；用户配置未删除。'
    }
    return
}
Assert-Package $PSScriptRoot 'AutoCAD2018'
if (Test-Path -LiteralPath $destination) { throw "目标目录已存在，请先卸载；旧版无安装标记目录需手动备份处理：$destination" }
if ($PSCmdlet.ShouldProcess($destination, '安装当前用户的 AutoCAD 2018 插件（仅 R22.0）')) {
    Assert-CadClosed 'acad'
    Copy-NewInstallation (Join-Path $PSScriptRoot 'AutoCAD2018\FastBatchPlot.bundle') $destination 'AutoCAD2018'
    Write-Output "已复制到：$destination。未启动 CAD，原生加载和打印验收待完成。"
}

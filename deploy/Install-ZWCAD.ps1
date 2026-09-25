[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('2026')][string]$TargetVersion = '2026',
    [switch]$Uninstall
)
. (Join-Path $PSScriptRoot 'Install-Common.ps1')
$parent = Join-Path $env:LOCALAPPDATA 'FastBatchPlot'
$destination = Join-Path $parent 'ZWCAD2026'
$loader = Join-Path $destination 'Contents\FastBatchPlot.ZWCAD.dll'
$yearKey = 'HKCU:\Software\ZWSOFT\ZWCAD\2026'
$languages = @(if (Test-Path -LiteralPath $yearKey) { Get-ChildItem -LiteralPath $yearKey | Where-Object { $_.PSChildName -match '^[a-z]{2}-[A-Z]{2}$' } })
if ($Uninstall) {
    if (Test-Path -LiteralPath $destination) { Assert-OwnedDirectory $destination }
    $ownedKeys = @($languages | ForEach-Object {
        $key = Join-Path $_.PSPath 'Applications\FastBatchPlot'
        if (Test-Path -LiteralPath $key) {
            $entry = Get-ItemProperty -LiteralPath $key
            if ($entry.PSObject.Properties['LOADER'] -and $entry.LOADER -eq $loader) { $key }
        }
    })
    if ($PSCmdlet.ShouldProcess($destination, '移除当前用户 ZWCAD 2026 的本安装器文件及对应注册项')) {
        Assert-CadClosed 'ZWCAD'
        foreach ($key in $ownedKeys) { Remove-Item -LiteralPath $key -Recurse -Force }
        if (Test-Path -LiteralPath $destination) { Remove-OwnedDirectory $destination $parent }
        Write-Output '已卸载对应插件；用户配置及其他 LOADER 注册项未删除。'
    }
    return
}
Assert-Package $PSScriptRoot 'ZWCAD2026'
if ($languages.Count -eq 0) { throw '未找到当前用户 ZWCAD 2026 的语言配置。请自行启动并关闭 ZWCAD 2026 完成首次初始化，或使用发布包手动 NETLOAD。' }
if (Test-Path -LiteralPath $destination) { throw "目标目录已存在，请先卸载：$destination" }
foreach ($language in $languages) {
    $key = Join-Path $language.PSPath 'Applications\FastBatchPlot'
    if (Test-Path -LiteralPath $key) { throw "已有同名插件注册项，请先核对并处理旧安装，脚本不会覆盖：$key" }
}
if ($PSCmdlet.ShouldProcess($destination, '复制发布包并仅向 HKCU 的 ZWCAD 2026 语言项注册')) {
    Assert-CadClosed 'ZWCAD'
    Copy-NewInstallation (Join-Path $PSScriptRoot 'ZWCAD2026') $destination 'ZWCAD2026'
    foreach ($language in $languages) {
        $key = Join-Path $language.PSPath 'Applications\FastBatchPlot'
        New-Item -Path $key -Force | Out-Null
        New-ItemProperty -LiteralPath $key -Name 'LOADER' -Value $loader -PropertyType String -Force | Out-Null
        New-ItemProperty -LiteralPath $key -Name 'LOADCTRLS' -Value 14 -PropertyType DWord -Force | Out-Null
        New-ItemProperty -LiteralPath $key -Name 'MANAGED' -Value 1 -PropertyType DWord -Force | Out-Null
        New-ItemProperty -LiteralPath $key -Name 'DESCRIPTION' -Value 'FastBatchPlot ZWCAD 2026' -PropertyType String -Force | Out-Null
        $actual = Get-ItemProperty -LiteralPath $key
        if ($actual.LOADER -ne $loader -or $actual.LOADCTRLS -ne 14 -or $actual.MANAGED -ne 1) { throw "注册后校验失败：$key" }
    }
    Write-Output "已部署到：$destination。未启动 CAD，原生加载和打印验收待完成。"
}

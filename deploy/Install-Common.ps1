Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Assert-Package([string]$Root, [string]$Target) {
    $manifestPath = Join-Path $Root 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw '仅可从 Build-Release.ps1 生成的完整发布目录安装；请勿使用源码 deploy 目录。' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.product -ne 'FastBatchPlot' -or $manifest.schemaVersion -ne 1 -or $manifest.configuration -ne 'Release' -or $Target -notin $manifest.targets) { throw '发布清单不匹配。' }
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $listed = @{}
    foreach ($entry in $manifest.files) {
        $full = [IO.Path]::GetFullPath((Join-Path $rootFull $entry.path))
        if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { throw "清单路径越界：$($entry.path)" }
        if ($listed.ContainsKey($full)) { throw "重复清单项：$($entry.path)" }
        $listed[$full] = $true
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "缺少文件：$($entry.path)" }
        $file = Get-Item -LiteralPath $full
        if ($file.Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ne $entry.sha256) { throw "文件校验失败：$($entry.path)" }
    }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $Root $Target) -Recurse -File) {
        if (-not $listed.ContainsKey($file.FullName)) { throw "包含未登记文件：$($file.FullName)" }
    }
    $hostName = if ($Target -eq 'AutoCAD2018') { 'AutoCAD' } else { 'ZWCAD' }
    $contents = if ($Target -eq 'AutoCAD2018') { 'AutoCAD2018\FastBatchPlot.bundle\Contents' } else { 'ZWCAD2026\Contents' }
    foreach ($name in @("FastBatchPlot.$hostName.dll", 'FastBatchPlot.Core.dll', 'FastBatchPlot.CadBridge.dll', 'FastBatchPlot.UI.dll', 'PdfSharpCore.dll', 'SixLabors.ImageSharp.dll', 'SixLabors.Fonts.dll')) {
        $required = Join-Path (Join-Path $Root $contents) $name
        if (-not $listed.ContainsKey([IO.Path]::GetFullPath($required))) { throw "清单缺少必要依赖：$name" }
    }
}
function Assert-InstalledCopy([string]$Source, [string]$Destination) {
    $prefix = [IO.Path]::GetFullPath($Source).TrimEnd('\') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relative = $file.FullName.Substring($prefix.Length)
        $installed = Join-Path $Destination $relative
        if (-not (Test-Path -LiteralPath $installed -PathType Leaf)) { throw "安装后缺少文件：$installed" }
        if ((Get-Item -LiteralPath $installed).Length -ne $file.Length -or (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "安装后文件校验失败：$installed" }
    }
}
function Assert-CadClosed([string]$ProcessName) {
    if (Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) { throw '请先自行保存图纸并关闭对应 CAD 后再安装或卸载；脚本不会关闭 CAD。' }
}
function Assert-OwnedDirectory([string]$Path) {
    $receipt = Join-Path $Path '.fastbatchplot-install.json'
    if (-not (Test-Path -LiteralPath $receipt)) { throw "目录缺少本安装器标记，拒绝覆盖或删除：$Path" }
    $record = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
    if ($record.product -ne 'FastBatchPlot' -or $record.installPath -ne [IO.Path]::GetFullPath($Path)) { throw "安装标记不匹配：$Path" }
}
function Remove-OwnedDirectory([string]$Path, [string]$AllowedParent) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $parent = [IO.Path]::GetFullPath($AllowedParent).TrimEnd('\') + '\'
    if (-not $full.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or $full -eq $parent.TrimEnd('\')) { throw "删除目录越界：$full" }
    Assert-OwnedDirectory $full
    Remove-Item -LiteralPath $full -Recurse -Force
}
function Copy-NewInstallation([string]$Source, [string]$Destination, [string]$Target) {
    if (Test-Path -LiteralPath $Destination) { throw "安装目录已存在。请先使用 -Uninstall 卸载当前版本（用户配置会保留）：$Destination" }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    # 先写标记，复制中断后也可用卸载命令清理。
    [ordered]@{ product = 'FastBatchPlot'; target = $Target; installPath = [IO.Path]::GetFullPath($Destination); installedUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination '.fastbatchplot-install.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force }
    Assert-InstalledCopy $Source $Destination
}

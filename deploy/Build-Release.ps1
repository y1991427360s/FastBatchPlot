[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$AutoCADSdkPath,
    [string]$ZWCADSdkPath,
    # 当前交付目标为中望 CAD；需要同时打包 AutoCAD 2018 版本时加 -IncludeAutoCAD。
    [switch]$IncludeAutoCAD
)
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $PSScriptRoot '..\artifacts'
}
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$started = Get-Date
$package = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ('FastBatchPlot-' + $started.ToString('yyyyMMdd-HHmmss-fff'))
if (Test-Path -LiteralPath $package) { throw "输出目录已存在：$package" }
New-Item -ItemType Directory -Path $package | Out-Null
try {
    $hosts = if ($IncludeAutoCAD) { @('AutoCAD', 'ZWCAD') } else { @('ZWCAD') }
    foreach ($hostName in $hosts) {
        # 每次使用全新的编译输出目录，避免历史 bin 里的无关 DLL 混入发布包。
        $source = Join-Path ($package + '.build') $hostName
        if (Test-Path -LiteralPath $source) { throw "临时编译目录已存在：$source" }
        $buildArgs = @('build', (Join-Path $repo "src\FastBatchPlot.$hostName\FastBatchPlot.$hostName.csproj"), '-c', 'Release', '-t:Rebuild', '--nologo', '--output', $source)
        if ($AutoCADSdkPath) { $buildArgs += "-p:AutoCADSdkPath=$AutoCADSdkPath" }
        if ($ZWCADSdkPath) { $buildArgs += "-p:ZWCADSdkPath=$ZWCADSdkPath" }
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "$hostName Release 构建失败，退出码：$LASTEXITCODE" }
        foreach ($assembly in @("FastBatchPlot.$hostName.dll", 'FastBatchPlot.Core.dll', 'FastBatchPlot.CadBridge.dll', 'FastBatchPlot.UI.dll')) {
            $main = Get-Item -LiteralPath (Join-Path $source $assembly)
            if ($main.LastWriteTimeUtc -lt $started.ToUniversalTime()) { throw "项目程序集未在本次构建更新：$($main.FullName)" }
        }
        $target = if ($hostName -eq 'AutoCAD') { Join-Path $package 'AutoCAD2018\FastBatchPlot.bundle\Contents' } else { Join-Path $package 'ZWCAD2026\Contents' }
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        # 复制依赖和卫星资源；不分发 CAD SDK 或调试符号。
        foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
            if ($file.Extension -notin @('.dll', '.config')) { continue }
            if ($file.Name -match '^(acmgd|acdbmgd|accoremgd|ZwManaged|ZwDatabaseMgd)\.dll$') { throw "构建目录包含不应分发的 CAD SDK：$($file.FullName)" }
            $relative = $file.FullName.Substring($source.Length).TrimStart('\')
            $dest = Join-Path $target $relative
            New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $dest
        }
        foreach ($required in @("FastBatchPlot.$hostName.dll", 'FastBatchPlot.Core.dll', 'FastBatchPlot.CadBridge.dll', 'FastBatchPlot.UI.dll', 'PdfSharpCore.dll', 'SixLabors.ImageSharp.dll', 'SixLabors.Fonts.dll')) {
            if (-not (Test-Path -LiteralPath (Join-Path $target $required))) { throw "缺少依赖：$required" }
        }
    }
    if ($IncludeAutoCAD) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FastBatchPlot.bundle\PackageContents.xml') -Destination (Join-Path $package 'AutoCAD2018\FastBatchPlot.bundle\PackageContents.xml')
    }
    $scripts = @('Install-ZWCAD.ps1', 'Install-Common.ps1', 'Install-All.ps1', '一键安装.bat', '一键卸载.bat', 'LoadFastBatchPlot.lsp')
    if ($IncludeAutoCAD) { $scripts += 'Install-AutoCAD.ps1' }
    foreach ($name in $scripts) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package
    }
    Copy-Item -LiteralPath (Join-Path $repo 'docs\PORTABLE_DEPLOYMENT.md') -Destination (Join-Path $package '部署说明.md')
    Copy-Item -LiteralPath (Join-Path $repo 'docs\THIRD_PARTY_NOTICES.md') -Destination (Join-Path $package 'THIRD_PARTY_NOTICES.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination (Join-Path $package 'licenses') -Recurse
    $licenseTargets = if ($IncludeAutoCAD) { @('AutoCAD2018\FastBatchPlot.bundle', 'ZWCAD2026') } else { @('ZWCAD2026') }
    foreach ($licenseTarget in $licenseTargets) {
        $licenseRoot = Join-Path $package $licenseTarget
        Copy-Item -LiteralPath (Join-Path $repo 'docs\THIRD_PARTY_NOTICES.md') -Destination $licenseRoot
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination (Join-Path $licenseRoot 'licenses') -Recurse
    }
    $files = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = $_.FullName.Substring($package.Length + 1).Replace('\', '/'); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; lastWriteTimeUtc = $_.LastWriteTimeUtc.ToString('o') }
    })
    $manifest = [ordered]@{ schemaVersion = 1; product = 'FastBatchPlot'; configuration = 'Release'; buildStartedUtc = $started.ToUniversalTime().ToString('o'); packagedUtc = [DateTime]::UtcNow.ToString('o'); targets = @($(if ($IncludeAutoCAD) { 'AutoCAD2018' }) + 'ZWCAD2026' | Where-Object { $_ }); nativeAcceptance = 'pending'; files = $files }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'manifest.json') -Encoding UTF8
    Write-Output "PACKAGE_CREATED=$package"
    Get-ChildItem -LiteralPath $package -Recurse -Filter 'FastBatchPlot.*.dll' | Select-Object FullName, LastWriteTime, Length
    Write-Warning '仅完成构建与打包。未启动、连接或验证 CAD；原生加载和打印验收仍待明确授权。'
} catch {
    # 保留失败目录供诊断，失败目录不会有可安装的 manifest.json。
    throw
}

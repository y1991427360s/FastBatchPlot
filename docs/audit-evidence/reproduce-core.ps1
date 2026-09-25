param(
    [string]$ProjectRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

# 仅加载纯 Core 程序集，不连接 CAD，不修改产品源码和用户图纸。
$ErrorActionPreference = 'Stop'
$assemblyPath = Join-Path $ProjectRoot 'src/FastBatchPlot.Core/bin/Release/net8.0/FastBatchPlot.Core.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    $assemblyPath = Join-Path $ProjectRoot 'src/FastBatchPlot.Core/bin/Debug/net8.0/FastBatchPlot.Core.dll'
}
Add-Type -Path $assemblyPath
$observations = [System.Collections.Generic.List[object]]::new()

$observations.Add([pscustomobject]@{
    Case = 'ACI 调色板'; Expected = 'ACI 10=(255,0,0)，ACI 30=(255,127,0)'
    Actual = "ACI 10=$([FastBatchPlot.Core.Common.AciColorHelper]::GetAciRgb(10)); ACI 30=$([FastBatchPlot.Core.Common.AciColorHelper]::GetAciRgb(30)); RGB(255,127,0) -> ACI $([FastBatchPlot.Core.Common.AciColorHelper]::RgbToAci(255,127,0))"
})

$block = [FastBatchPlot.Core.Detection.RawBlockCandidate]::new()
$block.Bounds = [FastBatchPlot.Core.Common.Rect2D]::new(0,0,84100,59400)
$blocks = [System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawBlockCandidate]]::new()
$blocks.Add($block)
$observations.Add([pscustomobject]@{
    Case = '模型单位定义图块以 1 倍插入'; Expected = '识别 1 个 A1 1:100 图框'
    Actual = "识别 $([FastBatchPlot.Core.Detection.BlockFrameDetector]::ProcessBlockCandidates($blocks,0).Count) 个图框"
})

$poly = [FastBatchPlot.Core.Detection.RawPolylineCandidate]::new()
foreach ($xy in @(@(0,0),@(420,0),@(0,297),@(420,297))) {
    $poly.Vertices.Add([FastBatchPlot.Core.Common.Point2D]::new($xy[0],$xy[1]))
}
$polys = [System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawPolylineCandidate]]::new()
$polys.Add($poly)
$observations.Add([pscustomobject]@{
    Case = '自交蝴蝶多段线'; Expected = '识别 0 个矩形图框'
    Actual = "识别 $([FastBatchPlot.Core.Detection.PolylineFrameDetector]::FilterAndCreateFrames($polys,0).Count) 个图框"
})

$polys.Clear()
$inner = [FastBatchPlot.Core.Detection.RawPolylineCandidate]::new()
$inner.Handle = 'valid_A3'
foreach ($xy in @(@(10,20),@(430,20),@(430,317),@(10,317))) {
    $inner.Vertices.Add([FastBatchPlot.Core.Common.Point2D]::new($xy[0],$xy[1]))
}
$polys.Add($inner)
$innerAloneCount = [FastBatchPlot.Core.Detection.PolylineFrameDetector]::FilterAndCreateFrames($polys,0).Count
$outer = [FastBatchPlot.Core.Detection.RawPolylineCandidate]::new()
$outer.Handle = 'invalid_outer'
foreach ($xy in @(@(0,0),@(440,0),@(440,350),@(0,350))) {
    $outer.Vertices.Add([FastBatchPlot.Core.Common.Point2D]::new($xy[0],$xy[1]))
}
$polys.Add($outer)
$observations.Add([pscustomobject]@{
    Case = '有效 A3 外围另有不合规矩形'; Expected = '两种情况都应保留 1 个有效 A3 图框'
    Actual = "单独 A3=$innerAloneCount; 添加 440x350 外矩形后=$([FastBatchPlot.Core.Detection.PolylineFrameDetector]::FilterAndCreateFrames($polys,0).Count)"
})

$frame = [FastBatchPlot.Core.Models.PlotFrame]::new()
$frame.TitleInfo.DrawingName = '预算$&版'
$observations.Add([pscustomobject]@{
    Case = '命名模板中的原始属性值'; Expected = '预算$&版'
    Actual = [FastBatchPlot.Core.Naming.DrawingNameFormatter]::Format('{DwgName}',$frame,'demo.dwg')
})
$frame.CalculatedScale = 2.5
$observations.Add([pscustomobject]@{
    Case = '非整数出图比例的文件命名'; Expected = '1-2.5'
    Actual = [FastBatchPlot.Core.Naming.DrawingNameFormatter]::Format('{Scale}',$frame,'demo.dwg')
})
$observations | ConvertTo-Json -Depth 4

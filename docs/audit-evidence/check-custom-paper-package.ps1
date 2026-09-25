param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$frame=[FastBatchPlot.Core.Models.PlotFrame]::new()
$frame.MaxX=42000;$frame.MaxY=29700;$frame.CalculatedScale=100
$frames=[System.Collections.Generic.List[FastBatchPlot.Core.Models.PlotFrame]]::new();$frames.Add($frame)
$config=[FastBatchPlot.Core.Models.PlotConfig]::new()
$form=[FastBatchPlot.UI.Views.CustomPaperForm]::new($frames,$config)
try{
    $form.CreateControl()
    $candidate=[FastBatchPlot.Core.Planning.CustomPaperEdit]::Preview($frame,200.125,400.25,$true)
    $plan=[FastBatchPlot.Core.Planning.PlotPlanBuilder]::Create($candidate,$config)
    if($plan.PaperWidthMm -ne 200.125 -or $plan.PaperHeightMm -ne 400.25 -or $candidate.IsLandscape){throw 'Custom paper mismatch'}
    Write-Output ('PACKAGE_NET48_CUSTOM_PAPER_OK='+$Contents)
}finally{$form.Dispose()}

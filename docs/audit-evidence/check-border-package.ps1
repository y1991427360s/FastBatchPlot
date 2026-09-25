param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$f=[FastBatchPlot.Core.Models.PlotFrame]::new();$f.MinX=100;$f.MinY=200;$f.MaxX=42100;$f.MaxY=29900;$f.CalculatedScale=100;$f.IsLandscape=$true
$f.DetectedPaper=[FastBatchPlot.Core.Models.PaperSize]::new('A3',420,297,$true)
$config=[FastBatchPlot.Core.Models.PlotConfig]::new();$config.PrintOuterBorderLine=$false;$config.OuterBorderInsetMm=0.6
$plan=[FastBatchPlot.Core.Planning.PlotPlanBuilder]::Create($f,$config)
if($plan.MinX -ne 160 -or $plan.MaxX -ne 42040 -or $plan.ScaleDenominator -ne 100 -or $plan.PaperWidthMm -ne 420){throw 'Wrong border geometry'}
$frames=[System.Collections.Generic.List[FastBatchPlot.Core.Models.PlotFrame]]::new();$frames.Add($f)
$form=[FastBatchPlot.UI.Views.PageMarginsForm]::new([FastBatchPlot.Core.Models.PageMargins]::new(),0,$frames,$false,0.6)
try {
 $form.CreateControl()
 if($form.PrintOuterBorderLine -or $form.OuterBorderInsetMm -ne 0.6){throw 'Wrong border UI values'}
 $preview=$form.GetType().GetField('preview',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($form)
 if($preview.Rows[0].Cells[2].Value -ne '1:100' -or $preview.Rows[0].Cells[5].Value -notlike '*0.6*'){throw 'Wrong border UI preview'}
 Write-Output 'PACKAGE_NET48_BORDER_OK=paper and scale preserved; preview uses same plan'
} finally {$form.Dispose()}

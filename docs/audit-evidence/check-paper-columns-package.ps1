param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
try {
 $form.CreateControl();$flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $frame=[FastBatchPlot.Core.Models.PlotFrame]::new()
 $frame.MaxX=42000;$frame.MaxY=29700;$frame.CalculatedScale=100;$frame.IsLandscape=$true
 $frame.DetectedPaper=[FastBatchPlot.Core.Models.PaperSize]::new('A3',420,297,$true)
 $frames=$form.GetType().GetField('_frames',$flags).GetValue($form);$frames.Add($frame)
 $form.GetType().GetMethod('RefreshGrid',$flags).Invoke($form,@())
 $grid=$form.GetType().GetField('dgvDrawings',$flags).GetValue($form)
 $margin=$form.GetType().GetField('numMargin',$flags).GetValue($form);$margin.Value=2
 if($grid.Rows[0].Cells['PdfPaperSize'].Value -notlike '424*301'){throw 'Live paper size mismatch'}
 $grid.Rows[0].Cells['BasePaperSize'].Value='500x350'
 if($frame.DetectedPaper.WidthMm -ne 500 -or $grid.Rows[0].Cells['PdfPaperSize'].Value -notlike '504*354'){throw 'Paper edit did not reach plan'}
 $grid.Rows[0].Cells['BasePaperSize'].Value='100x100'
 if($frame.DetectedPaper.WidthMm -ne 500){throw 'Invalid paper edit was committed'}
 Write-Output 'PACKAGE_NET48_PAPER_COLUMNS_OK'
} finally {$form.Dispose()}

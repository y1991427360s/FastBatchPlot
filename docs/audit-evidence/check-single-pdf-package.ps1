param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('PdfSharpCore.dll','FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
$source=[FastBatchPlot.Core.Pdf.SinglePdfFile]::CreateTemporaryPath()
$target=Join-Path (Split-Path $source -Parent) 'saved.pdf'
try {
 $form.CreateControl();$flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $button=$form.GetType().GetField('btnSinglePdf',$flags).GetValue($form)
 if($button.Text -notlike '*PDF*'){throw 'Single PDF entry missing'}
 $document=[PdfSharpCore.Pdf.PdfDocument]::new()
 try {$null=$document.AddPage();$document.Save($source)} finally {$document.Dispose()}
 [FastBatchPlot.Core.Pdf.SinglePdfFile]::Validate($source)
 [FastBatchPlot.Core.Pdf.SinglePdfFile]::SaveCopy($source,$target,$false)
 if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw 'PDF bytes changed'}
 Write-Output 'PACKAGE_NET48_SINGLE_PDF_OK'
} finally {
 $form.Dispose()
 if(Test-Path -LiteralPath $source){[IO.File]::Delete($source)}
 if(Test-Path -LiteralPath $target){[IO.File]::Delete($target)}
 [IO.Directory]::Delete((Split-Path $source -Parent),$false)
}

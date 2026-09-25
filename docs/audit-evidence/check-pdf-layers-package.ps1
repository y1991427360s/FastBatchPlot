param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$InputRoot,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'PdfSharpCore.dll')
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$items=New-Object 'System.Collections.Generic.List[FastBatchPlot.Core.Pdf.PdfMergeItem]'
$items.Add((New-Object FastBatchPlot.Core.Pdf.PdfMergeItem((Join-Path $InputRoot 'source-1.pdf'),'Drawing 1')))
$items.Add((New-Object FastBatchPlot.Core.Pdf.PdfMergeItem((Join-Path $InputRoot 'source-2.pdf'),'Drawing 2')))
$mergeError=''
if(-not [FastBatchPlot.Core.Pdf.PdfMerger]::MergePdfFiles($items,$Output,[ref]$mergeError,$false)){throw $mergeError}
$pdf=[PdfSharpCore.Pdf.IO.PdfReader]::Open($Output,[PdfSharpCore.Pdf.IO.PdfDocumentOpenMode]::Import)
try {
    $properties=$pdf.Internals.Catalog.Elements.GetDictionary('/OCProperties')
    if($null -eq $properties){throw 'Layer catalog missing'}
    $groups=$properties.Elements.GetArray('/OCGs')
    $config=$properties.Elements.GetDictionary('/D')
    if($pdf.PageCount -ne 2 -or $groups.Elements.Count -ne 2 -or $config.Elements.GetArray('/OFF').Elements.Count -ne 2){throw 'Layer states changed'}
    Write-Output ('PACKAGE_NET48_PDF_LAYERS_OK='+$Output)
} finally {$pdf.Dispose()}

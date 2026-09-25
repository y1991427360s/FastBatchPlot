param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$folder=Join-Path ([IO.Path]::GetTempPath()) ('FbpSource-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
try {
    $frame=[FastBatchPlot.Core.Models.PlotFrame]::new()
    $frame.SourceFileName='source-original.dwg';$frame.SourceDocumentId='offline-document';$frame.SourceLayoutId='model';$frame.LayoutName='layout-A'
    $frame.MaxX=420;$frame.MaxY=297;$frame.CalculatedScale=1
    if([FastBatchPlot.Core.Naming.DrawingNameFormatter]::Format('{DwgFileName}_{Layout}',$frame,'wrong.dwg') -ne 'source-original_layout-A'){throw 'Source name mismatch'}
    $config=[FastBatchPlot.Core.Models.PlotConfig]::new();$config.OutputDirectory=$folder;$config.MergeToSinglePdf=$false;$config.BookmarkTemplate='{DwgFileName}/{Layout}'
    $pages=[System.Collections.Generic.List[FastBatchPlot.Core.Tasks.BatchPage]]::new()
    $pages.Add([FastBatchPlot.Core.Tasks.BatchPage]::new($frame,(Join-Path $folder 'page.pdf')))
    $run=[FastBatchPlot.Core.Tasks.BatchPlotRun]::new($pages,$config)
    $record=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Create($run,'')
    $history=Join-Path $folder 'task.json';[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Save($history,$record)
    $loaded=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Load($history)
    if($loaded.Pages[0].Frame.SourceFileName -ne 'source-original.dwg'){throw 'History source mismatch'}
    $items=[FastBatchPlot.Core.Pdf.PdfBookmarkItems]::Create([FastBatchPlot.Core.Tasks.PdfTaskHistory]::PendingPages($loaded),$loaded.Config)
    if($items[0].BookmarkTitle -ne 'source-original/layout-A'){throw 'History bookmark mismatch'}
    Write-Output ('PACKAGE_NET48_SOURCE_NAMING_OK='+$Contents)
}finally{
    foreach($name in @('task.json','task.json.lock')){$path=Join-Path $folder $name;if(Test-Path -LiteralPath $path){[IO.File]::Delete($path)}}
    [IO.Directory]::Delete($folder,$false)
}

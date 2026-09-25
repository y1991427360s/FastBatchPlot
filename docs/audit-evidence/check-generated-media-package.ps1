param(
    [Parameter(Mandatory=$true)][string]$Contents,
    [Parameter(Mandatory=$true)][string]$SourcePc5
)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$sourceBefore=Get-Item -LiteralPath $SourcePc5
$sourceTime=$sourceBefore.LastWriteTimeUtc
$sourceHash=(Get-FileHash -LiteralPath $SourcePc5 -Algorithm SHA256).Hash
$folder=Join-Path ([IO.Path]::GetTempPath()) ('PdfMediaPackageCheck-'+[Guid]::NewGuid().ToString('N'))
$files=$null
try {
    $files=[FastBatchPlot.Core.Printing.ZwPdfMediaFiles]::Create($SourcePc5,634,301,$folder)
    $gbk=[Text.Encoding]::GetEncoding(936,[Text.EncoderFallback]::ExceptionFallback,[Text.DecoderFallback]::ExceptionFallback)
    $pc5=[IO.File]::ReadAllText($files.ConfigurationPath,$gbk)
    $pmp=[IO.File]::ReadAllText($files.PmpPath,$gbk)
    if(-not $pc5.Contains('pmp_filepath='+$files.PmpPath) -or -not $pmp.Contains('size_x0=634.000000') -or
        -not $pmp.Contains('size_y0=301.000000') -or -not $pmp.Contains('userdef_num=1')){throw 'Generated media fields do not match'}
    $frame=New-Object FastBatchPlot.Core.Models.PlotFrame
    $frame.MaxX=630; $frame.MaxY=297; $frame.CalculatedScale=1; $frame.IsLandscape=$true
    $frame.DetectedPaper=New-Object FastBatchPlot.Core.Models.PaperSize('extended',630,297,$true)
    $config=New-Object FastBatchPlot.Core.Models.PlotConfig
    $config.MarginMm=2
    $plan=[FastBatchPlot.Core.Planning.PlotPlanBuilder]::Create($frame,$config)
    $media=New-Object FastBatchPlot.Core.Planning.PlotMedia
    $media.Name=$files.MediaName; $media.WidthMm=634; $media.HeightMm=301
    $media.PrintableWidthMm=634; $media.PrintableHeightMm=301
    [FastBatchPlot.Core.Planning.GeneratedPdfMediaGuard]::Validate($plan,$files.ConfigurationPath,$files.ConfigurationPath,$files.MediaName,$media)
    $rejected=$false
    try{[FastBatchPlot.Core.Planning.GeneratedPdfMediaGuard]::Validate($plan,$files.ConfigurationPath,$SourcePc5,$files.MediaName,$media)}catch{$rejected=$true}
    if(-not $rejected){throw 'Wrong loaded configuration was accepted'}
    foreach($path in @($files.ConfigurationPath,$files.PmpPath)) {
        $locked=$false
        try{$write=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Write);$write.Dispose()}catch [IO.IOException]{$locked=$true}
        if(-not $locked){throw 'Generated media file was writable while in use'}
    }
    $configuration=$files.ConfigurationPath
    $files.Dispose();$files=$null
    if(Test-Path -LiteralPath $configuration){throw 'Generated configuration was not cleaned'}
    if((Get-FileHash -LiteralPath $SourcePc5 -Algorithm SHA256).Hash -ne $sourceHash -or
        (Get-Item -LiteralPath $SourcePc5).LastWriteTimeUtc -ne $sourceTime){throw 'Source PC5 changed'}
    Write-Output ('PACKAGE_NET48_GENERATED_MEDIA_OK='+$Contents)
    Write-Output ('READ_ONLY_SOURCE='+$SourcePc5+'; SHA256='+$sourceHash)
} finally {
    if($null -ne $files){$files.Dispose()}
    if(Test-Path -LiteralPath $folder){[IO.Directory]::Delete($folder,$false)}
}

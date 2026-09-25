param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$source='D:\Autodesk\CAD2018\AutoCAD 2018\UserDataCache\Plotters\DWG To PDF.pc3'
$before=(Get-FileHash -LiteralPath $source).Hash
$beforeTime=(Get-Item -LiteralPath $source).LastWriteTimeUtc
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('AcadMediaPackage-'+[Guid]::NewGuid().ToString('N'))
$files=$null
try {
    [IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
    $files=[FastBatchPlot.Core.Printing.AcadPdfMediaFiles]::Create($source,634.25,301.125,$temporary)
    $pc3=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($files.ConfigurationPath)
    $pmp=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($files.PmpPath)
    if($pc3.Root.Child('meta').Text('user_defined_model_pathname') -ne $files.PmpPath){throw 'PMP link mismatch'}
    $media=$pmp.Root.Child('udm').Child('media')
    if($media.Child('description').Child('0').Raw('media_bounds_urx') -ne '634.250000'){throw 'Paper size mismatch'}
    foreach($path in @($files.ConfigurationPath,$files.PmpPath)) {
        $locked=$false
        try{$write=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Write);$write.Dispose()}catch [IO.IOException]{$locked=$true}
        if(-not $locked){throw 'Generated configuration not protected'}
        Copy-Item -LiteralPath $path -Destination $EvidenceRoot
    }
    $files.Dispose();$files=$null
    if((Get-FileHash -LiteralPath $source).Hash -ne $before -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $beforeTime){throw 'Source changed'}
    Write-Output ('PACKAGE_NET48_ACAD_MEDIA_OK='+$Contents)
    Write-Output ('READ_ONLY_SOURCE_SHA256='+$before)
} finally {
    if($null -ne $files){$files.Dispose()}
    if(Test-Path -LiteralPath $temporary){[IO.Directory]::Delete($temporary,$false)}
}

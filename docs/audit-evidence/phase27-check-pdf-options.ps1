param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('FbpPdfOptions-'+[Guid]::NewGuid().ToString('N'))
$records=@()
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
function Get-IniFields([string]$Path) {
    $fields=@{};$section=''
    foreach($line in [IO.File]::ReadAllLines($Path,[Text.Encoding]::GetEncoding(936))) {
        if($line -match '^\[(.+)\]$'){$section=$Matches[1]}
        elseif($line -match '^([^=]+)=(.*)$'){$fields[$section+'/'+$Matches[1]]=$Matches[2]}
    }
    return $fields
}
function Get-Custom($configuration,[string]$name) {
    $found=@($configuration.Root.Child('custom').Children.Values | Where-Object {$_.Text('name') -eq $name})
    if($found.Count -ne 1){throw ('Missing/duplicate custom: '+$name)}
    return $found[0].Raw('value')
}
try {
    foreach($name in @('DWG to PDF','ZWCAD PDF(General Documentation)','ZWCAD PDF(High Quality Print)','ZWCAD PDF(Smallest File)','ZWCAD PDF(Web and Mobile)')) {
        $source=Join-Path 'D:\ZWCAD2026\UserDataCache\zh-CN\Plotters' ($name+'.pc5')
        $hash=(Get-FileHash -LiteralPath $source).Hash;$time=(Get-Item -LiteralPath $source).LastWriteTimeUtc
        $original=Get-IniFields $source
        $options=[FastBatchPlot.Core.Models.PdfOutputOptions]::new();$options.VectorResolutionDpi=1200;$options.RasterResolutionDpi=300;$options.TextToGeometry=$true;$options.IncludeLayers=$false;$options.MergeLines=$true
        $files=$null
        try {
            $files=[FastBatchPlot.Core.Printing.ZwPdfMediaFiles]::Create($source,634.25,301.125,$temporary,$options)
            $generated=Get-IniFields $files.ConfigurationPath
            foreach($key in $original.Keys | Where-Object {$_ -notlike 'Meta/*'}) {
                $expected=$original[$key]
                if($key -in @('res_color_mem/resolution_x','res_color_mem/resolution_y')){$expected='1200'}
                if($key -eq 'res_color_mem/truetype_as_text'){$expected='0'}
                if($key -in @('res_color_mem/raster_resolution_x','res_color_mem/raster_resolution_y')){$expected='300'}
                if($key -eq 'Retain/layerinclude'){$expected='0'}
                if($key -eq 'res_color_mem/lines_overwrite'){$expected='0'}
                if($generated[$key] -ne $expected){throw ('Unexpected field: '+$key)}
            }
            foreach($key in @('res_color_mem/raster_resolution_x','res_color_mem/raster_resolution_y')) {
                if($generated[$key] -ne '300'){throw ('Missing raster setting: '+$key)}
            }
            if($generated['Retain/layerinclude'] -ne '0'){throw 'Missing layer setting'}
            if($generated['res_color_mem/lines_overwrite'] -ne '0'){throw 'Missing line merge setting'}
            Copy-Item -LiteralPath $files.ConfigurationPath -Destination (Join-Path $EvidenceRoot ($name+'.pc5'))
        } finally {if($null -ne $files){$files.Dispose()}}
        if((Get-FileHash -LiteralPath $source).Hash -ne $hash -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $time){throw 'Source changed'}
        $records += [ordered]@{source=$source;sha256=$hash;lastWriteTimeUtc=$time.ToString('o');result='PDF_OPTIONS_OK'}
        Write-Output ('PDF_OPTIONS_OK='+$name)
    }
    $source='D:\Autodesk\CAD2018\AutoCAD 2018\UserDataCache\Plotters\AutoCAD PDF (General Documentation).pc3'
    $hash=(Get-FileHash -LiteralPath $source).Hash;$time=(Get-Item -LiteralPath $source).LastWriteTimeUtc
    $original=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($source)
    $options=[FastBatchPlot.Core.Models.PdfOutputOptions]::new();$options.VectorResolutionDpi=2400;$options.RasterResolutionDpi=600;$options.TextToGeometry=$true;$options.IncludeLayers=$false;$options.MergeLines=$true
    $files=$null
    try {
        $files=[FastBatchPlot.Core.Printing.AcadPdfMediaFiles]::Create($source,634.25,301.125,$temporary,$options)
        $generated=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($files.ConfigurationPath)
        if($generated.Root.Child('res_color_mem').Raw('lines_overwrite') -ne 'FALSE'){throw 'Missing AutoCAD line merge setting'}
        $expected=@{Hardcopy_Resolution='2400';Raster_Limit='600';Monochrome_Raster_Limit='600';Custom_Raster_Resolution='TRUE';Custom_Monochrome_Resolution='TRUE';All_As_Geometry='TRUE';Include_Layer='FALSE'}
        foreach($entry in $original.Root.Child('custom').Children.Values) {
            $name=$entry.Text('name');$value=$entry.Raw('value');if($expected.ContainsKey($name)){$value=$expected[$name]}
            if((Get-Custom $generated $name) -ne $value){throw ('Unexpected custom: '+$name)}
        }
        foreach($field in @('phys_resolution_x','phys_resolution_y','effective_resolution_x','effective_resolution_y')) {
            if($generated.Root.Child('res_color_mem').Child('resolution').Raw($field) -ne '2400.0'){throw ('Unexpected resolution: '+$field)}
        }
        Copy-Item -LiteralPath $files.ConfigurationPath -Destination (Join-Path $EvidenceRoot 'AutoCAD-General.pc3')
    } finally {if($null -ne $files){$files.Dispose()}}
    if((Get-FileHash -LiteralPath $source).Hash -ne $hash -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $time){throw 'Source changed'}
    $records += [ordered]@{source=$source;sha256=$hash;lastWriteTimeUtc=$time.ToString('o');result='PDF_OPTIONS_OK'}
    Write-Output 'PDF_OPTIONS_OK=AutoCAD General Documentation'
    if(@(Get-ChildItem -LiteralPath $temporary -Force).Count -ne 0){throw 'Temporary files remain'}
    $records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceRoot 'sources.json') -Encoding UTF8
} finally {if(Test-Path -LiteralPath $temporary){[IO.Directory]::Delete($temporary,$false)}}

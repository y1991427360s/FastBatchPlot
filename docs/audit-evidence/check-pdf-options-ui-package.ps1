param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$options=[FastBatchPlot.Core.Models.PdfOutputOptions]::new()
$options.VectorResolutionDpi=1200
$options.TextToGeometry=$false
$options.RasterResolutionDpi=300
$options.IncludeLayers=$true
$options.MergeLines=$true
foreach($platform in @('AutoCAD','ZWCAD')){
    $form=[FastBatchPlot.UI.Views.PdfParametersForm]::new($options,$platform)
    try {
        $form.CreateControl()
        if($form.Value.VectorResolutionDpi -ne 1200 -or $form.Value.TextToGeometry -ne $false -or $form.Value.RasterResolutionDpi -ne 300 -or $form.Value.IncludeLayers -ne $true){throw 'Parameters changed'}
        $flags=[Reflection.BindingFlags]'Instance,NonPublic'
        foreach($name in @('raster','layers','apply')) {
            if(-not $form.GetType().GetField($name,$flags).GetValue($form).Enabled){throw ('Unavailable control: '+$name)}
        }
        if($form.Value.MergeLines -ne $true -or -not $form.GetType().GetField('mergeLines',$flags).GetValue($form).Enabled){throw 'Line merge unavailable'}
        Write-Output ('PACKAGE_NET48_PDF_OPTIONS_UI_OK='+$platform)
    } finally {$form.Dispose()}
}

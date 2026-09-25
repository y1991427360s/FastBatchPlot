param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach ($assembly in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')) {
    Add-Type -Path (Join-Path $Contents $assembly)
}
$form = New-Object FastBatchPlot.UI.Views.BatchPlotForm
try {
    if ($form.Text -notlike '*PDF*') { throw '界面标题无效' }
    $form.CreateControl()
    Write-Output ('PACKAGE_NET48_UI_OK=' + $form.Text)
} finally { $form.Dispose() }

param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('FbpAcadMerge-'+[guid]::NewGuid().ToString('N'))
$records=@()
try {
 foreach($name in @('DWG To PDF','AutoCAD PDF (General Documentation)','AutoCAD PDF (High Quality Print)','AutoCAD PDF (Smallest File)','AutoCAD PDF (Web and Mobile)')) {
  $source=Join-Path 'D:\Autodesk\CAD2018\AutoCAD 2018\UserDataCache\Plotters' ($name+'.pc3')
  $hash=(Get-FileHash -LiteralPath $source).Hash;$time=(Get-Item -LiteralPath $source).LastWriteTimeUtc
  $original=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($source)
  foreach($state in @('follow','merge','overwrite')) {
   $options=[FastBatchPlot.Core.Models.PdfOutputOptions]::new()
   if($state -ne 'follow'){$options.MergeLines=($state -eq 'merge')}
   $expected=if($state -eq 'follow'){$original.Root.Child('res_color_mem').Raw('lines_overwrite')}elseif($state -eq 'merge'){'FALSE'}else{'TRUE'}
   $files=$null
   try {
    $files=[FastBatchPlot.Core.Printing.AcadPdfMediaFiles]::Create($source,420,297,$temporary,$options)
    $generated=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($files.ConfigurationPath)
    if($generated.Root.Child('res_color_mem').Raw('lines_overwrite') -ne $expected){throw ('Wrong merge flag: '+$name+'/'+$state)}
    foreach($entry in $original.Root.Child('custom').Children.GetEnumerator()) {
     if($generated.Root.Child('custom').Child($entry.Key).Raw('value') -ne $entry.Value.Raw('value')){throw 'Other PDF parameter changed'}
    }
    foreach($entry in $original.Root.Child('res_color_mem').Child('resolution').Values.GetEnumerator()) {
     if($generated.Root.Child('res_color_mem').Child('resolution').Raw($entry.Key) -ne $entry.Value){throw 'Resolution changed'}
    }
   } finally {if($null -ne $files){$files.Dispose()}}
  }
  if((Get-FileHash -LiteralPath $source).Hash -ne $hash -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $time){throw 'Source changed'}
  $records+=[ordered]@{source=$source;sha256=$hash;lastWriteTimeUtc=$time.ToString('o');states=@('follow','merge','overwrite');result='ACAD_MERGE_OK'}
  Write-Output ('ACAD_MERGE_OK='+$name)
 }
 if(@(Get-ChildItem -LiteralPath $temporary -Force).Count -ne 0){throw 'Temporary files remain'}
 $records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceRoot 'sources.json') -Encoding UTF8
} finally {if(Test-Path -LiteralPath $temporary){[IO.Directory]::Delete($temporary,$false)}}

param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$work=Join-Path ([IO.Path]::GetTempPath()) ('FbpAcadPmp-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
$records=@()
function Compare-Node($before,$after,[string]$prefix) {
 foreach($entry in $before.Values.GetEnumerator()) {
  if($prefix -eq 'meta' -and $entry.Key -in @('user_defined_model_pathname','user_defined_model_basename')){continue}
  if($after.Raw($entry.Key) -ne $entry.Value){throw ('Inherited field changed: '+$prefix+'/'+$entry.Key)}
 }
 foreach($entry in $before.Children.GetEnumerator()){Compare-Node $entry.Value ($after.Child($entry.Key)) ($prefix+'/'+$entry.Key).TrimStart('/')}
}
try {
 foreach($name in @('DWG To PDF','AutoCAD PDF (General Documentation)','AutoCAD PDF (High Quality Print)','AutoCAD PDF (Smallest File)','AutoCAD PDF (Web and Mobile)')) {
  $source=Join-Path 'D:\Autodesk\CAD2018\AutoCAD 2018\UserDataCache\Plotters' ($name+'.pc3')
  $hash=(Get-FileHash -LiteralPath $source).Hash;$time=(Get-Item -LiteralPath $source).LastWriteTimeUtc
  $seed=$null;$files=$null
  $linked=Join-Path $work 'linked.pc3';$pmp=Join-Path $work 'existing.pmp'
  try {
   # 用真实原生 PDF 配置生成离线纸张样本；不宣称它是 CAD 已验收的用户 PMP。
   $seed=[FastBatchPlot.Core.Printing.AcadPdfMediaFiles]::Create($source,420,297,(Join-Path $work 'seed'))
   Copy-Item -LiteralPath $seed.PmpPath -Destination $pmp
   $original=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($pmp)
   $original.Root.Child('udm').Child('calibration').SetRaw('_x','0.95')
   $original.Root.Child('udm').Child('calibration').SetRaw('_y','1.05')
   [IO.File]::WriteAllBytes($pmp,$original.Encode())
   $pc3=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($source)
   $pc3.Root.Child('meta').SetText('user_defined_model_pathname',$pmp)
   $pc3.Root.Child('meta').SetText('user_defined_model_basename','existing.pmp')
   [IO.File]::WriteAllBytes($linked,$pc3.Encode())
   $seed.Dispose();$seed=$null
   $pmpHash=(Get-FileHash -LiteralPath $pmp).Hash
   $files=[FastBatchPlot.Core.Printing.AcadPdfMediaFiles]::Create($linked,634.25,301.125,(Join-Path $work 'private'))
   $generated=[FastBatchPlot.Core.Printing.PiaConfiguration]::Read($files.PmpPath)
   Compare-Node $original.Root $generated.Root ''
   $media=$generated.Root.Child('udm').Child('media')
   if($media.Child('size').Children.Count -ne 2 -or $media.Child('description').Children.Count -ne 2){throw 'Wrong paper count'}
   if($media.Child('size').Child('1').Text('name') -ne $files.MediaName -or $media.Child('description').Child('1').Raw('media_bounds_urx') -ne '634.250000'){throw 'Wrong appended paper'}
   if((Get-FileHash -LiteralPath $pmp).Hash -ne $pmpHash){throw 'Linked PMP changed'}
   Copy-Item -LiteralPath $files.PmpPath -Destination (Join-Path $EvidenceRoot ($name+'.pmp'))
   $files.Dispose();$files=$null
  } finally {if($null -ne $files){$files.Dispose()};if($null -ne $seed){$seed.Dispose()}}
  if((Get-FileHash -LiteralPath $source).Hash -ne $hash -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $time){throw 'Original PC3 changed'}
  $records+=[ordered]@{source=$source;sha256=$hash;lastWriteTimeUtc=$time.ToString('o');fixture='offline generated PDF PMP with non-unit calibration';result='ACAD_PMP_INHERITANCE_OK'}
  Write-Output ('ACAD_PMP_INHERITANCE_OK='+$name)
 }
 $records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceRoot 'sources.json') -Encoding UTF8
} finally {
 foreach($file in @('linked.pc3','existing.pmp')){$path=Join-Path $work $file;if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
 foreach($dir in @('seed','private')){$path=Join-Path $work $dir;if(Test-Path -LiteralPath $path){[IO.Directory]::Delete($path,$false)}}
 [IO.Directory]::Delete($work,$false)
}

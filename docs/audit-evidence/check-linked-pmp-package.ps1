param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
[IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
$work=Join-Path ([IO.Path]::GetTempPath()) ('FbpLinkedPmp-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null
$pc5='D:\ZWCAD2026\UserDataCache\zh-CN\Plotters\DWG to PDF.pc5'
$pmp='D:\ZWCAD2026\UserDataCache\zh-CN\Plotters\PMP Files\ZWPLOT_PDF.pmp'
$encoding=[Text.Encoding]::GetEncoding(936)
$records=@(@($pc5,$pmp) | ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash;time=(Get-Item -LiteralPath $_).LastWriteTimeUtc.ToString('o')}})
$files=$null
try {
 $source=Join-Path $work 'source.pc5'
 $text=[IO.File]::ReadAllText($pc5,$encoding)
 $text=$text.Replace('pmp_filepath=',('pmp_filepath='+$pmp))
 [IO.File]::WriteAllText($source,$text,$encoding)
 $options=[FastBatchPlot.Core.Models.PdfOutputOptions]::new();$options.MergeLines=$true;$options.IncludeLayers=$false
 $files=[FastBatchPlot.Core.Printing.ZwPdfMediaFiles]::Create($source,634.25,301.125,(Join-Path $work 'private'),$options)
 $generated=[IO.File]::ReadAllText($files.PmpPath,$encoding)
 $original=[IO.File]::ReadAllText($pmp,$encoding)
 foreach($line in $original -split '[\r\n]+' | Where-Object {$_ -ne ''}) {
  $expected=if($line -eq 'userdef_num=72'){'userdef_num=73'}else{$line}
  if(-not (($generated -split '[\r\n]+') -contains $expected)){throw ('Original PMP field lost: '+$line)}
 }
 foreach($line in @(('paper_name72='+$files.MediaName),'size_x72=634.250000','size_y72=301.125000','Unit72=1')) {
  if(-not (($generated -split '[\r\n]+') -contains $line)){throw ('Missing new media: '+$line)}
 }
 Copy-Item -LiteralPath $files.PmpPath -Destination (Join-Path $EvidenceRoot 'inherited.pmp')
 Copy-Item -LiteralPath $files.ConfigurationPath -Destination (Join-Path $EvidenceRoot 'inherited.pc5')
 $files.Dispose();$files=$null
 foreach($r in $records){if((Get-FileHash -LiteralPath $r.path).Hash -ne $r.sha256 -or (Get-Item -LiteralPath $r.path).LastWriteTimeUtc.ToString('o') -ne $r.time){throw 'Source changed'}}
 $records | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $EvidenceRoot 'sources.json') -Encoding UTF8
 if(@(Get-ChildItem -LiteralPath (Join-Path $work 'private') -Force).Count -ne 0){throw 'Temporary media remains'}
 Write-Output 'LINKED_PMP_OK=72 original papers preserved; 1 appended; source hashes unchanged'
} finally {
 if($null -ne $files){$files.Dispose()}
 foreach($path in @((Join-Path $work 'source.pc5'))){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
 if(Test-Path -LiteralPath (Join-Path $work 'private')){[IO.Directory]::Delete((Join-Path $work 'private'),$false)}
 [IO.Directory]::Delete($work,$false)
}

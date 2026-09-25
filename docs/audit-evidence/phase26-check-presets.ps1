param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$sourceRoot='D:\ZWCAD2026\UserDataCache\zh-CN\Plotters'
$names=@('DWG to PDF','ZWCAD PDF(General Documentation)','ZWCAD PDF(High Quality Print)','ZWCAD PDF(Smallest File)','ZWCAD PDF(Web and Mobile)')
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('FbpPresets-'+[Guid]::NewGuid().ToString('N'))
$records=@()
try {
    [IO.Directory]::CreateDirectory($EvidenceRoot) | Out-Null
    foreach($name in $names) {
        $source=Join-Path $sourceRoot ($name+'.pc5')
        $hash=(Get-FileHash -LiteralPath $source).Hash
        $time=(Get-Item -LiteralPath $source).LastWriteTimeUtc
        $files=$null
        try {
            $files=[FastBatchPlot.Core.Printing.ZwPdfMediaFiles]::Create($source,634.25,301.125,$temporary)
            Copy-Item -LiteralPath $files.ConfigurationPath -Destination (Join-Path $EvidenceRoot ($name+'.pc5'))
            Copy-Item -LiteralPath $files.PmpPath -Destination (Join-Path $EvidenceRoot ($name+'.pmp'))
            foreach($path in @($files.ConfigurationPath,$files.PmpPath)) {
                $locked=$false
                try {$stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Write);$stream.Dispose()} catch [IO.IOException] {$locked=$true}
                if(-not $locked){throw 'Generated file is not read locked'}
            }
            $records += [ordered]@{source=$source;sha256=$hash;lastWriteTimeUtc=$time.ToString('o');configuration=$files.ConfigurationPath;pmp=$files.PmpPath;mediaName=$files.MediaName}
        } finally {if($null -ne $files){$files.Dispose()}}
        if((Get-FileHash -LiteralPath $source).Hash -ne $hash -or (Get-Item -LiteralPath $source).LastWriteTimeUtc -ne $time){throw 'Source was changed'}
        Write-Output ('NATIVE_PRESET_FILE_OK='+$name)
    }
    if(@(Get-ChildItem -LiteralPath $temporary -Force).Count -ne 0){throw 'Temporary files remain'}
    $records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceRoot 'sources.json') -Encoding UTF8
} finally {if(Test-Path -LiteralPath $temporary){[IO.Directory]::Delete($temporary,$false)}}

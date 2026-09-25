param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
trap { Write-Output $_.Exception.ToString(); exit 1 }
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$bitmap=New-Object Drawing.Bitmap 12,6
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$buffer=New-Object IO.MemoryStream
try {
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.FillRectangle([Drawing.Brushes]::Red,2,1,8,4)
    $bitmap.Save($buffer,[Drawing.Imaging.ImageFormat]::Png)
    $asset=[FastBatchPlot.Core.Assets.StampAsset]::Import('框架兼容性测试',$buffer.ToArray())
} finally { $buffer.Dispose();$graphics.Dispose();$bitmap.Dispose() }
# 合成图片的固定测试口令，不是用户资产的授权码。
$testCode='test-only-authorization-2026'
$now=[DateTimeOffset]::UtcNow
$watch=[Diagnostics.Stopwatch]::StartNew()
$details=New-Object FastBatchPlot.Core.Assets.StampDetails
$details.Kind=[FastBatchPlot.Core.Assets.StampKind]::Registration
$details.Sizing=[FastBatchPlot.Core.Assets.StampSizing]::PhysicalSize
$details.WidthMm=62
$details.HeightMm=32
$details.ValidUntilUtcTicks=$now.AddHours(2).UtcDateTime.Ticks
$asset.Details=$details
$encrypted=[FastBatchPlot.Core.Assets.StampAuthorization]::Protect($asset,$testCode,$now.AddDays(1),$now)
$permit=[FastBatchPlot.Core.Assets.StampAuthorization]::Unlock($encrypted,$testCode,[DateTimeOffset]::UtcNow)
$plain=$permit.GetAsset($encrypted,[DateTimeOffset]::UtcNow)
if ($plain.PngBase64 -ne $asset.PngBase64 -or $encrypted.PngBase64 -ne '') { throw '保护后明文检查或解锁内容不匹配' }
if ($encrypted.Protection.Version -ne 2) { throw '新属性未纳入新版认证' }
$expired=$false
try { $null=$permit.GetAsset($encrypted,$now.AddHours(2)) } catch { $expired=$true }
if (-not $expired) { throw '印章独立期限未执行' }
$changed=$encrypted.Copy()
$changed.Protection.ExpiresUtcTicks=$changed.Protection.ExpiresUtcTicks+1
$rejected=$false
try { $null=[FastBatchPlot.Core.Assets.StampAuthorization]::Unlock($changed,$testCode,[DateTimeOffset]::UtcNow) }
catch { $rejected=$true }
if (-not $rejected) { throw '期限篡改未被拒绝' }
$watch.Stop()
'NET48_STAMP_AUTHORIZATION_OK; elapsedMs=' + $watch.ElapsedMilliseconds

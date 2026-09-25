$ErrorActionPreference='Stop'
$app=[Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
$doc=$app.Documents.Open('E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase43-native\independent-test.dwg',$false)
$doc.Activate()
$doc.SendCommand('BP43PROBEC'+[char]10)
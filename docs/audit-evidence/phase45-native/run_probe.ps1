$app = [Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
$dll = 'E:/366256/vibecoding/批打印-new/docs/audit-evidence/phase45-native/bin/Release/net48/TestMediaProbe.dll'
$app.ActiveDocument.SendCommand('(command "_.NETLOAD" "' + $dll + '")' + [char]10)
Start-Sleep -Milliseconds 800
$app.ActiveDocument.SendCommand('BP_PROBE_MEDIA' + [char]10)
Start-Sleep -Seconds 1

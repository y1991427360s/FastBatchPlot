$app = [Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
$dllPath = "E:/366256/vibecoding/批打印-new/docs/audit-evidence/phase45-native/l-bin/net48/Phase45ProbeL.dll"
$app.ActiveDocument.SendCommand('(command "_.NETLOAD" "' + $dllPath + '")' + [char]10)
$app.ActiveDocument.SendCommand('BP45L' + [char]10)
Start-Sleep -Seconds 1
$app.ActiveDocument.SendCommand('BP45LCHECK' + [char]10)
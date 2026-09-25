$app = [Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
$dllPath = "E:/366256/vibecoding/批打印-new/docs/audit-evidence/phase45-native/i-bin/net48/Phase45ProbeI.dll"
$app.ActiveDocument.SendCommand('(command "_.NETLOAD" "' + $dllPath + '")' + [char]10)
$app.ActiveDocument.SendCommand('BP45I' + [char]10)
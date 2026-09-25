$ErrorActionPreference='Stop'
$cadApp=[Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
$plugin='E:/366256/vibecoding/批打印-new/artifacts/FastBatchPlot-20260920-213046-884/ZWCAD2026/Contents/FastBatchPlot.ZWCAD.dll'
$probe='E:/366256/vibecoding/批打印-new/docs/audit-evidence/phase43-native/bin/C/Phase43ProbeC.dll'
$command='(progn (command "_.NETLOAD" "'+$plugin+'") (command "_.NETLOAD" "'+$probe+'") (princ))'
$cadApp.ActiveDocument.SendCommand($command+"`n")

$progIds = @("ZWCAD.Application", "ZWCAD.Application.2026", "ZwCAD.Application")
foreach ($p in $progIds) {
    try {
        $obj = [Runtime.InteropServices.Marshal]::GetActiveObject($p)
        Write-Output "GetActiveObject succeeded for $p! ActiveDoc: $($obj.ActiveDocument.Name)"
    } catch {
        Write-Output "GetActiveObject failed for $p`: $($_.Exception.Message)"
    }
}
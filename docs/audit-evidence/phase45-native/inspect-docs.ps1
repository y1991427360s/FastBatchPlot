$app = [Runtime.InteropServices.Marshal]::GetActiveObject('ZWCAD.Application')
Write-Output ("Docs count: " + $app.Documents.Count)
for ($i = 0; $i -lt $app.Documents.Count; $i++) {
    $doc = $app.Documents.Item($i)
    Write-Output ("Doc " + $i + ": " + $doc.FullName + " (Name: " + $doc.Name + ") Active: " + ($app.ActiveDocument.FullName -eq $doc.FullName))
}

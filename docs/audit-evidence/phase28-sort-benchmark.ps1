param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
# 构造数据及 PowerShell 调用开销不纳入排序计时；计时区在 C# 内。
Add-Type -ReferencedAssemblies @((Join-Path $Contents 'FastBatchPlot.Core.dll'), (Get-ChildItem -Path (Join-Path $env:USERPROFILE '.nuget/packages/microsoft.netframework.referenceassemblies.net48') -Recurse -Filter netstandard.dll | Select-Object -First 1 -ExpandProperty FullName)) -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Detection;
public static class SortBenchmark {
 public static double Measure(int count, int kind) {
  var frames=new List<PlotFrame>();
  for(int i=count-1;i>=0;i--)frames.Add(new PlotFrame {
   Id=i,MinX=kind==1?0:i*500.0,MaxX=kind==1?420:i*500.0+420,
   MinY=kind==1?i*400.0:0,MaxY=kind==1?i*400.0+297:297,
   TitleInfo=new TitleBlockInfo {DrawingNo="电施-"+i+"-详图"}});
  var rule=kind==0?SortOrderRule.LeftToRight_TopToBottom:kind==1?SortOrderRule.TopToBottom_LeftToRight:SortOrderRule.ByDrawingNo;
  FrameSorter.Sort(frames.GetRange(0,Math.Min(count,30)),rule);
  var watch=Stopwatch.StartNew();var sorted=FrameSorter.Sort(frames,rule);watch.Stop();
  if(sorted.Count!=count || sorted[0].OrderIndex!=1 || sorted[count-1].OrderIndex!=count)throw new Exception("排序结果无效");
  for(int i=0;i<count;i++)if(sorted[i].Id!=(kind==1?count-1-i:i))throw new Exception("图纸顺序不符");
  return watch.Elapsed.TotalMilliseconds;
 }
}
'@
$records=@()
foreach($kind in 0..2){
 $times=@();for($trial=0;$trial -lt 3;$trial++){$times+=[SortBenchmark]::Measure(10000,$kind)}
 $records += [ordered]@{scenario=@('单行图框','单列图框','图号自然排序')[$kind];pages=10000;milliseconds=$times;median=($times | Sort-Object)[1]}
}
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Output -Encoding UTF8
$records | ForEach-Object { [pscustomobject]$_ } | Format-Table scenario,pages,median

param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
try {
 $form.CreateControl()
 $references=@((Join-Path $Contents 'FastBatchPlot.Core.dll'),(Join-Path $Contents 'FastBatchPlot.CadBridge.dll'),'C:\Program Files\dotnet\sdk\8.0.419\Microsoft\Microsoft.NET.Build.Extensions\net461\lib\netstandard.dll')
 Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Detection;
public class PageProbe : ICadHost, ICadPlotter, ICadRevisionHost, ICadPlotResourceHost {
 public string Token="stable";
 public string GetSourceRevision(PlotFrame f){return Token;}
 public string GetPlotResourceRevision(PlotConfig c){return "style";}
 public string PlatformName{get{return "offline";}} public string Version{get{return "test";}}
 public void WriteMessage(string s){} public string GetCurrentDocumentPath(){return "";} public string GetCurrentDocumentName(){return "";}
 public List<RawPolylineCandidate> CollectPolylineCandidates(string s="*"){throw new Exception("no CAD");}
 public List<RawBlockCandidate> CollectBlockCandidates(string s="*"){throw new Exception("no CAD");}
 public bool PromptSelectFrames(out List<RawPolylineCandidate> a,out List<RawBlockCandidate> b){throw new Exception("no CAD");}
 public bool PromptSelectSampleBlock(out string a,out string b){throw new Exception("no CAD");}
 public void ZoomToFrame(double a,double b,double c,double d){throw new Exception("no CAD");}
 public List<string> GetAllLayers(){return new List<string>();} public List<string> GetAllBlockNames(){return new List<string>();}
 public List<string> GetAvailablePlotters(){return new List<string>();} public List<string> GetAvailablePlotStyles(){return new List<string>();}
 public List<string> GetPaperSizesForPlotter(string s){return new List<string>();}
 public bool PlotFrameToFile(PlotFrame f,PlotConfig c,string p,out string error){throw new Exception("no CAD");}
 public static void Check(){
  var probe=new PageProbe();CadHostProvider.Host=probe;CadHostProvider.Plotter=probe;
  try {
   var frame=new PlotFrame();var config=new PlotConfig();
   var check=new CadPageConsistency(probe,probe,frame,config);check.Verify();probe.Token="changed";
   bool failed=false;try{check.Verify();}catch(InvalidOperationException){failed=true;}if(!failed)throw new Exception("changed page accepted");
   probe.Token=" ";failed=false;try{new CadPageConsistency(probe,probe,frame,config);}catch(InvalidOperationException){failed=true;}if(!failed)throw new Exception("empty token accepted");
  }finally{CadHostProvider.Host=null;CadHostProvider.Plotter=null;}
 }
}
'@
 [PageProbe]::Check()
 Write-Output 'PACKAGE_NET48_PAGE_CONSISTENCY_OK'
} finally {$form.Dispose()}

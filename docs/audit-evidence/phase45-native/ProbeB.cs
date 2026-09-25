using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.ZWCAD;
using App=ZwSoft.ZwCAD.ApplicationServices.Application;
[assembly:CommandClass(typeof(Phase45ProbeB))]
public class Phase45ProbeB {
 static readonly string Root=@"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
 static void Log(string s){File.AppendAllText(Path.Combine(Root,"fixed-b.log"),DateTime.Now.ToString("o")+" "+s+Environment.NewLine);}
 [CommandMethod("BP45B",CommandFlags.Session)]
 public void Run(){
  try{
   var doc=App.DocumentManager.MdiActiveDocument;
   if(!string.Equals(doc.Database.Filename,Path.GetFullPath(Path.Combine(Root,"../phase43-native/independent-test.dwg")),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("测试入口只允许本次独立测试图。");
   using(doc.LockDocument()){
    var db=doc.Database; var frames=new List<PlotFrame>();
    using(var tr=db.TransactionManager.StartTransaction()){
     var dict=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
     foreach(DBDictionaryEntry entry in dict){
      var layout=(Layout)tr.GetObject(entry.Value,OpenMode.ForRead);
      var space=(BlockTableRecord)tr.GetObject(layout.BlockTableRecordId,OpenMode.ForRead);
      frames.Add(new PlotFrame{Id=frames.Count+1,MinX=0,MinY=0,MaxX=297,MaxY=210,Type=FrameType.PickWindow,DetectedPaper=new PaperSize("A4",297,210,true),CalculatedScale=1,SourceDocumentId=DocumentSessionIdentity.Get(doc),SourceLayoutId=space.Handle.ToString(),LayoutName=layout.LayoutName});
     }tr.Commit();
    }
    LayoutManager.Current.CurrentLayout="Model";
    var host=new ZwCadAdapter();var plotter=new ZwCadPlotEngine();CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
    Log("DEVICES "+string.Join(" | ",plotter.GetAvailablePlotters()));Log("STYLES "+string.Join(" | ",plotter.GetAvailablePlotStyles()));
    var device=plotter.GetAvailablePlotters().First(s=>s.IndexOf("PDF",StringComparison.OrdinalIgnoreCase)>=0&&s.EndsWith(".pc5",StringComparison.OrdinalIgnoreCase));
    Log("DEVICE "+device);
    ObjectEventHandler modified=(s,e)=>Log("MOD "+e.DBObject.GetType().Name+" "+e.DBObject.Handle);
    ObjectEventHandler appended=(s,e)=>Log("ADD "+e.DBObject.GetType().Name+" "+e.DBObject.Handle);
    db.ObjectModified+=modified;db.ObjectAppended+=appended;
    ObjectErasedEventHandler erased=(s,e)=>Log("ERASE "+e.DBObject.GetType().Name+" "+e.DBObject.Handle);db.ObjectErased+=erased;
    EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> first=(s,e)=>{if(e.Exception.StackTrace!=null&&e.Exception.StackTrace.Contains("FastBatchPlot"))Log("FIRST "+e.Exception);};
    AppDomain.CurrentDomain.FirstChanceException+=first;
    try{foreach(var frame in frames.Take(1)){
     var config=new FastBatchPlot.Core.Models.PlotConfig{PrinterDevice=device,PlotStyleTable="monochrome.ctb",MergeToSinglePdf=false,PdfOptions=new PdfOutputOptions{VectorResolutionDpi=1200,IncludeLayers=true}};
     Log("BEGIN "+frame.LayoutName); for(int i=0;i<3;i++)Log("READ "+host.GetSourceRevision(frame));
     try{
      var check=new CadPageConsistency(host,plotter,frame,config);Log("BEFORE "+host.GetSourceRevision(frame));
      string error;bool ok=plotter.PlotFrameToFile(frame,config,Path.Combine(Root,"fixed-b-"+frame.Id+".pdf"),out error);
      Log("PLOT "+ok+" "+error);Log("AFTER "+host.GetSourceRevision(frame));
      if(ok){check.Verify();Log("VERIFY OK");}else Log("VERIFY SKIPPED: plot failed");
     }catch(System.Exception ex){Log("FAIL "+ex);}
    }}finally{db.ObjectErased-=erased;db.ObjectModified-=modified;db.ObjectAppended-=appended;AppDomain.CurrentDomain.FirstChanceException-=first;}
    Log("DONE layout="+LayoutManager.Current.CurrentLayout);
   }
  }catch(System.Exception ex){Log("FATAL "+ex);}
 }
}

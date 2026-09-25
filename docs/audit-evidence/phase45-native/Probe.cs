using System;
using System.IO;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.PlottingServices;
using ZwSoft.ZwCAD.Runtime;
using App=ZwSoft.ZwCAD.ApplicationServices.Application;
[assembly:CommandClass(typeof(Phase45Probe))]
public class Phase45Probe {
 static readonly string Root=@"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
 static void Log(string s){File.AppendAllText(Path.Combine(Root,"probe.log"),DateTime.Now.ToString("o")+" "+s+Environment.NewLine);}
 static void Config(PlotInfo info,string when){
  Log(when+" validated="+info.IsValidated);
  try{using(var ps=info.ValidatedSettings)Log("SETTINGS "+ps.PlotConfigurationName+" / "+ps.CanonicalMediaName);}catch(System.Exception e){Log("SETTINGS ERROR "+e);}
  try{using(var pc=info.ValidatedConfig)Log("CONFIG "+pc.FullPath);}catch(System.Exception e){Log("CONFIG ERROR "+e);}
 }
 [CommandMethod("BP45DIAG",CommandFlags.Session)] public void Run(){
  object bg=null;
  try{
   var doc=App.DocumentManager.MdiActiveDocument;
   string expected=Path.GetFullPath(Path.Combine(Root,"../phase43-native/independent-test.dwg"));
   if(!string.Equals(doc.Name,expected,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("独立测试图不匹配："+doc.Name);
   using(doc.LockDocument())using(var tr=doc.Database.TransactionManager.StartTransaction()){
    var btr=(BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId,OpenMode.ForRead);
    var layout=(Layout)tr.GetObject(btr.LayoutId,OpenMode.ForRead);
    if(!layout.ModelType)throw new InvalidOperationException("测试需要模型空间");
    using(var ps=new PlotSettings(true))using(var pi=new PlotInfo())using(var validator=new PlotInfoValidator()){
     ps.CopyFrom(layout);var psv=PlotSettingsValidator.Current;
     psv.SetPlotConfigurationName(ps,"DWG to PDF.pc5",null);psv.RefreshLists(ps);
     string chosen=null;
     foreach(string name in psv.GetCanonicalMediaNameList(ps)){
      psv.SetCanonicalMediaName(ps,name);psv.SetPlotPaperUnits(ps,PlotPaperUnit.Millimeters);
      var size=ps.PlotPaperSize;
      if(Math.Abs(size.X-297)<.1&&Math.Abs(size.Y-210)<.1){chosen=name;break;}
     }
     if(chosen==null)throw new InvalidOperationException("无A4横向");Log("MEDIA "+chosen);
     psv.SetPlotWindowArea(ps,new Extents2d(0,0,297,210));psv.SetPlotType(ps,ZwSoft.ZwCAD.DatabaseServices.PlotType.Window);
     psv.SetPlotRotation(ps,PlotRotation.Degrees000);psv.SetUseStandardScale(ps,false);psv.SetCustomPrintScale(ps,new CustomScale(1,1));
     psv.SetCurrentStyleSheet(ps,"monochrome.ctb");ps.PlotPlotStyles=true;ps.PrintLineweights=true;
     pi.Layout=layout.ObjectId;pi.OverrideSettings=ps;validator.MediaMatchingPolicy=MatchingPolicy.MatchDisabled;validator.Validate(pi);
     Config(pi,"AFTER VALIDATE");
     bg=App.GetSystemVariable("BACKGROUNDPLOT");App.SetSystemVariable("BACKGROUNDPLOT",0);
     using(var engine=PlotFactory.CreatePublishEngine())using(var page=new PlotPageInfo()){
      engine.BeginPlot(null,null);Log("BEGIN PLOT");
      engine.BeginDocument(pi,doc.Name,null,1,true,Path.Combine(Root,"direct.pdf"));Log("BEGIN DOCUMENT");Config(pi,"AFTER BEGIN DOCUMENT");
      engine.BeginPage(page,pi,true,null);Log("BEGIN PAGE");engine.BeginGenerateGraphics(null);engine.EndGenerateGraphics(null);engine.EndPage(null);engine.EndDocument(null);engine.EndPlot(null);Log("PRINT OK");
     }
    }
   }
  }catch(System.Exception e){Log("FAIL "+e);}finally{if(bg!=null)App.SetSystemVariable("BACKGROUNDPLOT",bg);}
 }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Export
{
    /// <summary>可搬移的 OOXML 导出器，无 Excel COM 或外部进程依赖。</summary>
    public static class CatalogXlsxExporter
    {
        private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace P="http://schemas.openxmlformats.org/package/2006/relationships";
        public static void Export(IEnumerable<PlotFrame> frames,string path,CatalogOptions? options=null,bool overwrite=false)
        {
            options=options??new CatalogOptions(); options.Validate();
            if (!string.Equals(Path.GetExtension(path),".xlsx",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("目录文件须使用 .xlsx 扩展名。");
            var rows=frames.Where(f=>!options.SelectedOnly||f.IsSelected).ToList();
            if(rows.Count==0 || rows.Count>100000) throw new ArgumentException("目录需包含 1 到 100000 张图纸。");
            var usage=PaperUsageCalculator.Calculate(rows);
            if (rows.Any(f=>f.TitleInfo==null || !CatalogOptions.Finite(f.CalculatedScale) || f.CalculatedScale<=0)) throw new ArgumentException("图纸信息或比例无效。");
            string full=Path.GetFullPath(path),directory=Path.GetDirectoryName(full)!;
            if(File.Exists(full)&&!overwrite) throw new IOException("目录文件已存在，未覆盖。");
            Directory.CreateDirectory(directory);
            string temporary=Path.Combine(directory,".catalog-"+Guid.NewGuid().ToString("N")+".xlsx");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {
                    using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
                    {
                        Write(zip,"[Content_Types].xml",ContentTypes(options.IncludeStatistics));
                        Write(zip,"_rels/.rels",new XElement(P+"Relationships",Relation("rId1","officeDocument","xl/workbook.xml")));
                        var sheets=new XElement(S+"sheets",Sheet("图纸目录",1));
                        var names=new XElement(S+"definedNames",Defined("_xlnm.Print_Titles",0,"'图纸目录'!$4:$4"),
                            Defined("_xlnm.Print_Area",0,"'图纸目录'!$A$1:$"+Column(options.Columns.Count)+"$"+(rows.Count+4)));
                        if(options.IncludeStatistics) {sheets.Add(Sheet("纸张统计",2)); names.Add(Defined("_xlnm.Print_Titles",1,"'纸张统计'!$5:$5"));}
                        Write(zip,"xl/workbook.xml",new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),sheets,names,
                            new XElement(S+"calcPr",new XAttribute("calcId",191029),new XAttribute("fullCalcOnLoad",1))));
                        var relations=new XElement(P+"Relationships",Relation("rId1","worksheet","worksheets/sheet1.xml"),Relation("rIdStyles","styles","styles.xml"));
                        if(options.IncludeStatistics) relations.Add(Relation("rId2","worksheet","worksheets/sheet2.xml"));
                        Write(zip,"xl/_rels/workbook.xml.rels",relations);
                        Write(zip,"xl/styles.xml",Styles());
                        Write(zip,"xl/worksheets/sheet1.xml",CatalogSheet(rows,options));
                        if(options.IncludeStatistics) Write(zip,"xl/worksheets/sheet2.xml",StatisticsSheet(usage));
                    }
                    stream.Flush(true);
                }
                if(overwrite&&File.Exists(full)) File.Replace(temporary,full,null); else File.Move(temporary,full);
            }
            finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }
        private static XElement Sheet(string name,int id)=>new XElement(S+"sheet",new XAttribute("name",name),new XAttribute("sheetId",id),new XAttribute(R+"id","rId"+id));
        private static XElement Defined(string name,int index,string value)=>new XElement(S+"definedName",new XAttribute("name",name),new XAttribute("localSheetId",index),value);
        private static XElement Relation(string id,string type,string target)=>new XElement(P+"Relationship",new XAttribute("Id",id),new XAttribute("Type",R.NamespaceName+"/"+type),new XAttribute("Target",target));
        private static XElement ContentTypes(bool statistics)
        {
            XNamespace n="http://schemas.openxmlformats.org/package/2006/content-types";
            var root=new XElement(n+"Types",new XElement(n+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(n+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")));
            foreach(var pair in new[]{new[]{"/xl/workbook.xml","sheet.main"},new[]{"/xl/styles.xml","styles"},new[]{"/xl/worksheets/sheet1.xml","worksheet"}})
                root.Add(new XElement(n+"Override",new XAttribute("PartName",pair[0]),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml."+pair[1]+"+xml")));
            if(statistics) root.Add(new XElement(n+"Override",new XAttribute("PartName","/xl/worksheets/sheet2.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            return root;
        }
        private static XElement CatalogSheet(List<PlotFrame> frames,CatalogOptions options)
        {
            var data=new XElement(S+"sheetData",Row(2,26,Text(1,2,options.Title,1)),
                Row(3,23,Text(1,3,(options.SelectedOnly?"勾选图纸":"全部图纸")+"，共 "+frames.Count+" 张；导出内容为当前列表快照。",4)),
                Row(4,30,options.Columns.Select((c,i)=>Text(i+1,4,c.Header,2)).ToArray()));
            int row=5;
            foreach(var f in frames)
            {
                var cells=new List<XElement>(); int column=1;
                foreach(var c in options.Columns)
                {
                    string value=CatalogOptions.Value(f,c.Field)??"";
                    if(c.Field==CatalogField.Index) cells.Add(Number(column,row,f.OrderIndex,3));
                    else if(c.Field==CatalogField.Date && DateTime.TryParseExact(value,new[]{"yyyy-MM-dd","yyyy/MM/dd","yyyyMMdd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) && date.Year>=1900)
                        cells.Add(Number(column,row,date.ToOADate()-(date < new DateTime(1900,3,1)?1:0),6));
                    else cells.Add(Text(column,row,value));
                    column++;
                }
                data.Add(Row(row++,options.RowHeight,cells.ToArray()));
            }
            var sheet=Worksheet(data,options.Columns.Select(c=>c.Width).ToArray(),4,"A4:"+Column(options.Columns.Count)+(frames.Count+4));
            if(options.Columns.Count>1) sheet.Element(S+"autoFilter")!.AddAfterSelf(new XElement(S+"mergeCells",new XAttribute("count",2),
                new XElement(S+"mergeCell",new XAttribute("ref","A2:"+Column(options.Columns.Count)+"2")),new XElement(S+"mergeCell",new XAttribute("ref","A3:"+Column(options.Columns.Count)+"3"))));
            return sheet;
        }
        private static XElement StatisticsSheet(List<PaperUsage> usage)
        {
            var data=new XElement(S+"sheetData",Row(2,26,Text(1,2,"纸张面积与 A1 折算",1)),
                Row(3,40,Text(1,3,"A1 基准面积(m²)"),Number(2,3,PaperUsageCalculator.A1AreaSquareMetres,5),Text(3,3,"841 × 594 mm；折算按面积，不计份数、留白与损耗。",4)),
                Row(5,30,new[]{"图幅","长边(mm)","短边(mm)","张数","总面积(m²)","折合A1(张)"}.Select((h,i)=>Text(i+1,5,h,2)).ToArray()));
            int row=6;
            foreach(var u in usage)
            {
                data.Add(Row(row,26,Text(1,row,u.Name),Number(2,row,u.WidthMm,7),Number(3,row,u.HeightMm,7),Number(4,row,u.Count,3),
                    Formula(5,row,$"B{row}*C{row}*D{row}/1000000",u.AreaSquareMetres),Formula(6,row,$"E{row}/$B$3",u.EquivalentA1)));
                row++;
            }
            data.Add(Row(row,28,Text(1,row,"合计",2),Formula(4,row,$"SUM(D6:D{row-1})",usage.Sum(u=>u.Count),3),
                Formula(5,row,$"SUM(E6:E{row-1})",usage.Sum(u=>u.AreaSquareMetres)),Formula(6,row,$"SUM(F6:F{row-1})",usage.Sum(u=>u.EquivalentA1))));
            var sheet=Worksheet(data,new[]{24.0,18,18,12,20,20},5,"A5:F"+(row-1));
            sheet.Element(S+"autoFilter")!.AddAfterSelf(new XElement(S+"mergeCells",new XAttribute("count",2),new XElement(S+"mergeCell",new XAttribute("ref","A2:F2")),new XElement(S+"mergeCell",new XAttribute("ref","C3:F3"))));
            return sheet;
        }
        private static XElement Worksheet(XElement data,double[] widths,int freeze,string filter)
        {
            return new XElement(S+"worksheet",new XElement(S+"sheetPr",new XElement(S+"pageSetUpPr",new XAttribute("fitToPage",1))),
                new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),
                    new XElement(S+"pane",new XAttribute("ySplit",freeze),new XAttribute("topLeftCell","A"+(freeze+1)),new XAttribute("activePane","bottomLeft"),new XAttribute("state","frozen")))),
                new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",26)),
                new XElement(S+"cols",widths.Select((w,i)=>new XElement(S+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",w),new XAttribute("customWidth",1)))),
                data,new XElement(S+"autoFilter",new XAttribute("ref",filter)),
                new XElement(S+"pageMargins",new XAttribute("left",0.3),new XAttribute("right",0.3),new XAttribute("top",0.4),new XAttribute("bottom",0.4),new XAttribute("header",0.2),new XAttribute("footer",0.2)),
                new XElement(S+"pageSetup",new XAttribute("paperSize",8),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)));
        }
        private static XElement Row(int index,double height,params XElement[] cells)=>new XElement(S+"row",new XAttribute("r",index),new XAttribute("ht",height),new XAttribute("customHeight",1),cells);
        private static XElement Text(int col,int row,string value,int style=0)
        {
            if(value.Length>32767) throw new ArgumentException("目录文本超过 Excel 单元格长度限制，未截断导出。");
            XmlConvert.VerifyXmlChars(value);
            return new XElement(S+"c",new XAttribute("r",Column(col)+row),new XAttribute("s",style),new XAttribute("t","inlineStr"),new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value)));
        }
        private static XElement Number(int col,int row,double value,int style)=>new XElement(S+"c",new XAttribute("r",Column(col)+row),new XAttribute("s",style),new XElement(S+"v",value.ToString("R",CultureInfo.InvariantCulture)));
        private static XElement Formula(int col,int row,string formula,double value,int style=5)
        {var cell=Number(col,row,value,style);cell.AddFirst(new XElement(S+"f",formula));return cell;}
        private static string Column(int index) {string name="";while(index>0){index--;name=(char)('A'+index%26)+name;index/=26;}return name;}
        private static void Write(ZipArchive zip,string name,XElement element)
        {using(var stream=zip.CreateEntry(name,CompressionLevel.Optimal).Open()) using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false})) new XDocument(new XDeclaration("1.0","utf-8","yes"),element).Save(writer);}
        private static XElement Styles()
        {
            XElement Font(bool bold,int size,string color)=>new XElement(S+"font",bold?new XElement(S+"b"):null,new XElement(S+"sz",new XAttribute("val",size)),new XElement(S+"color",new XAttribute("rgb",color)),new XElement(S+"name",new XAttribute("val","Microsoft YaHei")));
            XElement Xf(int font,int fill,int number,string align="left")=>new XElement(S+"xf",new XAttribute("numFmtId",number),new XAttribute("fontId",font),new XAttribute("fillId",fill),new XAttribute("borderId",0),new XAttribute("xfId",0),new XAttribute("applyAlignment",1),new XAttribute("applyNumberFormat",1),new XElement(S+"alignment",new XAttribute("vertical","center"),new XAttribute("horizontal",align),new XAttribute("wrapText",1)));
            return new XElement(S+"styleSheet",
                new XElement(S+"numFmts",new XAttribute("count",3),new XElement(S+"numFmt",new XAttribute("numFmtId",164),new XAttribute("formatCode","0.000000")),new XElement(S+"numFmt",new XAttribute("numFmtId",165),new XAttribute("formatCode","yyyy-mm-dd")),new XElement(S+"numFmt",new XAttribute("numFmtId",166),new XAttribute("formatCode","0.###"))),
                new XElement(S+"fonts",new XAttribute("count",4),Font(false,10,"FF202A35"),Font(true,14,"FF202A35"),Font(true,10,"FFFFFFFF"),Font(false,10,"FF586779")),
                new XElement(S+"fills",new XAttribute("count",3),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","none"))),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","gray125"))),new XElement(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType","solid"),new XElement(S+"fgColor",new XAttribute("rgb","FF34495E")),new XElement(S+"bgColor",new XAttribute("indexed",64))))),
                new XElement(S+"borders",new XAttribute("count",1),new XElement(S+"border",new XElement(S+"left"),new XElement(S+"right"),new XElement(S+"top"),new XElement(S+"bottom"),new XElement(S+"diagonal"))),
                new XElement(S+"cellStyleXfs",new XAttribute("count",1),new XElement(S+"xf",new XAttribute("numFmtId",0),new XAttribute("fontId",0),new XAttribute("fillId",0),new XAttribute("borderId",0))),
                new XElement(S+"cellXfs",new XAttribute("count",8),Xf(0,0,49),Xf(1,0,0),Xf(2,2,0,"center"),Xf(0,0,1,"right"),Xf(3,0,0),Xf(0,0,164,"right"),Xf(0,0,165,"right"),Xf(0,0,166,"right")),
                new XElement(S+"cellStyles",new XAttribute("count",1),new XElement(S+"cellStyle",new XAttribute("name","Normal"),new XAttribute("xfId",0),new XAttribute("builtinId",0))));
        }
    }
}

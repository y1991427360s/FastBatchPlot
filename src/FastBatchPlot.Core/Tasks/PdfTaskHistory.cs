using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Xml;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.Core.Tasks
{
    public enum PdfTaskMergeState { Pending, Running, Succeeded, Failed, Cancelled }

    [DataContract]
    public sealed class PdfTaskRecord
    {
        [DataMember(IsRequired=true)] internal int SchemaVersion=1;
        [DataMember(IsRequired=true)] public string Id {get;set;}="";
        [DataMember(IsRequired=true)] public long Revision {get;internal set;}
        [DataMember(IsRequired=true)] public long CreatedUtcTicks {get;set;}
        [DataMember(IsRequired=true)] public long UpdatedUtcTicks {get;set;}
        [DataMember(IsRequired=true)] public List<PdfTaskPage> Pages {get;set;}=new List<PdfTaskPage>();
        [DataMember(IsRequired=true)] public string MergedOutputPath {get;set;}="";
        [DataMember(IsRequired=true)] public PdfTaskMergeState MergeState {get;set;}
        [DataMember(IsRequired=true)] public string MergeError {get;set;}="";
        [DataMember(IsRequired=true)] public string MergedSha256 {get;set;}="";
        [DataMember(IsRequired=true)] public Dictionary<string,string> SourceRevisions {get;set;}=new Dictionary<string,string>();
        [DataMember(EmitDefaultValue=false)] public string? PlotResourceRevision {get;set;}
        [DataMember(Name="Config",IsRequired=true)] internal PdfTaskConfigData Configuration=null!;
        public PlotConfig Config=>Configuration.ToConfig();
    }

    [DataContract]
    public sealed class PdfTaskPage
    {
        [DataMember(Name="Frame",IsRequired=true)] internal PdfTaskFrameData Snapshot=null!;
        public PlotFrame Frame=>Snapshot.ToFrame();
        [DataMember(IsRequired=true)] public string OutputPath {get;set;}="";
        [DataMember(IsRequired=true)] public BatchPageState State {get;set;}
        [DataMember(IsRequired=true)] public string Error {get;set;}="";
        [DataMember(IsRequired=true)] public string Sha256 {get;set;}="";
        [DataMember(IsRequired=true)] public long Bytes {get;set;}
    }

    // 窄 DTO：新增其他产品设置不会自动进入磁盘快照，尤其不保存资产或内存授权。
    [DataContract]
    internal sealed class PdfTaskConfigData
    {
        [DataMember(IsRequired=true)] public string Device="",Style="",Directory="",MergedName="",Bookmark="";
        [DataMember(IsRequired=true)] public bool Merge,AutoOrientation,OuterBorder;
        [DataMember(EmitDefaultValue=false)] public PdfOutputOptions? PdfOptions;
        [DataMember(IsRequired=true)] public double Margin;
        [DataMember(EmitDefaultValue=false)] public double? OuterBorderInset;
        [DataMember(EmitDefaultValue=false)] public bool? Overwrite;
        [DataMember(IsRequired=true)] public PageMargins Margins=null!;
        public static PdfTaskConfigData From(PlotConfig c)
        {
            if(c.SendToPrinter||c.ExportFormat!=PlotExportFormat.PDF||c.Copies!=1)
                throw new InvalidDataException("任务历史仅支持单份 PDF 文件输出。");
            return new PdfTaskConfigData {Device=c.PrinterDevice,Style=c.PlotStyleTable,Directory=c.OutputDirectory,
                MergedName=c.MergedFileName,Bookmark=c.BookmarkTemplate,Merge=c.MergeToSinglePdf,
                AutoOrientation=c.AutoOrientation,OuterBorder=c.PrintOuterBorderLine,OuterBorderInset=c.OuterBorderInsetMm,Overwrite=c.OverwriteExisting,Margin=c.MarginMm,Margins=c.Margins.Copy(),PdfOptions=c.PdfOptions?.Copy()};
        }
        public PlotConfig ToConfig()=>new PlotConfig {PrinterDevice=Device,PlotStyleTable=Style,OutputDirectory=Directory,
            MergedFileName=MergedName,BookmarkTemplate=Bookmark,MergeToSinglePdf=Merge,AutoOrientation=AutoOrientation,
            PrintOuterBorderLine=OuterBorder,OuterBorderInsetMm=OuterBorderInset??0.25,OverwriteExisting=Overwrite??false,MarginMm=Margin,Margins=Margins.Copy(),ExportFormat=PlotExportFormat.PDF,
            Copies=1,SendToPrinter=false,PrintSignatures=true,PrintStamps=true,PdfOptions=PdfOptions?.Copy() ?? new PdfOutputOptions()};
    }

    [DataContract]
    internal sealed class PdfTaskFrameData
    {
        [DataMember(IsRequired=true)] public int Id,Order,LayoutOrder,Priority;
        [DataMember(IsRequired=true)] public FrameType Type;
        [DataMember(IsRequired=true)] public double MinX,MinY,MaxX,MaxY,Rotation,Scale,PaperWidth,PaperHeight;
        [DataMember(IsRequired=true)] public bool Landscape,PaperLandscape;
        [DataMember(IsRequired=true)] public string Block="",Layer="",Handle="",Layout="",Template="",Document="",Space="",FileName="",Paper="",Standard="";
        [DataMember(IsRequired=true)] public string[] Title=Array.Empty<string>();
        [DataMember(IsRequired=true)] public Dictionary<string,string> Attributes=new Dictionary<string,string>();
        [DataMember(EmitDefaultValue=false)] public string? SourceFileName;
        [DataMember(EmitDefaultValue=false)] public TemplateRegion? Crop;
        [DataMember(EmitDefaultValue=false)] public double[]? Bounds;
        public static PdfTaskFrameData From(PlotFrame f)
        {
            var t=f.TitleInfo;
            return new PdfTaskFrameData {Id=f.Id,Order=f.OrderIndex,LayoutOrder=f.LayoutOrder,Priority=f.TemplatePriority,Type=f.Type,
                MinX=f.MinX,MinY=f.MinY,MaxX=f.MaxX,MaxY=f.MaxY,Rotation=f.RotationDegrees,Scale=f.CalculatedScale,
                Landscape=f.IsLandscape,PaperWidth=f.DetectedPaper.WidthMm,PaperHeight=f.DetectedPaper.HeightMm,PaperLandscape=f.DetectedPaper.IsLandscape,
                Block=f.SourceBlockName,Layer=f.SourceLayer,Handle=f.HandleOrId,Layout=f.LayoutName,Template=f.TemplateId,
                SourceFileName=f.SourceFileName,Document=f.SourceDocumentId,Space=f.SourceLayoutId,FileName=f.CustomOutputFileName,Paper=f.DetectedPaper.Name,Standard=f.DetectedPaper.StandardName,
                Title=new[]{t.DrawingNo,t.DrawingName,t.ProjectName,t.SubProject,t.Stage,t.Discipline,t.Revision,t.Date,t.Scale,t.Designer,t.Checker,t.Approver},
                Attributes=new Dictionary<string,string>(t.RawAttributes,StringComparer.OrdinalIgnoreCase),
                Crop=f.AppliedPrintRegion==null?null:TemplateCropGeometry.Copy(f.AppliedPrintRegion),
                Bounds=f.PrintBounds.HasValue?new[]{f.PrintBounds.Value.MinX,f.PrintBounds.Value.MinY,f.PrintBounds.Value.MaxX,f.PrintBounds.Value.MaxY}:null};
        }
        public PlotFrame ToFrame()=>new PlotFrame {Id=Id,OrderIndex=Order,LayoutOrder=LayoutOrder,TemplatePriority=Priority,Type=Type,
            MinX=MinX,MinY=MinY,MaxX=MaxX,MaxY=MaxY,RotationDegrees=Rotation,CalculatedScale=Scale,IsLandscape=Landscape,
            DetectedPaper=new PaperSize(Paper,PaperWidth,PaperHeight,PaperLandscape){StandardName=Standard},
            SourceFileName=SourceFileName??"",SourceBlockName=Block,SourceLayer=Layer,HandleOrId=Handle,LayoutName=Layout,TemplateId=Template,SourceDocumentId=Document,SourceLayoutId=Space,
            CustomOutputFileName=FileName,IsSelected=true,TitleInfo=new TitleBlockInfo {DrawingNo=Title[0],DrawingName=Title[1],ProjectName=Title[2],
                SubProject=Title[3],Stage=Title[4],Discipline=Title[5],Revision=Title[6],Date=Title[7],Scale=Title[8],Designer=Title[9],Checker=Title[10],Approver=Title[11],
                RawAttributes=new Dictionary<string,string>(Attributes,StringComparer.OrdinalIgnoreCase)},
            AppliedPrintRegion=Crop==null?null:TemplateCropGeometry.Copy(Crop),
            PrintBounds=Bounds==null?(Rect2D?)null:new Rect2D(Bounds[0],Bounds[1],Bounds[2],Bounds[3])};
    }

    public static class PdfTaskHistory
    {
        private const int Limit=16*1024*1024;
        private const int MaximumPages=10000;
        private static readonly ConcurrentDictionary<Type,IReadOnlyDictionary<string,Type>> Contracts =
            new ConcurrentDictionary<Type,IReadOnlyDictionary<string,Type>>();
        private static DataContractJsonSerializer Serializer()=>new DataContractJsonSerializer(typeof(PdfTaskRecord));

        public static PdfTaskRecord Create(BatchPlotRun run,string mergedPath)
        {
            if(run==null)throw new ArgumentNullException(nameof(run));
            var now=DateTime.UtcNow.Ticks;
            var record=new PdfTaskRecord {Id=Guid.NewGuid().ToString("N"),CreatedUtcTicks=now,UpdatedUtcTicks=now,
                Configuration=PdfTaskConfigData.From(run.Config),MergedOutputPath=mergedPath??""};
            foreach(var p in run.Pages)record.Pages.Add(new PdfTaskPage {Snapshot=PdfTaskFrameData.From(p.Frame),OutputPath=p.OutputPath});
            Validate(record);
            // 上面已经验证完整快照；逐页初始化不再重复验证全部页面。
            for(int i=0;i<run.Pages.Count;i++)RecordPageValidated(record,i,run.Pages[i].State,run.Pages[i].Error);
            return record;
        }

        // 本进程最近一次写入的状态：文件长度和修改时间都未变时，说明没有其他窗口改写，可跳过整份重新解析。
        private static readonly ConcurrentDictionary<string,(string Id,long Revision,long Length,long Ticks)> LastWritten=
            new ConcurrentDictionary<string,(string,long,long,long)>(StringComparer.OrdinalIgnoreCase);

        public static void Save(string path,PdfTaskRecord record)
        {
            Validate(record);
            string full=Path.GetFullPath(path),dir=Path.GetDirectoryName(full)!;
            if(record.Pages.Any(p=>SamePath(p.OutputPath,full))||(!string.IsNullOrEmpty(record.MergedOutputPath)&&SamePath(record.MergedOutputPath,full)))
                throw new IOException("历史文件不能与 PDF 输出路径相同。");
            Directory.CreateDirectory(dir);
            using(var guard=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                if(File.Exists(full))
                {
                    var info=new FileInfo(full);
                    bool unchanged=LastWritten.TryGetValue(full,out var last)&&last.Id==record.Id&&last.Revision==record.Revision&&
                        last.Length==info.Length&&last.Ticks==info.LastWriteTimeUtc.Ticks;
                    if(!unchanged)
                    {
                        var old=Load(full);
                        if(old.Id!=record.Id||old.Revision!=record.Revision)throw new IOException("任务历史已被其他窗口更新，未覆盖。");
                    }
                }
                else if(record.Revision!=0)throw new IOException("原任务历史已丢失，未重新创建或覆盖。");
                // 同步序列化已验证的内存 DTO；无需走针对不可信磁盘 JSON 的往返解析。
                // 只替换标量头，不提前修改调用者的 Revision；提交失败时原对象保持原版本。
                var copy=new PdfTaskRecord {SchemaVersion=record.SchemaVersion,Id=record.Id,
                    Revision=checked(record.Revision+1),CreatedUtcTicks=record.CreatedUtcTicks,
                    UpdatedUtcTicks=Math.Max(DateTime.UtcNow.Ticks,record.UpdatedUtcTicks),Pages=record.Pages,
                    MergedOutputPath=record.MergedOutputPath,MergeState=record.MergeState,MergeError=record.MergeError,
                    MergedSha256=record.MergedSha256,SourceRevisions=record.SourceRevisions,PlotResourceRevision=record.PlotResourceRevision,Configuration=record.Configuration};
                byte[] data=Serialize(copy);
                string temp=Path.Combine(dir,".pdf-task-"+Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(data,0,data.Length);output.Flush(true);}
                    if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);
                    record.Revision=copy.Revision;record.UpdatedUtcTicks=copy.UpdatedUtcTicks;
                    var written=new FileInfo(full);
                    LastWritten[full]=(record.Id,record.Revision,written.Length,written.LastWriteTimeUtc.Ticks);
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }

        public static PdfTaskRecord Load(string path)
        {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length==0||file.Length>Limit)throw new InvalidDataException("任务历史为空或超过16MB。");
                using(var buffer=new MemoryStream()){file.CopyTo(buffer);return Deserialize(buffer.ToArray());}
            }
        }

        public static void RecordPage(PdfTaskRecord record,int index,BatchPageState state,string error)
        {
            Validate(record);
            RecordPageValidated(record,index,state,error);
        }
        private static void RecordPageValidated(PdfTaskRecord record,int index,BatchPageState state,string error)
        {
            if(index<0||index>=record.Pages.Count)throw new ArgumentOutOfRangeException(nameof(index));
            if(!Enum.IsDefined(typeof(BatchPageState),state))throw new ArgumentException("未知页面状态。");
            CheckText(error,32768);
            var page=record.Pages[index];
            string hash="";long bytes=0;
            if(state==BatchPageState.Succeeded)InspectFile(page,record.Config,out hash,out bytes);
            // 文件验证失败时保留原记录，不能把不可信文件标为成功。
            page.State=state;page.Error=error;page.Sha256=hash;page.Bytes=bytes;
            record.UpdatedUtcTicks=Math.Max(record.UpdatedUtcTicks,DateTime.UtcNow.Ticks);
        }

        public static void ValidateCompleted(PdfTaskRecord record)
        {
            Validate(record);
            if(record.Pages.Any(p=>p.State!=BatchPageState.Succeeded))throw new InvalidOperationException("任务尚有未成功页面。");
            VerifySucceeded(record);
        }

        public static List<BatchPage> PendingPages(PdfTaskRecord record)
        {
            Validate(record);VerifySucceeded(record);
            var pending=record.Pages.Where(p=>p.State!=BatchPageState.Succeeded).ToList();
            bool overwrite=record.Config.OverwriteExisting;
            foreach(var page in pending)
                if(Directory.Exists(page.OutputPath)||(!overwrite&&File.Exists(page.OutputPath)))throw new IOException("未完成页面的目标已有未知文件或目录，禁止覆盖或自动认作成功："+page.OutputPath);
            return pending.Select(p=>new BatchPage(p.Frame,p.OutputPath)).ToList();
        }

        private static void VerifySucceeded(PdfTaskRecord record)
        {
            foreach(var p in record.Pages.Where(p=>p.State==BatchPageState.Succeeded))
            {
                InspectFile(p,record.Config,out string hash,out long bytes);
                if(hash!=p.Sha256||bytes!=p.Bytes)throw new IOException("已完成 PDF 已变化，停止恢复："+p.OutputPath);
            }
        }

        private static void InspectFile(PdfTaskPage page,PlotConfig config,out string hash,out long bytes)
        {
            // 同一个读共享锁覆盖计划验证和摘要读取，避免校验期间文件被替换/写入。
            using(var stream=new FileStream(page.OutputPath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                PlotOutputCommitter.Validate(page.OutputPath,PlotPlanBuilder.Create(page.Frame,config));
                bytes=stream.Length;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            }
        }

        private static byte[] Serialize(PdfTaskRecord record)
        {
            using(var buffer=new MemoryStream())
            {Serializer().WriteObject(buffer,record);if(buffer.Length>Limit)throw new InvalidDataException("任务历史超过16MB。");return buffer.ToArray();}
        }
        private static PdfTaskRecord Deserialize(byte[] bytes)
        {
            try
            {
                EnsureSingleJsonObject(bytes);
                // DataContract 默认忽略未知字段；先检查窄 DTO 结构，拒绝未知/重复成员。
                using(var shapeReader=Reader(bytes))
                {
                    var shape=new XmlDocument {XmlResolver=null};shape.Load(shapeReader);
                    ValidateShape(shape.DocumentElement??throw new InvalidDataException("任务历史为空。"),typeof(PdfTaskRecord));
                }
                using(var reader=Reader(bytes))
                {
                    var record=(PdfTaskRecord?)Serializer().ReadObject(reader)??throw new InvalidDataException("任务历史为空。");
                    while(reader.Read())if(reader.NodeType!=XmlNodeType.Whitespace&&reader.NodeType!=XmlNodeType.SignificantWhitespace)throw new InvalidDataException("任务历史有多余内容。");
                    Validate(record);return record;
                }
            }
            catch(Exception ex) when(ex is SerializationException||ex is XmlException||ex is ArgumentException||ex is NullReferenceException||ex is IndexOutOfRangeException)
            {throw new InvalidDataException("任务历史损坏："+ex.Message,ex);}
        }
        // 框架 JSON reader 在首个根对象后可能直接 EOF，不能据 reader.Read() 判断是否还有尾随垃圾。
        private static void EnsureSingleJsonObject(byte[] bytes)
        {
            int i=bytes.Length>=3 && bytes[0]==239 && bytes[1]==187 && bytes[2]==191?3:0;
            while(i<bytes.Length && JsonSpace(bytes[i]))i++;
            if(i>=bytes.Length || bytes[i]!=(byte)'{')throw new InvalidDataException("任务历史必须是单个 JSON 对象。");
            int depth=0;bool quoted=false,escaped=false;
            for(;i<bytes.Length;i++)
            {
                byte b=bytes[i];
                if(quoted)
                {
                    if(escaped){escaped=false;continue;}
                    if(b==92)escaped=true;else if(b==34)quoted=false;
                    continue;
                }
                if(b==34){quoted=true;continue;}
                if(b==123 || b==91)depth++;
                else if(b==125 || b==93)
                {
                    depth--;
                    if(depth==0)
                    {
                        for(i++;i<bytes.Length;i++)if(!JsonSpace(bytes[i]))throw new InvalidDataException("任务历史有多余内容。");
                        return;
                    }
                }
            }
            throw new InvalidDataException("任务历史 JSON 未完整结束。");
        }
        private static bool JsonSpace(byte b)=>b==32 || b==9 || b==10 || b==13;
        private static XmlDictionaryReader Reader(byte[] bytes)=>JsonReaderWriterFactory.CreateJsonReader(bytes,
            new XmlDictionaryReaderQuotas {MaxDepth=32,MaxArrayLength=MaximumPages*64,MaxStringContentLength=Limit,MaxBytesPerRead=4096,MaxNameTableCharCount=65536});
        private static void ValidateShape(XmlElement element,Type type)
        {
            var children=element.ChildNodes.OfType<XmlElement>().ToList();
            if(type.IsArray || (type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>)))
            {
                Type itemType=type.IsArray?type.GetElementType()!:type.GetGenericArguments()[0];
                foreach(var child in children){if(child.LocalName!="item")throw new InvalidDataException("任务历史数组成员无效。");ValidateShape(child,itemType);}return;
            }
            if(type==typeof(Dictionary<string,string>))
            {
                foreach(var child in children)
                {
                    var entries=child.ChildNodes.OfType<XmlElement>().ToList();
                    if(child.LocalName!="item"||entries.Count!=2||entries.Select(e=>e.LocalName).Distinct().Count()!=2||entries.Any(e=>e.LocalName!="Key"&&e.LocalName!="Value"))
                        throw new InvalidDataException("任务历史字典成员无效。");
                    foreach(var entry in entries)ValidateShape(entry,typeof(string));
                }
                return;
            }
            if(type.GetCustomAttribute<DataContractAttribute>()==null)
            {if(children.Count!=0)throw new InvalidDataException("任务历史标量内容无效。");return;}
            var members=Contracts.GetOrAdd(type,ReadContract);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var child in children)
            {
                if(!seen.Add(child.LocalName)||!members.TryGetValue(child.LocalName,out Type? memberType))throw new InvalidDataException("任务历史包含未知或重复成员："+child.LocalName);
                ValidateShape(child,memberType);
            }
        }
        private static IReadOnlyDictionary<string,Type> ReadContract(Type type)
        {
            var members=new Dictionary<string,Type>(StringComparer.Ordinal);
            foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            {
                var attribute=member.GetCustomAttribute<DataMemberAttribute>();if(attribute==null)continue;
                var valueType=member is FieldInfo field?field.FieldType:((PropertyInfo)member).PropertyType;
                members.Add(attribute.Name??member.Name,valueType);
            }
            return members;
        }
        private static void Validate(PdfTaskRecord r)
        {
            if(r==null||r.SchemaVersion!=1||!Guid.TryParseExact(r.Id,"N",out _)||r.Revision<0||r.Configuration==null||r.Pages==null||r.Pages.Count<1||r.Pages.Count>MaximumPages||r.SourceRevisions==null)
                throw new InvalidDataException("任务历史结构或版本无效。");
            if(r.CreatedUtcTicks<=0||r.CreatedUtcTicks>DateTime.MaxValue.Ticks||r.UpdatedUtcTicks<r.CreatedUtcTicks||r.UpdatedUtcTicks>DateTime.MaxValue.Ticks)
                throw new InvalidDataException("任务历史时间无效。");
            if(r.PlotResourceRevision!=null){CheckText(r.PlotResourceRevision,128);if(string.IsNullOrWhiteSpace(r.PlotResourceRevision))throw new InvalidDataException("打印资源标记无效。");}
            var c=r.Configuration;
            foreach(string s in new[]{c.Device,c.Style,c.Directory,c.MergedName,c.Bookmark})CheckText(s,32768);
            if(string.IsNullOrWhiteSpace(c.Device)||string.IsNullOrWhiteSpace(c.Style)||c.Margins==null)throw new InvalidDataException("PDF 设备、样式或留白设置缺失。");
            ValidateAbsolutePath(c.Directory,true);
            var config=r.Config;
            if(!Enum.IsDefined(typeof(PdfTaskMergeState),r.MergeState))throw new InvalidDataException("未知合并状态。");
            CheckText(r.MergeError,32768);CheckHash(r.MergedSha256,r.MergeState==PdfTaskMergeState.Succeeded);
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var p in r.Pages)
            {
                if(p==null||p.Snapshot==null||!Enum.IsDefined(typeof(BatchPageState),p.State))throw new InvalidDataException("页面状态或快照缺失。");
                AddPath(paths,p.OutputPath,c.Directory);CheckText(p.Error,32768);CheckHash(p.Sha256,p.State==BatchPageState.Succeeded);
                if(p.State==BatchPageState.Succeeded?p.Bytes<=0:p.Bytes!=0)throw new InvalidDataException("页面文件大小与状态不一致。");
                var f=p.Snapshot;
                if(f.Title==null||f.Title.Length!=12||f.Attributes==null||f.Attributes.Count>4096||!Enum.IsDefined(typeof(FrameType),f.Type))throw new InvalidDataException("图框标题或类型无效。");
                if(f.SourceFileName!=null)CheckText(f.SourceFileName,32768);
                foreach(string s in new[]{f.Block,f.Layer,f.Handle,f.Layout,f.Template,f.Document,f.Space,f.FileName,f.Paper,f.Standard}.Concat(f.Title))CheckText(s,32768);
                if(string.IsNullOrWhiteSpace(f.Document)||string.IsNullOrWhiteSpace(f.Space))throw new InvalidDataException("原图文档/空间会话身份缺失，不能自动重新绑定。");
                foreach(var pair in f.Attributes){CheckText(pair.Key,32768);CheckText(pair.Value,32768);}
                if(f.Attributes.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=f.Attributes.Count)throw new InvalidDataException("标题属性名称重复。");
                foreach(double n in new[]{f.MinX,f.MinY,f.MaxX,f.MaxY,f.Rotation})if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("图框坐标无效。");
                if(f.MaxX<=f.MinX||f.MaxY<=f.MinY)throw new InvalidDataException("图框宽高无效。");
                if(f.Bounds!=null&&f.Bounds.Length!=4)throw new InvalidDataException("裁切范围无效。");
                if(f.Crop!=null){TemplateCropGeometry.Validate(f.Crop);if(f.Bounds==null)throw new InvalidDataException("模板裁切缺少实际范围。");}
                PlotPlanBuilder.Create(p.Frame,config);
            }
            CheckText(r.MergedOutputPath,32768);
            if(!string.IsNullOrEmpty(r.MergedOutputPath))AddPath(paths,r.MergedOutputPath,c.Directory);
            if(config.MergeToSinglePdf&&string.IsNullOrEmpty(r.MergedOutputPath))throw new InvalidDataException("合并目标缺失。");
            if(r.MergeState==PdfTaskMergeState.Succeeded&&(string.IsNullOrEmpty(r.MergedOutputPath)||r.Pages.Any(p=>p.State!=BatchPageState.Succeeded)))throw new InvalidDataException("合并成功状态缺少完成页面或目标。");
            var documents=new HashSet<string>(r.Pages.Select(p=>p.Snapshot.Document),StringComparer.Ordinal);
            foreach(var pair in r.SourceRevisions)
            {
                if(!documents.Contains(pair.Key))throw new InvalidDataException("源图修订标记不属于任务中的文档。");
                CheckText(pair.Value,128);
            }
        }
        private static void CheckText(string text,int max){if(text==null||text.Length>max||text.IndexOf('\0')>=0)throw new InvalidDataException("任务历史文本无效或过长。");}
        private static void CheckHash(string hash,bool required)
        {
            if(hash==null||(required?(hash.Length!=64||hash.Any(ch=>!(ch>='0'&&ch<='9')&&!(ch>='A'&&ch<='F'))):hash.Length!=0))throw new InvalidDataException("任务文件摘要与状态不一致。");
        }
        private static void AddPath(HashSet<string> paths,string path,string directory)
        {
            ValidateAbsolutePath(path,false);
            if(!string.Equals(Path.GetDirectoryName(path)?.TrimEnd('\\','/'),directory.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)||
                !string.Equals(Path.GetExtension(path),".pdf",StringComparison.OrdinalIgnoreCase)||!paths.Add(Path.GetFullPath(path)))
                throw new InvalidDataException("PDF 输出必须使用指定输出目录内唯一的完整绝对路径。");
        }
        private static void ValidateAbsolutePath(string path,bool directory)
        {
            CheckText(path,32768);
            try
            {
                if(string.IsNullOrWhiteSpace(path)||path.StartsWith(@"\\?\",StringComparison.Ordinal)||path.StartsWith(@"\\.\",StringComparison.Ordinal)||
                    !Path.IsPathRooted(path)||!string.Equals(Path.GetFullPath(path),path,StringComparison.OrdinalIgnoreCase)||Path.GetPathRoot(path)?.Length<3)
                    throw new InvalidDataException("输出目录及文件必须使用普通完整绝对路径。");
                string root=Path.GetPathRoot(path)!;
                string rest=path.Substring(root.Length);
                if(directory)rest=rest.TrimEnd('\\','/');
                else if(rest.Length==0||rest.EndsWith("\\",StringComparison.Ordinal)||rest.EndsWith("/",StringComparison.Ordinal))
                    throw new InvalidDataException("输出文件名缺失。");
                var parts=rest.Split(new[]{'\\','/'},StringSplitOptions.None);
                if(directory&&rest.Length==0)parts=Array.Empty<string>();
                foreach(string part in parts)
                {
                    if(part.Length==0||part!=part.Trim()||part.EndsWith(".",StringComparison.Ordinal)||
                        part.Any(ch=>ch<32||"<>:\"/\\|?*".IndexOf(ch)>=0))throw new InvalidDataException("输出路径含非法名称、数据流或尾随空格/句点。");
                    string stem=part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
                    if(new[]{"CON","PRN","AUX","NUL","CONIN$","CONOUT$","CLOCK$"}.Contains(stem)||
                        (stem.Length==4&&(stem.StartsWith("COM",StringComparison.Ordinal)||stem.StartsWith("LPT",StringComparison.Ordinal))&&"123456789¹²³".IndexOf(stem[3])>=0))
                        throw new InvalidDataException("输出路径含 Windows 保留设备名称。");
                }
            }
            catch(Exception ex) when(ex is ArgumentException||ex is NotSupportedException||ex is PathTooLongException)
            {throw new InvalidDataException("输出路径无效。",ex);}
        }
        private static bool SamePath(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
    }
}

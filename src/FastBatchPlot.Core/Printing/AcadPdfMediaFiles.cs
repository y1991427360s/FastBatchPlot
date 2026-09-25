using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Printing
{
    /// <summary>AutoCAD PDF 的任务私有 PC3/PMP；必须由宿主回读配置路径和实际纸张后才能出图。</summary>
    public sealed partial class AcadPdfMediaFiles : IDisposable
    {
        public string ConfigurationPath {get;}
        public string PmpPath {get;}
        public string MediaName {get;}
        private readonly string directory;
        private FileStream? pc3Lease,pmpLease;
        private bool disposed;
        private AcadPdfMediaFiles(string directory,string name)
        {this.directory=directory;MediaName=name;ConfigurationPath=Path.Combine(directory,name+".pc3");PmpPath=Path.Combine(directory,name+".pmp");}

        public static AcadPdfMediaFiles Create(string sourcePc3Path,double widthMm,double heightMm,string temporaryRoot)
            => Create(sourcePc3Path,widthMm,heightMm,temporaryRoot,null);

        public static AcadPdfMediaFiles Create(string sourcePc3Path,double widthMm,double heightMm,string temporaryRoot,PdfOutputOptions? options)
        {
            string width=Number(widthMm),height=Number(heightMm),area=Number(widthMm*heightMm);
            if(!string.Equals(Path.GetExtension(sourcePc3Path),".pc3",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("源设备必须为 PC3 文件。");
            if(string.IsNullOrWhiteSpace(temporaryRoot)||temporaryRoot.Any(c=>char.IsControl(c)))throw new ArgumentException("临时配置目录无效。");
            var source=PiaConfiguration.Read(sourcePc3Path);var meta=source.Root.Child("meta");
            Require(meta,"canonical_family_name","\"Autodesk ePlot");Require(meta,"canonical_model_name","\"pdf");
            Require(meta,"driver_type","3");Require(meta,"file_only","TRUE");Require(meta,"config_autospool","FALSE");
            string driver=Path.GetFileName(meta.Text("driver_pathname"));
            if(!string.Equals(driver,"pdfplot12.hdi",StringComparison.OrdinalIgnoreCase)&&!string.Equals(driver,"pdfplot14.hdi",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("仅支持 AutoCAD 2018 原生 PDF 驱动配置。");
            foreach(string key in new[]{"short_net_name","friendly_net_name"})if(meta.Text(key)!="")throw new InvalidDataException("网络设备不支持自动纸张。");
            var inheritedPmp=ReadLinkedPmp(sourcePc3Path,meta);
            var io=source.Root.Child("io");Require(io,"type","2");Require(io,"plot_to_file","TRUE");
            if(io.Text("pathname")!="")throw new InvalidDataException("源配置绑定了外部输出路径，不能自动生成纸张。");
            ApplyOptions(source,options);
            if(inheritedPmp!=null)ApplyLinkedOptions(inheritedPmp,source,options);
            string root=Path.GetFullPath(temporaryRoot),token="FBP_"+Guid.NewGuid().ToString("N");
            var files=new AcadPdfMediaFiles(Path.Combine(root,token),token);
            meta.SetText("user_defined_model_pathname",files.PmpPath);meta.SetText("user_defined_model_basename",Path.GetFileName(files.PmpPath));
            // 驱动路径由原始受支持配置保留；不猜测替换本机 SDK/驱动版本。
            string descriptionName=token+"_Description";
            var media=source.Root.Child("media");media.SetRaw("selection_method","2");media.SetRaw("number_of_copies","1");
            Bounds(media,"actual_",width,height);
            var size=new PiaNode();size.SetText("name",token);size.SetRaw("group","15");size.SetRaw("landscape_mode",widthMm>=heightMm?"TRUE":"FALSE");size.SetRaw("longplot_reduction","1.0");
            var description=size.Add("media_description");Bounds(description,"",width,height);description.SetRaw("printable_area",area);description.SetRaw("dimensional","TRUE");
            var box=description.Add("media_bounds");box.SetRaw("urx",width);box.SetRaw("ury",height);media.SetChild("size",size);

            var pmp=inheritedPmp ?? new PiaConfiguration();
            if(inheritedPmp==null)pmp.Root.SetChild("meta",meta);
            else
            {
                pmp.Root.Child("meta").SetText("user_defined_model_pathname",files.PmpPath);
                pmp.Root.Child("meta").SetText("user_defined_model_basename",Path.GetFileName(files.PmpPath));
            }
            var udm=GetOrAdd(pmp.Root,"udm");
            if(inheritedPmp==null){var calibration=udm.Add("calibration");calibration.SetRaw("_x","1.0");calibration.SetRaw("_y","1.0");}
            var custom=GetOrAdd(udm,"media");
            // 此处只描述本次纸张，不提高驱动能力位或宣称超过驱动允许的最大尺寸。
            var sizes=GetOrAdd(custom,"size");var descriptions=GetOrAdd(custom,"description");
            string newIndex=NextPaperIndex(sizes,descriptions);
            var customSize=sizes.Add(newIndex);customSize.SetRaw("caps_type","2");customSize.SetText("name",token);customSize.SetText("localized_name",token);
            customSize.SetText("media_description_name",descriptionName);customSize.SetRaw("media_group","15");customSize.SetRaw("landscape_mode",widthMm>=heightMm?"TRUE":"FALSE");
            var customDescription=descriptions.Add(newIndex);customDescription.SetRaw("caps_type","2");customDescription.SetText("name",descriptionName);
            customDescription.SetRaw("media_bounds_urx",width);customDescription.SetRaw("media_bounds_ury",height);Bounds(customDescription,"",width,height);
            customDescription.SetRaw("printable_area",area);customDescription.SetRaw("dimensional","TRUE");
            byte[] pc3Bytes=source.Encode(),pmpBytes=pmp.Encode();
            bool ownDirectory=false,ownPc3=false,ownPmp=false;
            try
            {
                Directory.CreateDirectory(root);
                if(Directory.Exists(files.directory)||File.Exists(files.directory))throw new IOException("独立配置目录已存在。");
                Directory.CreateDirectory(files.directory);ownDirectory=true;
                using(var file=new FileStream(files.PmpPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){ownPmp=true;file.Write(pmpBytes,0,pmpBytes.Length);file.Flush(true);}
                using(var file=new FileStream(files.ConfigurationPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){ownPc3=true;file.Write(pc3Bytes,0,pc3Bytes.Length);file.Flush(true);}
                files.pc3Lease=new FileStream(files.ConfigurationPath,FileMode.Open,FileAccess.Read,FileShare.Read);
                files.pmpLease=new FileStream(files.PmpPath,FileMode.Open,FileAccess.Read,FileShare.Read);
                CheckBytes(files.pc3Lease,pc3Bytes);CheckBytes(files.pmpLease,pmpBytes);
                return files;
            }
            catch
            {
                files.pc3Lease?.Dispose();files.pmpLease?.Dispose();
                if(ownPc3)TryDelete(files.ConfigurationPath);if(ownPmp)TryDelete(files.PmpPath);
                if(ownDirectory)try{Directory.Delete(files.directory,false);}catch(IOException){}catch(UnauthorizedAccessException){}
                throw;
            }
        }
        private static void ApplyOptions(PiaConfiguration source,PdfOutputOptions? options)
        {
            if(options==null)return;
            options.Validate();
            if(!options.HasOverrides)return;
            if(options.MergeLines.HasValue)
            {
                var color=source.Root.Child("res_color_mem");
                string overwrite=color.Raw("lines_overwrite");
                if(overwrite!="TRUE" && overwrite!="FALSE")
                    throw new InvalidDataException("AutoCAD PDF 的直线覆盖字段必须为 TRUE 或 FALSE。");
                // pc3edit 的摘要把 FALSE 显示为直线合并、TRUE 显示为直线覆盖。
                color.SetRaw("lines_overwrite",options.MergeLines.Value?"FALSE":"TRUE");
            }
            if(options.VectorResolutionDpi.HasValue)
            {
                string dpi=Dpi(options.VectorResolutionDpi.Value);
                Custom(source,"Hardcopy_Resolution").SetRaw("value",dpi);
                var resolution=source.Root.Child("res_color_mem").Child("resolution");
                foreach(string field in new[]{"phys_resolution_x","phys_resolution_y","effective_resolution_x","effective_resolution_y"})
                {
                    resolution.Raw(field); // 必须已存在，不能向未知驱动结构补造字段。
                    resolution.SetRaw(field,dpi+".0");
                }
                // custom/Resolution 是驱动枚举，保留源值，不把 DPI 写成枚举编号。
            }
            if(options.RasterResolutionDpi.HasValue)
            {
                string dpi=Dpi(options.RasterResolutionDpi.Value);
                Custom(source,"Raster_Limit").SetRaw("value",dpi);
                Custom(source,"Monochrome_Raster_Limit").SetRaw("value",dpi);
                Custom(source,"Custom_Raster_Resolution").SetRaw("value","TRUE");
                Custom(source,"Custom_Monochrome_Resolution").SetRaw("value","TRUE");
            }
            if(options.TextToGeometry.HasValue)
                Custom(source,"All_As_Geometry").SetRaw("value",options.TextToGeometry.Value?"TRUE":"FALSE");
            if(options.IncludeLayers.HasValue)
                Custom(source,"Include_Layer").SetRaw("value",options.IncludeLayers.Value?"TRUE":"FALSE");
        }
        private static PiaNode Custom(PiaConfiguration source,string name)
        {
            var matches=source.Root.Child("custom").Children.Values
                .Where(node=>node.Values.ContainsKey("name")&&node.Text("name")==name).ToList();
            if(matches.Count!=1)throw new InvalidDataException("PDF 配置参数缺失或重复："+name);
            matches[0].Raw("value");return matches[0];
        }
        private static string Dpi(int value)
        {return value.ToString(CultureInfo.InvariantCulture);}

        public void Dispose()
        {
            if(disposed)return;disposed=true;pc3Lease?.Dispose();pmpLease?.Dispose();
            var failures=new List<Exception>();
            foreach(var file in new[]{ConfigurationPath,PmpPath})try{File.Delete(file);}catch(Exception ex){failures.Add(ex);}
            try{if(!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory,false);}catch(Exception ex){failures.Add(ex);}
            if(failures.Count>0)throw new IOException("临时 PDF 配置清理失败，请检查残留目录："+directory,new AggregateException(failures));
        }
        private static void Bounds(PiaNode node,string prefix,string width,string height)
        {node.SetRaw(prefix+"printable_bounds_llx","0.0");node.SetRaw(prefix+"printable_bounds_lly","0.0");node.SetRaw(prefix+"printable_bounds_urx",width);node.SetRaw(prefix+"printable_bounds_ury",height);}
        private static string Number(double number)
        {if(double.IsNaN(number)||double.IsInfinity(number)||number<=0||number<0.000001)throw new ArgumentException("纸张尺寸/面积必须为可表示的有限正数。");return number.ToString("0.000000",CultureInfo.InvariantCulture);}
        private static void Require(PiaNode node,string key,string expected)
        {if(node.Raw(key)!=expected)throw new InvalidDataException("PDF 驱动配置不受支持："+key);}
        private static void CheckBytes(FileStream stream,byte[] expected)
        {if(stream.Length!=expected.Length)throw new IOException("临时配置在取得读锁前改变。");foreach(byte value in expected)if(stream.ReadByte()!=value)throw new IOException("临时配置在取得读锁前改变。");stream.Position=0;}
        private static void TryDelete(string path){try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}

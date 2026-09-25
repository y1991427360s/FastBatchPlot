using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Printing
{
    /// <summary>仅生成私有临时 PDF 配置；文件结构正确不代表宿主已经接受介质，调用方必须读回核验。</summary>
    public sealed partial class ZwPdfMediaFiles : IDisposable
    {
        private const int MaximumSourceBytes=256*1024;
        // ZWCAD 2026 随安装提供的原生 PDF 预设；显示名不同但驱动文件一致。
        // 仍须同时核对驱动文件、配置文件、设备类型和输出端口，不能只按 PDF 名称猜测。
        private static readonly HashSet<string> NativePdfDrivers = new HashSet<string>(new[] {
            "DWG to PDF", "ZWCAD PDF(General Documentation)", "ZWCAD PDF(High Quality Print)",
            "ZWCAD PDF(Smallest File)", "ZWCAD PDF(Web and Mobile)", "M_PDF"
        }, StringComparer.OrdinalIgnoreCase);
        private readonly string directory;
        private readonly List<string> stagedFiles=new List<string>();
        private FileStream? configurationLock,pmpLock;
        private bool disposed;
        public string ConfigurationPath {get;}
        public string MediaName {get;}
        public string PmpPath {get;}
        /// <summary>宿主拒绝临时目录的绝对路径时，暂存到 CAD 打印机目录中的配置完整路径；未暂存为 null。</summary>
        public string? StagedConfigurationPath {get;private set;}
        /// <summary>宿主实际应加载的配置路径。</summary>
        public string EffectiveConfigurationPath=>StagedConfigurationPath??ConfigurationPath;
        public const string StagedPrefix="FBP_";

        private ZwPdfMediaFiles(string directory,string name)
        {
            this.directory=directory;MediaName=name;
            // 部分宿主按设备文件名缓存配置；目录、文件名和纸张标识都唯一，避免复用上页缓存。
            ConfigurationPath=Path.Combine(directory,name+".pc5");PmpPath=Path.Combine(directory,name+".pmp");
        }

        public static ZwPdfMediaFiles Create(string sourcePc5Path,double widthMm,double heightMm,string temporaryRoot)
            => Create(sourcePc5Path,widthMm,heightMm,temporaryRoot,null);

        /// <summary>创建纸张并按明确支持的字段覆盖 PDF 驱动参数；空参数表示完全跟随驱动预设。</summary>
        public static ZwPdfMediaFiles Create(string sourcePc5Path,double widthMm,double heightMm,string temporaryRoot,PdfOutputOptions? options)
        {
            string width=Dimension(widthMm),height=Dimension(heightMm);
            double actualWidth=double.Parse(width,CultureInfo.InvariantCulture),actualHeight=double.Parse(height,CultureInfo.InvariantCulture);
            string area=Dimension(actualWidth*actualHeight);
            if(string.IsNullOrWhiteSpace(sourcePc5Path)||!string.Equals(Path.GetExtension(sourcePc5Path),".pc5",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("源文件必须为 PC5 配置。");
            var encoding=Gbk();
            byte[] source;
            using(var input=new FileStream(sourcePc5Path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length==0||input.Length>MaximumSourceBytes)throw new InvalidDataException("源 PC5 为空或超过256KB。");
                using(var buffer=new MemoryStream()){input.CopyTo(buffer);source=buffer.ToArray();}
            }
            // 安装原生 PC5 为无 BOM 的 ANSI 文本，不能把其他编码当作 GBK 猜测读取。
            if((source.Length>=3&&source[0]==0xEF&&source[1]==0xBB&&source[2]==0xBF)||
                (source.Length>=2&&((source[0]==0xFF&&source[1]==0xFE)||(source[0]==0xFE&&source[1]==0xFF))))
                throw new InvalidDataException("PC5 编码标记不受支持，应使用原生无 BOM 的 GBK 配置。");
            Ini ini;
            try{ini=Ini.Parse(encoding.GetString(source));}
            catch(DecoderFallbackException ex){throw new InvalidDataException("源 PC5 不是有效的 GBK 文本。",ex);}
            Require(ini,"Meta","PrinterType","2");
            Require(ini,"Standard","DeviceName","PDF");
            string driverName = ini.Get("Standard","DriverName") ?? "";
            string fileNameNoExt = Path.GetFileNameWithoutExtension(sourcePc5Path);
            if(!NativePdfDrivers.Contains(driverName) && !(string.IsNullOrEmpty(driverName) && NativePdfDrivers.Contains(fileNameNoExt)))
                throw new InvalidDataException("源 PC5 不是已识别的中望原生 PDF 预设。");
            Require(ini,"Standard","DriverPath","ZwPDFDriver.dll");Require(ini,"Standard","DriverCfgPath","PDF.ini");
            Require(ini,"Standard","PortName","FILE:");Require(ini,"Port","plot_to_file","1");
            Require(ini,"Caps","definepapersize_caps","1");
            var inheritedPmp=ReadLinkedPmp(sourcePc5Path,ini.Get("Meta","pmp_filepath"));
            foreach(string key in new[]{"Source","source_entry","destination_name"})Empty(ini,"Meta",key);
            foreach(string key in new[]{"ServerName","NetPrinterName"})Empty(ini,"Standard",key);
            ZeroOrEmpty(ini,"Standard","NetPrinterUSE");ZeroOrEmpty(ini,"Port","config_autospool");
            ZeroOrEmpty(ini,"Port","dmDriverExtra");ZeroOrEmpty(ini,"Port","privatedata");
            foreach(string key in new[]{"pre_init","post_init","termination"})ZeroOrEmpty(ini,"Retain",key);
            var driverKeys=new HashSet<string>(new[]{"DeviceName","DevicePath","DriverVersion","DriverName","DriverPath","DriverCfgPath",
                "PortName","DeviceComment","ServerName","LocationName","NetPrinterUSE","NetPrinterName"},StringComparer.OrdinalIgnoreCase);
            if(ini.Section("Standard").Keys.Any(key=>!driverKeys.Contains(key)))throw new InvalidDataException("PC5 包含未识别的驱动字段，不能生成独立配置。");
            ApplyOptions(ini, options);

            string root=PrivateRoot(temporaryRoot),token=Guid.NewGuid().ToString("N");
            string folder=Path.Combine(root,"fbp-pdf-"+token);
            if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("临时配置目录已存在，未覆盖。");
            var result=new ZwPdfMediaFiles(folder,"FBP_"+token);
            var meta=ini.Section("Meta");
            meta["pmp_filepath"]=result.PmpPath;meta["paper_name"]=result.MediaName;meta["paper_size_x"]=width;meta["paper_size_y"]=height;
            meta["actual_printable_bounds_llx"]="0.000000";meta["actual_printable_bounds_lly"]="0.000000";
            meta["actual_printable_bounds_urx"]=width;meta["actual_printable_bounds_ury"]=height;
            meta["physical_offsetx"]="0.000000";meta["physical_offsety"]="0.000000";
            meta["actual_x"]=width;meta["actual_y"]=height;meta["actual_area"]=area;meta["paper_unit"]="1";
            var pmp=inheritedPmp ?? Ini.Parse("[Meta]\r\npmpfilepath=\r\ndriver_path=\r\ncalibration_x=1.000000\r\ncalibration_y=1.000000\r\nmod_num=0\r\ndel_num=0\r\nuserdef_num=0\r\nhide_num=0\r\n[user]\r\n");
            AppendPaper(pmp,result.MediaName,width,height,area);
            // 所有编码/注入检查先于创建目录，不能用替换字符悄悄改变绝对 PMP 引用。
            byte[] pc5Bytes=encoding.GetBytes(ini.Render()),pmpBytes=encoding.GetBytes(pmp.Render());
            if(pc5Bytes.Length>MaximumSourceBytes || pmpBytes.Length>MaximumSourceBytes)
                throw new InvalidDataException("生成后的 PC5/PMP 超过 256KB，未创建临时配置。");
            bool ownDirectory=false,ownPc5=false,ownPmp=false;
            try
            {
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(folder);ownDirectory=true;
                using(var file=new FileStream(result.PmpPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {ownPmp=true;file.Write(pmpBytes,0,pmpBytes.Length);file.Flush(true);}
                using(var file=new FileStream(result.ConfigurationPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {ownPc5=true;file.Write(pc5Bytes,0,pc5Bytes.Length);file.Flush(true);}
                result.pmpLock=new FileStream(result.PmpPath,FileMode.Open,FileAccess.Read,FileShare.Read);
                result.configurationLock=new FileStream(result.ConfigurationPath,FileMode.Open,FileAccess.Read,FileShare.Read);
                VerifyLockedBytes(result.pmpLock,pmpBytes);VerifyLockedBytes(result.configurationLock,pc5Bytes);
                return result;
            }
            catch
            {
                result.configurationLock?.Dispose();result.pmpLock?.Dispose();
                // 仅清理本次成功创建的明确文件；不递归遍历或删除调用方目录。
                if(ownPc5)TryDelete(result.ConfigurationPath);if(ownPmp)TryDelete(result.PmpPath);
                if(ownDirectory)TryRemoveEmpty(folder);
                throw;
            }
        }

        private static void ApplyOptions(Ini ini, PdfOutputOptions? options)
        {
            if(options==null)return;
            options.Validate();
            if(!options.HasOverrides)return;
            var resolution=ini.Section("res_color_mem");
            // 配置编辑器把 0 显示为直线合并、1 显示为直线覆盖。
            if(options.MergeLines.HasValue)
                resolution["lines_overwrite"]=options.MergeLines.Value?"0":"1";
            // 原生配置可省略光栅字段；只在有分辨率覆盖时检查已知的有效组合。
            if(options.VectorResolutionDpi.HasValue || options.RasterResolutionDpi.HasValue)
            {
                foreach(string axis in new[]{"x","y"})
                {
                    int vector=options.VectorResolutionDpi ?? ReadResolution(resolution,"resolution_"+axis);
                    string rasterKey="raster_resolution_"+axis;
                    int? raster=options.RasterResolutionDpi;
                    // readPc5File 对缺少的光栅字段读取为 0，交由驱动决定；不能把 0 当成实际 DPI。
                    if(!raster.HasValue && resolution.TryGetValue(rasterKey,out var storedRaster) && storedRaster!="0")
                        raster=ReadResolution(resolution,rasterKey);
                    if(raster.HasValue && raster.Value>vector)
                        throw new ArgumentException("中望 PDF 光栅分辨率不能高于矢量分辨率；请同时调整两项参数，或恢复跟随驱动。");
                }
            }
            if(options.VectorResolutionDpi.HasValue)
            {
                if(!resolution.ContainsKey("resolution_x")||!resolution.ContainsKey("resolution_y"))
                    throw new NotSupportedException("中望 PDF 配置缺少可验证的分辨率字段。");
                resolution["resolution_x"]=options.VectorResolutionDpi.Value.ToString(CultureInfo.InvariantCulture);
                resolution["resolution_y"]=options.VectorResolutionDpi.Value.ToString(CultureInfo.InvariantCulture);
            }
            if(options.RasterResolutionDpi.HasValue)
            {
                // ZwPlotConfig 与安装配置 ZWPLOT_PDF.pc5 均使用这些独立字段。
                resolution["raster_resolution_x"]=options.RasterResolutionDpi.Value.ToString(CultureInfo.InvariantCulture);
                resolution["raster_resolution_y"]=options.RasterResolutionDpi.Value.ToString(CultureInfo.InvariantCulture);
            }
            if(options.IncludeLayers.HasValue)
                ini.Section("Retain")["layerinclude"]=options.IncludeLayers.Value?"1":"0";
            if(options.TextToGeometry.HasValue)
            {
                if(!resolution.ContainsKey("truetype_as_text"))
                    throw new NotSupportedException("中望 PDF 配置缺少 TrueType 输出字段。");
                // truetype_as_text=1 表示保留文字；文字转图形时必须为 0。
                resolution["truetype_as_text"]=options.TextToGeometry.Value?"0":"1";
            }
        }

        private static int ReadResolution(Dictionary<string,string> fields,string key)
        {
            if(!fields.TryGetValue(key,out string? text) ||
                !int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out int value) || value<72 || value>4800)
                throw new InvalidDataException("中望 PDF 配置的分辨率无效或缺失："+key+"；请明确设置矢量和光栅分辨率。");
            return value;
        }


        /// <summary>显式释放临时文件的托管读锁，以便 CAD 宿主驱动以排他或写模式打开配置文件。</summary>
        public void ReleaseLocks()
        {
            configurationLock?.Dispose();
            configurationLock = null;
            pmpLock?.Dispose();
            pmpLock = null;
        }

        /// <summary>
        /// 把私有配置暂存到 CAD 的打印机目录（部分中望版本只接受该目录中的设备名）。
        /// 暂存副本的 PMP 引用指向同目录的 PMP 副本，并在 Dispose 时一并删除，不在用户打印机列表留下失效设备。
        /// </summary>
        public string StageInto(string plottersDirectory)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ZwPdfMediaFiles));
            if(StagedConfigurationPath!=null)return StagedConfigurationPath;
            if(string.IsNullOrWhiteSpace(plottersDirectory)||!Directory.Exists(plottersDirectory))
                throw new DirectoryNotFoundException("CAD 打印机配置目录不存在："+plottersDirectory);
            string pmpDirectory=Path.Combine(plottersDirectory,"PMP Files");
            Directory.CreateDirectory(pmpDirectory);
            string stagedPmp=Path.Combine(pmpDirectory,Path.GetFileName(PmpPath));
            string stagedPc5=Path.Combine(plottersDirectory,Path.GetFileName(ConfigurationPath));
            var encoding=Gbk();
            if(File.Exists(PmpPath))
            {
                File.Copy(PmpPath,stagedPmp,true);stagedFiles.Add(stagedPmp);
            }
            string text=encoding.GetString(File.ReadAllBytes(ConfigurationPath));
            var lines=text.Split(new[]{"\r\n"},StringSplitOptions.None);
            for(int i=0;i<lines.Length;i++)
                if(lines[i].StartsWith("pmp_filepath=",StringComparison.OrdinalIgnoreCase))lines[i]="pmp_filepath="+stagedPmp;
            stagedFiles.Add(stagedPc5);
            File.WriteAllBytes(stagedPc5,encoding.GetBytes(string.Join("\r\n",lines)));
            StagedConfigurationPath=stagedPc5;
            return stagedPc5;
        }

        /// <summary>清理以前异常中断遗留在 CAD 打印机目录中的暂存配置（仅限本工具前缀且超过指定时长的文件）。</summary>
        public static int SweepStaged(string plottersDirectory,TimeSpan minimumAge)
        {
            int removed=0;
            if(string.IsNullOrWhiteSpace(plottersDirectory)||!Directory.Exists(plottersDirectory))return 0;
            var cutoff=DateTime.UtcNow-minimumAge;
            var candidates=Directory.EnumerateFiles(plottersDirectory,StagedPrefix+"*.pc5").ToList();
            string pmpDirectory=Path.Combine(plottersDirectory,"PMP Files");
            if(Directory.Exists(pmpDirectory))candidates.AddRange(Directory.EnumerateFiles(pmpDirectory,StagedPrefix+"*.pmp"));
            foreach(string path in candidates)
            {
                // 只删除本工具生成的 32 位随机标识文件，不碰用户自建配置。
                string stem=Path.GetFileNameWithoutExtension(path);
                if(stem.Length!=StagedPrefix.Length+32||!stem.Substring(StagedPrefix.Length).All(Uri.IsHexDigit))continue;
                try{if(File.GetLastWriteTimeUtc(path)<cutoff){File.Delete(path);removed++;}}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
            return removed;
        }

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            configurationLock?.Dispose();configurationLock=null;pmpLock?.Dispose();pmpLock=null;
            var failures=new List<Exception>();
            foreach(string path in stagedFiles.Concat(new[]{ConfigurationPath,PmpPath}))
                try{File.Delete(path);}catch(Exception ex){failures.Add(ex);}
            try{if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory,false);}
            catch(Exception ex){failures.Add(ex);}
            if(failures.Count>0)throw new IOException("临时 PDF 配置清理失败，请检查残留目录："+directory,new AggregateException(failures));
        }

        private static Encoding Gbk()
        {
#if NET8_0_OR_GREATER
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            return Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        }
        private static void VerifyLockedBytes(FileStream file,byte[] expected)
        {
            if(file.Length!=expected.Length)throw new IOException("取得读锁前临时 PDF 配置发生变化。");
            for(int index=0;index<expected.Length;index++)
                if(file.ReadByte()!=expected[index])throw new IOException("取得读锁前临时 PDF 配置发生变化。");
            file.Position=0;
        }
        private static string Dimension(double value)
        {
            if(double.IsNaN(value)||double.IsInfinity(value)||value<=0)throw new ArgumentOutOfRangeException(nameof(value),"纸张尺寸/面积必须为有限正数。");
            string text=value.ToString("0.000000",CultureInfo.InvariantCulture);
            if(text.Length>64||!double.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double parsed)||parsed<=0||Math.Abs(parsed-value)>0.000001)
                throw new ArgumentOutOfRangeException(nameof(value),"纸张尺寸不能以六位小数准确表示。");
            return text;
        }
        private static string PrivateRoot(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Any(ch=>ch<32||ch=='='||ch=='['||ch==']'))throw new ArgumentException("私有临时目录路径无效。");
            return Path.GetFullPath(path);
        }
        private static void Require(Ini ini,string section,string key,string expected)
        {if(!string.Equals(ini.Get(section,key),expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("源 PC5 驱动配置不受支持："+section+"/"+key);}
        private static void Empty(Ini ini,string section,string key)
        {if(!string.IsNullOrEmpty(ini.Get(section,key)))throw new InvalidDataException("PC5 含外部引用或私有数据："+section+"/"+key);}
        private static void ZeroOrEmpty(Ini ini,string section,string key)
        {var value=ini.Get(section,key);if(!string.IsNullOrEmpty(value)&&(value ?? "").Trim('0').Length>0)throw new InvalidDataException("PC5 含自动提交或初始化配置："+section+"/"+key);}
        private static void TryDelete(string path){try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        private static void TryRemoveEmpty(string path){try{if(Directory.Exists(path)&&!Directory.EnumerateFileSystemEntries(path).Any())Directory.Delete(path,false);}catch(IOException){}catch(UnauthorizedAccessException){}}

        private sealed class Ini
        {
            private readonly Dictionary<string,Dictionary<string,string>> sections=new Dictionary<string,Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> order=new List<string>();
            public Dictionary<string,string> Section(string name,bool create=false)
            {
                if(!sections.TryGetValue(name,out var found))
                {
                    if(!create)throw new InvalidDataException("PC5/PMP 缺少节："+name);
                    found=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);sections.Add(name,found);order.Add(name);
                }
                return found;
            }
            public string? Get(string section,string key)=>sections.TryGetValue(section,out var entries)&&entries.TryGetValue(key,out var value)?value:null;
            public static Ini Parse(string text)
            {
                if(text.Any(ch=>(ch<32&&ch!='\r'&&ch!='\n'&&ch!='\t')||ch==0x7f))throw new InvalidDataException("PC5 含非法控制字符。");
                var result=new Ini();Dictionary<string,string>? section=null;
                using(var reader=new StringReader(text))
                {
                    string? raw;while((raw=reader.ReadLine())!=null)
                    {
                        string line=raw.Trim();if(line.Length==0||line[0]==';'||line[0]=='#')continue;
                        if(line[0]=='[')
                        {
                            if(line.Length<3||line[line.Length-1]!=']'||line.Substring(1,line.Length-2).Any(ch=>ch=='['||ch==']'||ch=='='||char.IsControl(ch)))throw new InvalidDataException("PC5 节名称无效。");
                            string name=line.Substring(1,line.Length-2).Trim();
                            if(name.Length==0||result.sections.ContainsKey(name))throw new InvalidDataException("PC5 节名称为空或重复。");
                            section=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);result.sections.Add(name,section);result.order.Add(name);continue;
                        }
                        int split=line.IndexOf('=');
                        if(section==null||split<=0)throw new InvalidDataException("PC5 键值行无效。");
                        string key=line.Substring(0,split).Trim(),value=line.Substring(split+1).Trim();
                        if(key.Length==0||key.Any(ch=>ch=='['||ch==']'||char.IsControl(ch))||section.ContainsKey(key))throw new InvalidDataException("PC5 键名称无效或重复。");
                        section.Add(key,value);
                    }
                }
                return result;
            }
            public string Render()
            {
                var result=new StringBuilder();
                foreach(string name in order)
                {
                    result.Append('[').Append(name).Append("]\r\n");
                    foreach(var pair in sections[name])
                    {
                        if(pair.Value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new InvalidDataException("PC5 字段不能包含换行。");
                        result.Append(pair.Key).Append('=').Append(pair.Value).Append("\r\n");
                    }
                }
                return result.ToString();
            }
        }
    }
}

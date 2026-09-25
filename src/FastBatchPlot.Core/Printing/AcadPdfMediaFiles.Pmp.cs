using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Printing
{
    public sealed partial class AcadPdfMediaFiles
    {
        public static System.Collections.Generic.IReadOnlyList<string> DependencyFiles(string path)
        {
            var paths=new System.Collections.Generic.List<string>{Path.GetFullPath(path)};
            var source=PiaConfiguration.Read(path);
            ReadLinkedPmp(path,source.Root.Child("meta"),p=>paths.Add(p));
            return paths;
        }
        private static PiaConfiguration? ReadLinkedPmp(string sourcePc3,PiaNode sourceMeta,Action<string>? observePath=null)
        {
            string reference=OptionalText(sourceMeta,"user_defined_model_pathname");
            string basename=OptionalText(sourceMeta,"user_defined_model_basename");
            if(reference.Length==0)reference=basename;
            if(reference.Length==0)return null;
            if(reference.Any(char.IsControl) || !string.Equals(Path.GetExtension(reference),".pmp",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PC3 的 PMP 引用无效。");
            if(basename.Length>0 && !string.Equals(Path.GetFileName(reference),basename,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PC3 的 PMP 路径与文件名不一致。");
            string path;
            if(Path.IsPathRooted(reference))
            {
                if(reference.Length<3 || !char.IsLetter(reference[0]) || reference[1]!=':' || (reference[2]!='\\' && reference[2]!='/'))
                    throw new InvalidDataException("关联 PMP 必须为本地磁盘完整路径。");
                path=Path.GetFullPath(reference);
            }
            else
            {
                if(reference.IndexOfAny(new[]{'\\','/',':'})>=0)throw new InvalidDataException("关联 PMP 相对路径不明确。");
                string directory=Path.GetDirectoryName(Path.GetFullPath(sourcePc3))!;
                var candidates=new[]{Path.Combine(directory,reference),Path.Combine(directory,"PMP Files",reference)}
                    .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(candidates.Length!=1)throw new InvalidDataException(candidates.Length==0?"找不到 PC3 关联的 PMP，未丢弃原设置。":"同名 PMP 有多份，不能确定原配置。");
                path=candidates[0];
            }
            if(!File.Exists(path))throw new InvalidDataException("PC3 关联的 PMP 不存在："+path);
            observePath?.Invoke(path);
            var pmp=PiaConfiguration.Read(path);var meta=pmp.Root.Child("meta");
            foreach(string key in new[]{"canonical_family_name","canonical_model_name","driver_type","file_only"})
                if(meta.Raw(key)!=sourceMeta.Raw(key))throw new InvalidDataException("PMP 与所选 PDF 驱动不匹配："+key);
            if(!string.Equals(Path.GetFileName(meta.Text("driver_pathname")),Path.GetFileName(sourceMeta.Text("driver_pathname")),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PMP 与所选 PDF 驱动文件不匹配。");
            foreach(string key in new[]{"short_net_name","friendly_net_name"})
                if(OptionalText(meta,key).Length>0)throw new InvalidDataException("PMP 包含网络设备引用。");
            if(meta.Values.TryGetValue("config_autospool",out var autospool) && autospool!="FALSE")
                throw new InvalidDataException("PMP 含自动提交设置，不能创建独立 PDF 配置。");
            // PMP 的 meta 自引用是设备元数据，原生安装文件也可能保留构建机旧路径；生成时改为本次副本。
            var udm=pmp.Root.Child("udm");var calibration=udm.Child("calibration");
            foreach(string key in new[]{"_x","_y"})
            {
                string value=calibration.Raw(key).Split(' ')[0];
                if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double factor) || double.IsNaN(factor) || double.IsInfinity(factor) || factor<=0)
                    throw new InvalidDataException("PMP 的校准系数无效。");
            }
            if(udm.Children.TryGetValue("media",out var media))
            {
                var sizes=GetOrAdd(media,"size");var descriptions=GetOrAdd(media,"description");
                NextPaperIndex(sizes,descriptions);
                var names=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in descriptions.Children.Values)
                    if(!names.Add(item.Text("name")))throw new InvalidDataException("PMP 纸张描述名称重复。");
                var papers=new HashSet<string>(StringComparer.Ordinal);
                foreach(var item in sizes.Children.Values)
                {
                    if(!papers.Add(item.Text("name")))throw new InvalidDataException("PMP 纸张名称重复。");
                    if(!names.Contains(item.Text("media_description_name")))throw new InvalidDataException("PMP 纸张缺少对应描述。");
                }
            }
            return pmp;
        }

        private static string OptionalText(PiaNode node,string key)=>node.Values.ContainsKey(key)?node.Text(key):"";
        private static PiaNode GetOrAdd(PiaNode node,string key)=>node.Children.TryGetValue(key,out var child)?child:node.Add(key);
        private static string NextPaperIndex(PiaNode sizes,PiaNode descriptions)
        {
            if(sizes.Values.Count>0 || descriptions.Values.Count>0)throw new InvalidDataException("PMP 纸张集合包含非条目字段。");
            int next=0;
            foreach(string key in sizes.Children.Keys.Concat(descriptions.Children.Keys))
            {
                if(!int.TryParse(key,NumberStyles.None,CultureInfo.InvariantCulture,out int index) || index<0 || index>=10000 || key!=index.ToString(CultureInfo.InvariantCulture))
                    throw new InvalidDataException("PMP 纸张索引无效或过多。");
                next=Math.Max(next,index+1);
            }
            if(next>=10000)throw new InvalidDataException("PMP 纸张条目过多。");
            return next.ToString(CultureInfo.InvariantCulture);
        }

        private static void ApplyLinkedOptions(PiaConfiguration pmp,PiaConfiguration source,PdfOutputOptions? options)
        {
            if(options==null || !options.HasOverrides)return;
            var customNames=new List<string>();
            if(options.VectorResolutionDpi.HasValue)customNames.Add("Hardcopy_Resolution");
            if(options.RasterResolutionDpi.HasValue)customNames.AddRange(new[]{"Raster_Limit","Monochrome_Raster_Limit","Custom_Raster_Resolution","Custom_Monochrome_Resolution"});
            if(options.TextToGeometry.HasValue)customNames.Add("All_As_Geometry");
            if(options.IncludeLayers.HasValue)customNames.Add("Include_Layer");
            // 只同步现存的参数覆盖；不改变其他能力位或删除/隐藏规则。
            foreach(string section in new[]{"mod","udm","del"})
            {
                if(!pmp.Root.Children.TryGetValue(section,out var root))continue;
                bool deletion=section=="del";
                if(root.Children.TryGetValue("custom",out var custom))
                    foreach(var entry in custom.Children.Values)
                        if(entry.Values.ContainsKey("name") && customNames.Contains(entry.Text("name")))
                        {
                            if(deletion)throw new InvalidDataException("PMP 删除了本次要求覆盖的 PDF 参数："+entry.Text("name"));
                            entry.SetRaw("value",Custom(source,entry.Text("name")).Raw("value"));
                        }
                if(!root.Children.TryGetValue("res_color_mem",out var color))continue;
                if(options.MergeLines.HasValue && color.Values.ContainsKey("lines_overwrite"))
                {
                    if(deletion)throw new InvalidDataException("PMP 删除了直线合并参数，不能覆盖。");
                    color.SetRaw("lines_overwrite",source.Root.Child("res_color_mem").Raw("lines_overwrite"));
                }
                if(options.VectorResolutionDpi.HasValue && color.Children.TryGetValue("resolution",out var resolution))
                    foreach(string key in new[]{"phys_resolution_x","phys_resolution_y","effective_resolution_x","effective_resolution_y"})
                        if(resolution.Values.ContainsKey(key))
                        {
                            if(deletion)throw new InvalidDataException("PMP 删除了矢量分辨率参数，不能覆盖。");
                            resolution.SetRaw(key,source.Root.Child("res_color_mem").Child("resolution").Raw(key));
                        }
            }
        }
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FastBatchPlot.Core.Printing
{
    public sealed partial class ZwPdfMediaFiles
    {
        public static System.Collections.Generic.IReadOnlyList<string> DependencyFiles(string path)
        {
            var paths=new System.Collections.Generic.List<string>{Path.GetFullPath(path)};
            byte[] bytes;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length==0||file.Length>MaximumSourceBytes)throw new InvalidDataException("源 PC5 为空或超过256KB。");
                using(var buffer=new MemoryStream()){file.CopyTo(buffer);bytes=buffer.ToArray();}
            }
            var source=Ini.Parse(Gbk().GetString(bytes));
            ReadLinkedPmp(path,source.Get("Meta","pmp_filepath"),p=>paths.Add(p));
            return paths;
        }
        private static Ini? ReadLinkedPmp(string sourcePc5,string? reference,Action<string>? observePath=null)
        {
            if(reference==null || reference.Length==0)return null;
            if(reference.Any(char.IsControl) || !string.Equals(Path.GetExtension(reference),".pmp",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("PC5 的 PMP 引用无效。");
            string sourceDirectory=Path.GetDirectoryName(Path.GetFullPath(sourcePc5))!;
            string path;
            if(Path.IsPathRooted(reference))
            {
                // 不访问网络共享或驱动器相对路径，也不猜测用户自定义的搜索目录。
                if(reference.Length<3 || !char.IsLetter(reference[0]) || reference[1]!=':' || (reference[2]!='\\' && reference[2]!='/'))
                    throw new InvalidDataException("关联 PMP 必须为本地磁盘的完整路径。");
                path=Path.GetFullPath(reference);
            }
            else
            {
                if(reference.IndexOfAny(new[]{'\\','/',':'})>=0)
                    throw new InvalidDataException("关联 PMP 的相对路径不明确，请使用完整路径或文件名。");
                var candidates=new[]{Path.Combine(sourceDirectory,reference),Path.Combine(sourceDirectory,"PMP Files",reference)}
                    .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(candidates.Length!=1)
                    throw new InvalidDataException(candidates.Length==0?"找不到 PC5 关联的 PMP 文件；未丢弃原设置。":"同名 PMP 文件有多份，无法确定原配置。");
                path=candidates[0];
            }
            if(!File.Exists(path))throw new InvalidDataException("关联 PMP 文件不存在："+path);
            observePath?.Invoke(path);
            byte[] bytes;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length==0 || file.Length>MaximumSourceBytes)throw new InvalidDataException("关联 PMP 为空或超过 256KB。");
                using(var memory=new MemoryStream()){file.CopyTo(memory);bytes=memory.ToArray();}
            }
            if((bytes.Length>=3 && bytes[0]==0xef && bytes[1]==0xbb && bytes[2]==0xbf) ||
                (bytes.Length>=2 && ((bytes[0]==0xff && bytes[1]==0xfe)||(bytes[0]==0xfe && bytes[1]==0xff))))
                throw new InvalidDataException("关联 PMP 必须为原生无 BOM 的 GBK 文本。");
            Ini pmp;
            try{pmp=Ini.Parse(Gbk().GetString(bytes));}
            catch(DecoderFallbackException ex){throw new InvalidDataException("关联 PMP 不是有效 GBK 文本。",ex);}
            // 不复制仍然指向外部文件的链式覆盖；已确认的本地内容全部保留。
            if(!string.IsNullOrEmpty(pmp.Get("Meta","pmpfilepath")) || !string.IsNullOrEmpty(pmp.Get("Meta","driver_path")))
                throw new InvalidDataException("关联 PMP 含外部驱动或链式文件引用，不能建立独立副本。");
            foreach(string axis in new[]{"x","y"})
            {
                if(!double.TryParse(pmp.Get("Meta","calibration_"+axis),NumberStyles.Float,CultureInfo.InvariantCulture,out double factor) ||
                    double.IsNaN(factor) || double.IsInfinity(factor) || factor<=0)
                    throw new InvalidDataException("关联 PMP 的校准系数无效。");
            }
            foreach(string key in new[]{"mod_num","del_num","hide_num"})ReadPmpCount(pmp,key);
            int count=ReadPmpCount(pmp,"userdef_num");
            var user=pmp.Section("user",count==0);
            for(int index=0;index<count;index++)
                if(!user.TryGetValue("paper_name"+index.ToString(CultureInfo.InvariantCulture),out var name) || string.IsNullOrWhiteSpace(name))
                    throw new InvalidDataException("关联 PMP 的自定义纸张数量与条目不一致。");
            return pmp;
        }

        private static int ReadPmpCount(Ini pmp,string key)
        {
            if(!int.TryParse(pmp.Get("Meta",key),NumberStyles.None,CultureInfo.InvariantCulture,out int count) || count<0 || count>10000)
                throw new InvalidDataException("PMP 的条目数量无效："+key);
            return count;
        }

        private static void AppendPaper(Ini pmp,string name,string width,string height,string area)
        {
            int count=ReadPmpCount(pmp,"userdef_num");
            if(count>=10000)throw new InvalidDataException("PMP 自定义纸张条目过多。");
            string suffix=count.ToString(CultureInfo.InvariantCulture);
            var user=pmp.Section("user",count==0);
            string[] keys={"paper_name","paper_local_name","size_x","size_y","llx","lly","urx","ury","actual_x","actual_y","area","Unit"};
            string[] values={name,name,width,height,"0.000000","0.000000",width,height,width,height,area,"1"};
            if(keys.Any(key=>user.ContainsKey(key+suffix)))throw new InvalidDataException("PMP 新纸张索引与原有字段冲突，未覆盖原纸张。");
            for(int index=0;index<keys.Length;index++)user.Add(keys[index]+suffix,values[index]);
            pmp.Section("Meta")["userdef_num"]=(count+1).ToString(CultureInfo.InvariantCulture);
        }
    }
}

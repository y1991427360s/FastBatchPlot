using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FastBatchPlot.Core.Tasks
{
    public static class PlotResourceRevision
    {
        public static string ResolveStyle(string name,string searchPaths)
        {
            string extension=Path.GetExtension(name);
            if(!string.Equals(extension,".ctb",StringComparison.OrdinalIgnoreCase)&&!string.Equals(extension,".stb",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("打印样式必须为 CTB/STB 文件或 None。");
            if(Path.IsPathRooted(name))return Path.GetFullPath(name);
            if(name!=Path.GetFileName(name))throw new InvalidOperationException("打印样式的相对路径不明确。");
            var matches=searchPaths.Split(';').Select(p=>p.Trim().Trim('"')).Where(p=>p.Length>0)
                .Select(Environment.ExpandEnvironmentVariables).Where(Path.IsPathRooted)
                .Select(p=>Path.GetFullPath(Path.Combine(p,name))).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("当前 CAD 样式搜索目录无法唯一定位 "+name+"，请检查缺失或同名文件。");
            return matches[0];
        }
        public static string Compute(IEnumerable<string> paths)
        {
            var files=paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
            if(files.Length==0)throw new InvalidOperationException("打印资源列表为空，无法建立核验标记。");
            using(var bytes=new MemoryStream())
            using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true))
            using(var hash=SHA256.Create())
            {
                foreach(string path in files)
                {
                    string digest=ExternalFileRevision.Read(path);
                    if(!digest.StartsWith("sha256:",StringComparison.Ordinal))throw new FileNotFoundException("打印资源不存在，不能复用旧任务。",path);
                    writer.Write(path.ToUpperInvariant());writer.Write(digest);
                }
                writer.Flush();return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
            }
        }
    }
}

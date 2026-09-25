using System;
using System.IO;
using System.Security.Cryptography;

namespace FastBatchPlot.Core.Tasks
{
    /// <summary>文件身份使用完整内容摘要；相同长度和时间戳不能掩盖外部文件替换。</summary>
    public static class ExternalFileRevision
    {

        public static string ResolvePath(string ownerFile, string reference)
        {
            if(string.IsNullOrWhiteSpace(reference))return "";
            if(Path.IsPathRooted(reference))return Path.GetFullPath(reference);
            if(string.IsNullOrWhiteSpace(ownerFile)||!Path.IsPathRooted(ownerFile))
                throw new InvalidOperationException("相对外参路径缺少已保存的来源目录，无法核对任务依赖。");
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ownerFile)!,reference));
        }

        public static string Read(string path)
        {
            if(string.IsNullOrWhiteSpace(path))return "unresolved";
            try
            {
                // 同事正在编辑外参时文件以写方式打开；允许共享读写，否则整批会因共享冲突失败。
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(var hash=SHA256.Create())
                    return "sha256:"+Convert.ToBase64String(hash.ComputeHash(input));
            }
            catch(FileNotFoundException){return "missing";}
            catch(DirectoryNotFoundException){return "missing";}
            // 访问被拒绝/正在写入等情况必须报错，不能退化成可复用的“缺失”标记。
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Xml;

namespace FastBatchPlot.Core.Assets
{
    public static class StampLibraryStore
    {
        private const int Limit=32*1024*1024;
        private static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes)); }
        public static StampLibrary Load(string path)
        {
            if(string.IsNullOrWhiteSpace(path)) throw new ArgumentException("印章库路径不能为空。");
            byte[] bytes;
            try
            {
                using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(file.Length>Limit) throw new InvalidDataException("印章库超过32MB。");
                    using(var buffer=new MemoryStream()) { file.CopyTo(buffer); bytes=buffer.ToArray(); }
                }
            }
            catch(FileNotFoundException) { return new StampLibrary(); }
            catch(DirectoryNotFoundException) { return new StampLibrary(); }
            try
            {
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,XmlDictionaryReaderQuotas.Max))
                {
                    var library=new DataContractJsonSerializer(typeof(StampLibrary)).ReadObject(reader) as StampLibrary;
                    while(reader.Read()) if(reader.NodeType!=XmlNodeType.Whitespace && reader.NodeType!=XmlNodeType.SignificantWhitespace)
                        throw new InvalidDataException("印章库包含多余内容。");
                    if(library==null) throw new InvalidDataException("印章库不能为空对象。");
                    library.Validate(); library.Revision=Hash(bytes); return library;
                }
            }
            catch(Exception ex) when (!(ex is IOException)) { throw new InvalidDataException("印章库无效，原文件已保留："+ex.Message,ex); }
        }

        public static void Save(string path, StampLibrary library)
        {
            if(library==null || library.SchemaVersion<1 || library.SchemaVersion>3 || library.Assets==null || library.Assets.Any(a=>a==null))throw new InvalidDataException("印章库结构无效。");
            var copy=library.Copy();
            if(copy.Assets.Any(a=>a.Details!=null))copy.SchemaVersion=3;
            else if(copy.Assets.Any(a=>a.Protection!=null) && copy.SchemaVersion<2)copy.SchemaVersion=2;
            copy.Validate();
            string full=Path.GetFullPath(path), dir=Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            using(var guard=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                var current=Load(full);
                if(current.Revision!=copy.Revision) throw new IOException("印章库已被其他窗口或电脑更新，请重新打开后编辑；未覆盖共享库。");
                byte[] bytes;
                using(var buffer=new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(StampLibrary)).WriteObject(buffer,copy);
                    if(buffer.Length>Limit) throw new InvalidDataException("印章库超过32MB，未保存。");
                    bytes=buffer.ToArray();
                }
                string temp=Path.Combine(dir,".stamps-"+Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {file.Write(bytes,0,bytes.Length);file.Flush(true);}
                    if(File.Exists(full)) File.Replace(temp,full,null); else File.Move(temp,full);
                    library.Revision=Hash(bytes);library.SchemaVersion=copy.SchemaVersion;
                }
                finally {if(File.Exists(temp)) File.Delete(temp);}
            }
        }
    }
}

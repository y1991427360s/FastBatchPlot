using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace FastBatchPlot.Core.Templates
{
    public static class TitleTemplateStore
    {
        private const int Limit = 4 * 1024 * 1024;
        private static readonly object Gate = new object();
        public static TitleTemplateLibrary Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("模板库路径不能为空。");
            lock (Gate)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream.Length > Limit) throw new InvalidDataException("模板库超过 4MB。");
                        using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
                        {
                            var library = (TitleTemplateLibrary)new DataContractJsonSerializer(typeof(TitleTemplateLibrary)).ReadObject(reader)!;
                            while (reader.Read())
                                if (reader.NodeType != XmlNodeType.Whitespace && reader.NodeType != XmlNodeType.SignificantWhitespace)
                                    throw new InvalidDataException("模板库存在多余内容。");
                            TitleTemplateService.Validate(library);
                            return library;
                        }
                    }
                }
                catch (FileNotFoundException) { return new TitleTemplateLibrary(); }
                catch (DirectoryNotFoundException) { return new TitleTemplateLibrary(); }
                catch (SerializationException ex) { throw new InvalidDataException("模板库格式无效，保留原文件。", ex); }
                catch (XmlException ex) { throw new InvalidDataException("模板库格式无效，保留原文件。", ex); }
            }
        }
        public static void Save(string path, TitleTemplateLibrary library)
        {
            var copy = TitleTemplateService.CopyLibrary(library);
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
            lock (Gate)
            {
                Load(full);
                Directory.CreateDirectory(directory);
                // 同目录排他写锁，多个 CAD 同时写时明确失败，不并发替换。
                using (var writeLock = new FileStream(full + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Load(full);
                    string temp = Path.Combine(directory, ".templates-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            new DataContractJsonSerializer(typeof(TitleTemplateLibrary)).WriteObject(stream, copy);
                            if (stream.Length > Limit) throw new InvalidDataException("模板库超过 4MB，未保存。");
                            stream.Flush(true);
                        }
                        if (File.Exists(full)) File.Replace(temp, full, null);
                        else File.Move(temp, full);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
            }
        }
    }
}

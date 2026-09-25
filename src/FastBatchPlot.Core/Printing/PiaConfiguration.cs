using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FastBatchPlot.Core.Printing
{
    /// <summary>PC3/PMP 的 PIA 容器和行语法；只处理文件，不调用 CAD。</summary>
    public sealed class PiaConfiguration
    {
        private const string Header="PIAFILEVERSION_2.0,PC3VER1,compress\r\npmzlibcodec";
        private const string PlainHeader="PIAFILEVERSION_2.0,PC3VER1";
        private const int MaxBytes=4*1024*1024;
        public PiaNode Root {get;}=new PiaNode();

        public static PiaConfiguration Read(string path)
        {
            byte[] bytes;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length<=0||file.Length>MaxBytes)throw new InvalidDataException("PC3/PMP 配置为空或超过 4 MiB。");
                using(var buffer=new MemoryStream()){file.CopyTo(buffer);bytes=buffer.ToArray();}
            }
            return Decode(bytes);
        }

        public static PiaConfiguration Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length==0||bytes.Length>MaxBytes)throw new InvalidDataException("PC3/PMP 配置大小无效。");
            byte[] header=Encoding.ASCII.GetBytes(Header);
            byte[] decoded;
            if(bytes.Length>=header.Length && bytes.Take(header.Length).SequenceEqual(header))
            {
                if(bytes.Length<66)throw new InvalidDataException("PC3 压缩头不完整。");
                uint checksum=ReadUInt(bytes,48),length=ReadUInt(bytes,52),compressedLength=ReadUInt(bytes,56);
                if(length==0||length>MaxBytes||compressedLength!=bytes.Length-60||checksum!=Adler(bytes,60,bytes.Length-60))
                    throw new InvalidDataException("PC3 压缩长度或校验和不匹配。");
                if((bytes[60]&15)!=8||(bytes[60]>>4)>7||((bytes[60]<<8)+bytes[61])%31!=0||(bytes[61]&32)!=0)
                    throw new InvalidDataException("PC3 zlib 头不受支持。");
                using(var input=new MemoryStream(bytes,62,bytes.Length-66,false))
                using(var stream=new DeflateStream(input,CompressionMode.Decompress))
                using(var output=new MemoryStream())
                {
                    var buffer=new byte[8192];int count;
                    while((count=stream.Read(buffer,0,buffer.Length))>0)
                    {
                        if(output.Length+count>length)throw new InvalidDataException("PC3 解压长度超过声明长度。");
                        output.Write(buffer,0,count);
                    }
                    decoded=output.ToArray();
                }
                uint contentAdler=((uint)bytes[bytes.Length-4]<<24)|((uint)bytes[bytes.Length-3]<<16)|((uint)bytes[bytes.Length-2]<<8)|bytes[bytes.Length-1];
                if(decoded.Length!=length||Adler(decoded,0,decoded.Length)!=contentAdler)throw new InvalidDataException("PC3 解压内容校验失败。");
            }
            else
            {
                string text=Gbk().GetString(bytes);
                if(!text.StartsWith(PlainHeader+"\r\n",StringComparison.Ordinal)&&!text.StartsWith(PlainHeader+"\n",StringComparison.Ordinal))
                    throw new InvalidDataException("不支持此 PC3/PMP 容器版本。");
                return Parse(text.Substring(text.IndexOf('\n')+1));
            }
            return Parse(Gbk().GetString(decoded));
        }

        public byte[] Encode()
        {
            byte[] plain=Gbk().GetBytes(Root.Render()+"\0");
            if(plain.Length>MaxBytes)throw new InvalidDataException("PC3/PMP 配置过大。");
            byte[] compressed;
            using(var buffer=new MemoryStream())
            {
                buffer.WriteByte(0x78);buffer.WriteByte(0x9c);
                using(var deflate=new DeflateStream(buffer,CompressionLevel.Optimal,true))deflate.Write(plain,0,plain.Length);
                uint adler=Adler(plain,0,plain.Length);
                for(int shift=24;shift>=0;shift-=8)buffer.WriteByte((byte)(adler>>shift));
                compressed=buffer.ToArray();
            }
            using(var buffer=new MemoryStream())
            using(var writer=new BinaryWriter(buffer))
            {
                writer.Write(Encoding.ASCII.GetBytes(Header));writer.Write(Adler(compressed,0,compressed.Length));
                writer.Write((uint)plain.Length);writer.Write((uint)compressed.Length);writer.Write(compressed);
                return buffer.ToArray();
            }
        }

        public static PiaConfiguration Parse(string text)
        {
            if(text.EndsWith("\0",StringComparison.Ordinal))text=text.Substring(0,text.Length-1);
            if(text.Length>MaxBytes||text.Any(c=>char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t'))throw new InvalidDataException("PIA 文本含无效控制字符或超过长度限制。");
            var document=new PiaConfiguration();var stack=new Stack<PiaNode>();stack.Push(document.Root);
            int count=0;
            using(var reader=new StringReader(text))
            {
                string? raw;
                while((raw=reader.ReadLine())!=null)
                {
                    string line=raw.Trim();if(line.Length==0)continue;
                    if(++count>100000||stack.Count>64)throw new InvalidDataException("PIA 结构过大或嵌套过深。");
                    // 字符串以等号后的引号开始，到该行末尾结束，没有闭合引号。
                    int equals=line.IndexOf('=');
                    if(equals>0){stack.Peek().SetRaw(line.Substring(0,equals),line.Substring(equals+1),true);continue;}
                    if(line=="}"){if(stack.Count<=1)throw new InvalidDataException("PIA 结束括号多余。");stack.Pop();continue;}
                    if(line.EndsWith("{",StringComparison.Ordinal))
                    {stack.Push(stack.Peek().Add(line.Substring(0,line.Length-1).Trim()));continue;}
                    throw new InvalidDataException("PIA 配置行无效。");
                }
            }
            if(stack.Count!=1)throw new InvalidDataException("PIA 配置未闭合。");
            return document;
        }
        internal static Encoding Gbk()
        {
#if NET8_0_OR_GREATER
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            return Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        }
        private static uint ReadUInt(byte[] b,int offset)=>b[offset]|((uint)b[offset+1]<<8)|((uint)b[offset+2]<<16)|((uint)b[offset+3]<<24);
        private static uint Adler(byte[] b,int offset,int count)
        {uint a=1,c=0;for(int i=offset;i<offset+count;i++){a=(a+b[i])%65521;c=(c+a)%65521;}return (c<<16)|a;}
    }

    public sealed class PiaNode
    {
        private readonly Dictionary<string,string> values=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Dictionary<string,PiaNode> children=new Dictionary<string,PiaNode>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string,string> Values=>values;
        public IReadOnlyDictionary<string,PiaNode> Children=>children;
        public PiaNode Child(string name)=>children.TryGetValue(name,out var child)?child:throw new InvalidDataException("PIA 缺少节点："+name);
        public string Raw(string key)=>values.TryGetValue(key,out var value)?value:throw new InvalidDataException("PIA 缺少字段："+key);
        public string Text(string key){string value=Raw(key);if(!value.StartsWith("\"",StringComparison.Ordinal))throw new InvalidDataException("PIA 字符串类型错误："+key);return value.Substring(1);}
        public PiaNode Add(string name)
        {Name(name);if(children.ContainsKey(name)||values.ContainsKey(name))throw new InvalidDataException("PIA 节点重复："+name);var node=new PiaNode();children.Add(name,node);return node;}
        public void SetRaw(string key,string value,bool rejectDuplicate=false)
        {
            Name(key);
            if(value.IndexOfAny(new[]{'\r','\n','\0'})>=0||children.ContainsKey(key)||(rejectDuplicate&&values.ContainsKey(key)))throw new InvalidDataException("PIA 字段无效或重复："+key);
            if(!value.StartsWith("\"",StringComparison.Ordinal)&&value.Length==0)throw new InvalidDataException("PIA 非字符串字段不能为空。");
            values[key]=value;
        }
        public void SetText(string key,string value)=>SetRaw(key,"\""+value);
        public PiaNode Copy(){var copy=new PiaNode();foreach(var item in values)copy.values.Add(item.Key,item.Value);foreach(var child in children)copy.children.Add(child.Key,child.Value.Copy());return copy;}
        public void SetChild(string key,PiaNode node){Name(key);if(values.ContainsKey(key))throw new InvalidDataException("PIA 字段和节点冲突。");children[key]=node.Copy();}
        internal string Render(int level=0)
        {
            if(level>64)throw new InvalidDataException("PIA 配置嵌套过深。");
            var text=new StringBuilder();string indent=new string(' ',level);
            foreach(var item in values)text.Append(indent).Append(item.Key).Append('=').Append(item.Value).Append("\r\n");
            foreach(var child in children)text.Append(indent).Append(child.Key).Append("{\r\n").Append(child.Value.Render(level+1)).Append(indent).Append("}\r\n");
            return text.ToString();
        }
        private static void Name(string value)
        {if(string.IsNullOrWhiteSpace(value)||value.Any(c=>char.IsWhiteSpace(c)||char.IsControl(c)||c=='='||c=='{'||c=='}'||c=='"'))throw new InvalidDataException("PIA 字段名称无效。");}
    }
}

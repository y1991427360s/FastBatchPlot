using System;
using System.IO;

namespace FastBatchPlot.Core.Assets
{
    internal static class StampPng
    {
        private static readonly uint[] CrcTable=BuildTable();
        private static uint[] BuildTable()
        {
            var table=new uint[256];
            for(uint i=0;i<256;i++){uint c=i;for(int j=0;j<8;j++)c=(c&1)!=0?0xedb88320^(c>>1):c>>1;table[i]=c;}
            return table;
        }
        private static uint Read(byte[] b,int i)=>(uint)b[i]<<24|(uint)b[i+1]<<16|(uint)b[i+2]<<8|b[i+3];
        public static void Validate(byte[] png)
        {
            byte[] signature={137,80,78,71,13,10,26,10};
            if(png.Length<33)throw new InvalidDataException("PNG 被截断。");
            for(int i=0;i<8;i++)if(png[i]!=signature[i])throw new InvalidDataException("印章文件不是 PNG。");
            int offset=8,chunks=0;bool data=false;
            while(offset<png.Length)
            {
                if(++chunks>4096 || png.Length-offset<12)throw new InvalidDataException("PNG 块数量异常或被截断。");
                uint length=Read(png,offset);
                if(length>(uint)(png.Length-offset-12))throw new InvalidDataException("PNG 内容被截断。");
                uint type=Read(png,offset+4);
                if(chunks==1 && (type!=0x49484452 || length!=13))throw new InvalidDataException("PNG 缺少有效图像头。");
                if(chunks!=1 && type==0x49484452)throw new InvalidDataException("PNG 图像头重复。");
                if(type==0x6163544c || type==0x6663544c || type==0x66644154)throw new InvalidDataException("印章不能使用动画 PNG。");
                uint crc=0xffffffff;
                for(int i=offset+4;i<offset+8+(int)length;i++)crc=CrcTable[(crc^png[i])&255]^(crc>>8);
                if((crc^0xffffffff)!=Read(png,offset+8+(int)length))throw new InvalidDataException("PNG 校验失败，内容可能已损坏。");
                offset+=12+(int)length;
                if(type==0x49444154)data=true;
                if(type==0x49454e44)
                {
                    if(length!=0||!data||offset!=png.Length)throw new InvalidDataException("PNG 结束块无效或包含多余内容。");
                    return;
                }
            }
            throw new InvalidDataException("PNG 缺少结束块。");
        }
    }
}

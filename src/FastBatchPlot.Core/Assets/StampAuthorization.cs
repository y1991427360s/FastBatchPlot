using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace FastBatchPlot.Core.Assets
{
    [DataContract]
    public sealed class StampProtection
    {
        [DataMember(IsRequired=true)] public int Version {get;set;} = 1;
        [DataMember(IsRequired=true)] public int Iterations {get;set;} = 210000;
        [DataMember(IsRequired=true)] public long ExpiresUtcTicks {get;set;}
        [DataMember(IsRequired=true)] public string Salt {get;set;} = "";
        [DataMember(IsRequired=true)] public string IV {get;set;} = "";
        [DataMember(IsRequired=true)] public string Ciphertext {get;set;} = "";
        [DataMember(IsRequired=true)] public string Tag {get;set;} = "";
        public StampProtection Copy() => (StampProtection)MemberwiseClone();
        internal void Validate()
        {
            if((Version!=1 && Version!=2) || Iterations!=210000 || ExpiresUtcTicks<=DateTime.MinValue.Ticks || ExpiresUtcTicks>DateTime.MaxValue.Ticks)
                throw new InvalidDataException("印章授权结构、期限或加密版本无效。");
            Check(Salt,32,32);Check(IV,16,16);Check(Tag,32,32);
            var cipher=Check(Ciphertext,16,StampAsset.MaximumPngBytes+16);
            if(cipher.Length%16!=0)throw new InvalidDataException("加密印章内容长度无效。");
        }
        private static byte[] Check(string text,int min,int max)
        {
            if(text==null||text.Length>(max+2)/3*4)throw new InvalidDataException("加密印章字段超出限制。");
            try
            {
                byte[] bytes=Convert.FromBase64String(text);
                if(bytes.Length<min||bytes.Length>max || Convert.ToBase64String(bytes)!=text)
                    throw new InvalidDataException("加密印章字段无效。");
                return bytes;
            }
            catch(FormatException ex){throw new InvalidDataException("加密印章字段损坏。",ex);}
        }
    }

    /// <summary>只驻留内存的解锁凭据；不保留授权码或派生密钥，不进入用户设置/共享库。</summary>
    public sealed class StampUsePermit
    {
        private readonly string fingerprint;
        private readonly StampAsset plain;
        private readonly DateTimeOffset issuedUtc;
        private readonly DateTimeOffset expiresUtc;
        private readonly long started=Stopwatch.GetTimestamp();
        internal StampUsePermit(StampAsset encrypted,StampAsset image,DateTimeOffset now)
        {
            fingerprint=StampAuthorization.Fingerprint(encrypted);plain=image.Copy();issuedUtc=now.ToUniversalTime();
            long end=encrypted.Protection!.ExpiresUtcTicks;
            if(encrypted.Details?.ValidUntilUtcTicks>0)end=Math.Min(end,encrypted.Details.ValidUntilUtcTicks);
            expiresUtc=new DateTimeOffset(end,TimeSpan.Zero);
        }
        public StampAsset GetAsset(StampAsset encrypted)=>GetAsset(encrypted,()=>DateTimeOffset.UtcNow);
        public StampAsset GetAsset(StampAsset encrypted,DateTimeOffset now)=>GetAsset(encrypted,()=>now);
        public StampAsset GetAsset(StampAsset encrypted,Func<DateTimeOffset> utcNow)
        {
            VerifyAsset(encrypted);
            var now=utcNow();
            double elapsed=(Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency;
            if(now<issuedUtc-TimeSpan.FromSeconds(2))throw new InvalidOperationException("本机时间向后变化，请校准时间后重新授权。");
            if(now>=expiresUtc || elapsed>=(expiresUtc-issuedUtc).TotalSeconds)
                throw new InvalidOperationException("印章或授权已到期，未提交本页。");
            return plain.Copy();
        }
        public StampAsset GetAssetForManagement(StampAsset encrypted){VerifyAsset(encrypted);return plain.Copy();}
        private void VerifyAsset(StampAsset encrypted)
        {
            if(!string.Equals(fingerprint,StampAuthorization.Fingerprint(encrypted),StringComparison.Ordinal))
                throw new InvalidOperationException("印章内容或授权已改变，请重新授权。");
        }
    }

    public static class StampAuthorization
    {
        public static StampAsset Protect(StampAsset plain,string code,DateTimeOffset expiresUtc,DateTimeOffset now)
        {
            if(plain.Protection!=null)throw new InvalidOperationException("请先使用原授权码解锁，再修改授权。");
            plain.Validate();ValidateCode(code);
            if(expiresUtc<=now)throw new ArgumentException("授权截止时间必须晚于当前时间。");
            byte[] salt=Random(32),iv=Random(16),key=Derive(code,salt);
            try
            {
                byte[] bytes=Convert.FromBase64String(plain.PngBase64),cipher;
                try
                {
                    using(var aes=Aes.Create())
                    {
                        aes.Key=Part(key,0);aes.IV=iv;aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;
                        using(var transform=aes.CreateEncryptor())cipher=transform.TransformFinalBlock(bytes,0,bytes.Length);
                    }
                }
                finally {Array.Clear(bytes,0,bytes.Length);}
                var result=plain.Copy();result.PngBase64="";
                result.Protection=new StampProtection {Version=plain.Details==null?1:2,ExpiresUtcTicks=expiresUtc.UtcDateTime.Ticks,
                    Salt=Convert.ToBase64String(salt),IV=Convert.ToBase64String(iv),Ciphertext=Convert.ToBase64String(cipher)};
                using(var hmac=new HMACSHA256(Part(key,32)))result.Protection.Tag=Convert.ToBase64String(hmac.ComputeHash(AuthenticatedBytes(result)));
                result.Validate();return result;
            }
            finally {Array.Clear(key,0,key.Length);}
        }
        public static StampUsePermit Unlock(StampAsset encrypted,string code)=>Unlock(encrypted,code,()=>DateTimeOffset.UtcNow);
        public static StampUsePermit Unlock(StampAsset encrypted,string code,DateTimeOffset now)=>Unlock(encrypted,code,()=>now);
        public static StampUsePermit Unlock(StampAsset encrypted,string code,Func<DateTimeOffset> utcNow)
        {
            var permit=Decrypt(encrypted,code,utcNow);permit.GetAsset(encrypted,utcNow);return permit;
        }
        public static StampUsePermit UnlockForManagement(StampAsset encrypted,string code)
            => Decrypt(encrypted,code,()=>DateTimeOffset.UtcNow);
        private static StampUsePermit Decrypt(StampAsset encrypted,string code,Func<DateTimeOffset> utcNow)
        {
            encrypted.Validate();ValidateCode(code);
            var protection=encrypted.Protection??throw new InvalidOperationException("该印章没有设置授权码。");
            byte[] key=Derive(code,Convert.FromBase64String(protection.Salt));
            try
            {
                byte[] tag;
                using(var hmac=new HMACSHA256(Part(key,32)))tag=hmac.ComputeHash(AuthenticatedBytes(encrypted));
                var expected=Convert.FromBase64String(protection.Tag);int difference=tag.Length^expected.Length;
                for(int i=0;i<tag.Length;i++)difference|=tag[i]^expected[i];
                if(difference!=0)throw new InvalidOperationException("授权码错误或印章内容被修改，不能解锁。");
                byte[] cipher=Convert.FromBase64String(protection.Ciphertext),bytes;
                using(var aes=Aes.Create())
                {
                    aes.Key=Part(key,0);aes.IV=Convert.FromBase64String(protection.IV);aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;
                    using(var transform=aes.CreateDecryptor())bytes=transform.TransformFinalBlock(cipher,0,cipher.Length);
                }
                try
                {
                    var plain=encrypted.Copy();plain.Protection=null;plain.PngBase64=Convert.ToBase64String(bytes);plain.Validate();
                    return new StampUsePermit(encrypted,plain,utcNow());
                }
                finally {Array.Clear(bytes,0,bytes.Length);}
            }
            finally {Array.Clear(key,0,key.Length);}
        }
        public static string Fingerprint(StampAsset asset)
        {
            asset.Validate();
            using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(AuthenticatedBytes(asset,true)));
        }
        private static byte[] AuthenticatedBytes(StampAsset asset,bool includeTag=false)
        {
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true),true))
            {
                writer.Write(asset.Details==null?"FastBatchPlot.StampProtection.v1":"FastBatchPlot.StampProtection.v2");writer.Write(asset.Id);writer.Write(asset.Name);
                writer.Write(asset.PixelWidth);writer.Write(asset.PixelHeight);writer.Write(asset.PngBase64);
                if(asset.Details!=null)
                {
                    writer.Write((int)asset.Details.Kind);writer.Write((int)asset.Details.Sizing);
                    writer.Write(asset.Details.WidthMm);writer.Write(asset.Details.HeightMm);writer.Write(asset.Details.ValidUntilUtcTicks);
                }
                var p=asset.Protection;writer.Write(p!=null);
                if(p!=null)
                {
                    writer.Write(p.Version);writer.Write(p.Iterations);writer.Write(p.ExpiresUtcTicks);
                    writer.Write(p.Salt);writer.Write(p.IV);writer.Write(p.Ciphertext);if(includeTag)writer.Write(p.Tag);
                }
                writer.Flush();return stream.ToArray();
            }
        }
        private static void ValidateCode(string code)
        {
            if(string.IsNullOrWhiteSpace(code)||code.Length<8||code.Length>128)
                throw new ArgumentException("授权码须为8至128字符，不能全为空白。");
            try{new UTF8Encoding(false,true).GetByteCount(code);}
            catch(EncoderFallbackException){throw new ArgumentException("授权码包含无效的 Unicode 字符。");}
        }
        private static byte[] Random(int length){var result=new byte[length];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(result);return result;}
        private static byte[] Derive(string code,byte[] salt)
        {
#if NET8_0_OR_GREATER
            return Rfc2898DeriveBytes.Pbkdf2(code,salt,210000,HashAlgorithmName.SHA256,64);
#else
            // netstandard2.0 的引用面不含此构造器；实际宿主均为 net48，支持 SHA256 重载。
            // 只调用框架密码库，不回退到 SHA1，也不自行实现 PBKDF2。
            var constructor=typeof(Rfc2898DeriveBytes).GetConstructor(new[]{typeof(string),typeof(byte[]),typeof(int),typeof(HashAlgorithmName)});
            if(constructor==null)throw new PlatformNotSupportedException("印章授权需要 .NET Framework 4.8 或新版 .NET。");
            using(var kdf=(Rfc2898DeriveBytes)constructor.Invoke(new object[]{code,salt,210000,HashAlgorithmName.SHA256}))return kdf.GetBytes(64);
#endif
        }
        private static byte[] Part(byte[] key,int start){var result=new byte[32];Buffer.BlockCopy(key,start,result,0,32);return result;}
    }
}

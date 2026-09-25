using System;
using System.IO;
using System.Reflection;

namespace FastBatchPlot.Core.Common
{
    /// <summary>net48 插件不能依赖宿主读取 DLL.config；仅处理随包依赖的已验证版本差异。</summary>
    public static class LegacyDependencyResolution
    {
        private static readonly object Gate=new object();
        private static bool initialized;
        [ThreadStatic] private static int imageOperationDepth;
        internal static IDisposable BeginImageOperation()
        {
            EnsureInitialized();imageOperationDepth++;return new ImageScope();
        }
        private sealed class ImageScope:IDisposable
        {
            private bool disposed;
            public void Dispose(){if(!disposed){imageOperationDepth--;disposed=true;}}
        }
        private static readonly string Root=Path.GetDirectoryName(typeof(LegacyDependencyResolution).Assembly.Location)!;
        public static void EnsureInitialized()
        {
            if(typeof(object).Assembly.GetName().Name!="mscorlib")return;
            lock(Gate)
            {
                if(initialized)return;
                AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
                initialized=true;
            }
        }
        private static Assembly? Resolve(object? sender,ResolveEventArgs args)
        {
            var requester=args.RequestingAssembly;
            // Framework 的泛型 JIT 请求可能没有 RequestingAssembly；仅同步图片操作期间接管。
            if(requester==null){if(imageOperationDepth==0)return null;}
            else
            {
                if(requester.IsDynamic)return null;
                var source=requester.GetName().Name;
                if(source!="SixLabors.ImageSharp" && source!="SixLabors.Fonts" && source!="System.Memory" && source!="System.Threading.Tasks.Extensions")return null;
                if(!string.Equals(Path.GetDirectoryName(requester.Location),Root,StringComparison.OrdinalIgnoreCase))return null;
            }
            var requested=new AssemblyName(args.Name);
            if(requested.Name!="System.Runtime.CompilerServices.Unsafe" || !TokenMatches(requested))return null;
            if(requested.Version!=new Version(4,0,4,1))return null;
            string path=Path.Combine(Root,"System.Runtime.CompilerServices.Unsafe.dll");
            if(!File.Exists(path))return null;
            var bundled=AssemblyName.GetAssemblyName(path);
            if(bundled.Name!=requested.Name || bundled.Version!=new Version(4,0,6,0) || !TokenMatches(bundled))return null;
            return Assembly.LoadFrom(path);
        }
        private static bool TokenMatches(AssemblyName name)
            => BitConverter.ToString(name.GetPublicKeyToken()??new byte[0])=="B0-3F-5F-7F-11-D5-0A-3A";
    }
}

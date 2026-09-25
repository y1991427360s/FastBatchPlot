using System;
using System.Runtime.CompilerServices;

namespace FastBatchPlot.CadBridge
{
    /// <summary>按宿主文档实例分配会话身份；重开同名文件不会复用身份。另存可能保留实例，须另核对修订标记。</summary>
    public static class DocumentSessionIdentity
    {
        private sealed class Identity { public readonly string Value = Guid.NewGuid().ToString("N"); }
        private static readonly ConditionalWeakTable<object, Identity> Identities = new ConditionalWeakTable<object, Identity>();
        public static string Get(object document) => Identities.GetValue(document, _ => new Identity()).Value;
    }
}

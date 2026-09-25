using FastBatchPlot.CadBridge;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DocumentSessionIdentityTests
    {
        [Fact]
        public void IdentityStaysStableOnlyForTheSameDocumentInstance()
        {
            var first = new object();
            var second = new object();
            Assert.Equal(DocumentSessionIdentity.Get(first), DocumentSessionIdentity.Get(first));
            Assert.NotEqual(DocumentSessionIdentity.Get(first), DocumentSessionIdentity.Get(second));
        }
    }
}

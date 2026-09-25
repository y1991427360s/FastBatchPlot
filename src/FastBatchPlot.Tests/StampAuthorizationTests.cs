using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FastBatchPlot.Tests
{
    // 共用一次正常授权，避免每个不涉及口令派生的用例重复执行 PBKDF。
    public sealed class StampAuthorizationFixture
    {
        public const string Code = "test-only-code-8426";
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset Expires => Now.AddHours(1);
        public StampAsset Plain { get; }
        public StampAsset Protected { get; }
        public StampUsePermit Permit { get; }

        public StampAuthorizationFixture()
        {
            using (var image = new Image<Rgba32>(16, 10))
            using (var stream = new MemoryStream())
            {
                // 合成透明背景上的红色矩形，与任何用户印章无关。
                for (int x = 3; x < 13; x++)
                    for (int y = 3; y < 7; y++) image[x, y] = new Rgba32(220, 20, 35, 255);
                image.SaveAsPng(stream);
                Plain = StampAsset.Import("授权测试几何图形", stream.ToArray());
            }
            Protected = StampAuthorization.Protect(Plain, Code, Expires, Now);
            Permit = StampAuthorization.Unlock(Protected, Code, Now);
        }
    }

    public sealed class StampAuthorizationTests : IClassFixture<StampAuthorizationFixture>, IDisposable
    {
        private readonly StampAuthorizationFixture fixture;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "StampAuthorization-" + Guid.NewGuid().ToString("N"));
        private string LibraryPath => Path.Combine(directory, "合成测试库.json");
        public StampAuthorizationTests(StampAuthorizationFixture fixture) { this.fixture = fixture; }

        [Fact]
        public void ProtectionRemovesPlainPngAndRandomizesRepeatedProtection()
        {
            var second = StampAuthorization.Protect(fixture.Plain, StampAuthorizationFixture.Code, fixture.Expires, fixture.Now);
            Assert.Equal("", fixture.Protected.PngBase64);
            Assert.Equal("", second.PngBase64);
            Assert.NotEqual(fixture.Protected.Protection!.Salt, second.Protection!.Salt);
            Assert.NotEqual(fixture.Protected.Protection.IV, second.Protection.IV);
            Assert.NotEqual(fixture.Protected.Protection.Ciphertext, second.Protection.Ciphertext);
            Assert.Null(fixture.Plain.Protection);
            Assert.False(string.IsNullOrEmpty(fixture.Plain.PngBase64));
            AssertImage(fixture.Permit.GetAsset(fixture.Protected, fixture.Now));
            AssertImage(StampAuthorization.Unlock(second, StampAuthorizationFixture.Code, fixture.Now).GetAsset(second, fixture.Now));
        }

        [Fact]
        public void WrongCodeCannotUnlockOrGrantManagementAccess()
        {
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(fixture.Protected, "incorrect-code", fixture.Now));
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.UnlockForManagement(fixture.Protected, "incorrect-code"));
        }

        [Fact]
        public void ValidEmojiIsSupportedButUnpairedUnicodeCannotAliasAnotherCodeOrName()
        {
            const string emojiCode = "test-code-\U0001F512";
            var asset = fixture.Plain.Copy();
            asset.Name = "合成几何\U0001F537";
            var protectedAsset = StampAuthorization.Protect(asset, emojiCode, fixture.Expires, fixture.Now);
            var unlocked = StampAuthorization.Unlock(protectedAsset, emojiCode, fixture.Now).GetAsset(protectedAsset, fixture.Now);
            Assert.Equal(asset.Name, unlocked.Name);
            Assert.Equal(asset.PngBase64, unlocked.PngBase64);
            Assert.Throws<ArgumentException>(() => StampAuthorization.Protect(asset, "test-code-\uD800", fixture.Expires, fixture.Now));
            Assert.Throws<ArgumentException>(() => StampAuthorization.Unlock(protectedAsset, "test-code-\uDC00", fixture.Now));
            asset.Name = "损坏名称\uD800";
            Assert.Throws<InvalidDataException>(() => StampAuthorization.Protect(asset, emojiCode, fixture.Expires, fixture.Now));
        }

        [Theory]
        [InlineData("id")]
        [InlineData("name")]
        [InlineData("width")]
        [InlineData("height")]
        [InlineData("expiry")]
        [InlineData("ciphertext")]
        [InlineData("tag")]
        public void MetadataAndEncryptedContentCannotBeChangedWithOriginalCode(string field)
        {
            var altered = fixture.Protected.Copy();
            switch (field)
            {
                case "id": altered.Id = Guid.NewGuid().ToString("N"); break;
                case "name": altered.Name = "被修改的名称"; break;
                case "width": altered.PixelWidth++; break;
                case "height": altered.PixelHeight++; break;
                case "expiry": altered.Protection!.ExpiresUtcTicks += TimeSpan.TicksPerDay; break;
                case "ciphertext": altered.Protection!.Ciphertext = FlipByte(altered.Protection.Ciphertext); break;
                case "tag": altered.Protection!.Tag = FlipByte(altered.Protection.Tag); break;
            }
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(altered, StampAuthorizationFixture.Code, fixture.Now));
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(altered, fixture.Now));
        }

        [Fact]
        public void RemovingProtectionCannotTurnCiphertextIntoAnUnrestrictedAsset()
        {
            SaveProtectedLibrary();
            var root = JsonNode.Parse(File.ReadAllText(LibraryPath))!;
            root["Assets"]![0]!.AsObject().Remove("Protection");
            File.WriteAllText(LibraryPath, root.ToJsonString());
            Assert.Throws<InvalidDataException>(() => StampLibraryStore.Load(LibraryPath));

            var stripped = fixture.Protected.Copy();
            stripped.Protection = null;
            Assert.Throws<InvalidDataException>(() => stripped.Validate());
            Assert.Throws<InvalidDataException>(() => StampOutputGuard.Resolve(new PlotConfig { Stamp = stripped }, fixture.Now));
        }

        [Fact]
        public void ExpiryBoundaryAndClockRollbackInvalidateAnExistingPermit()
        {
            AssertImage(fixture.Permit.GetAsset(fixture.Protected, fixture.Expires.AddTicks(-1)));
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(fixture.Protected, fixture.Expires));
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(fixture.Protected, fixture.Expires.AddTicks(1)));
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(fixture.Protected, fixture.Now.AddMinutes(-1)));
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(fixture.Protected, StampAuthorizationFixture.Code, fixture.Expires));
        }

        [Fact]
        public void ExpiredAssetCanBeManagedAndReprotectedButCannotBeUsedDirectly()
        {
            var now = DateTimeOffset.UtcNow;
            var expired = StampAuthorization.Protect(fixture.Plain, StampAuthorizationFixture.Code, now.AddHours(-1), now.AddHours(-2));
            var management = StampAuthorization.UnlockForManagement(expired, StampAuthorizationFixture.Code);
            AssertImage(management.GetAssetForManagement(expired));
            Assert.Throws<InvalidOperationException>(() => management.GetAsset(expired, now));
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Protect(expired, "replacement-code", now.AddDays(1), now));
            var renewed = StampAuthorization.Protect(management.GetAssetForManagement(expired), "replacement-code", now.AddDays(1), now);
            AssertImage(StampAuthorization.Unlock(renewed, "replacement-code", now).GetAsset(renewed, now));
            Assert.NotEqual(expired.Protection!.Tag, renewed.Protection!.Tag);
        }

        [Fact]
        public void LegacyPlainLibraryAndProtectedVersionTwoRoundTripWithoutPlaintextLeak()
        {
            var legacy = new StampLibrary();
            legacy.Assets.Add(fixture.Plain.Copy());
            StampLibraryStore.Save(LibraryPath, legacy);
            var old = StampLibraryStore.Load(LibraryPath);
            Assert.Equal(1, old.SchemaVersion);
            Assert.Null(old.Assets.Single().Protection);
            AssertImage(old.Assets.Single());

            old.Assets[0] = fixture.Protected.Copy();
            StampLibraryStore.Save(LibraryPath, old);
            var loaded = StampLibraryStore.Load(LibraryPath);
            Assert.Equal(2, loaded.SchemaVersion);
            Assert.Equal("", loaded.Assets.Single().PngBase64);
            AssertImage(fixture.Permit.GetAsset(loaded.Assets.Single(), fixture.Now));
            var document = JsonNode.Parse(File.ReadAllText(LibraryPath))!;
            Assert.Equal("", document["Assets"]![0]!["PngBase64"]!.GetValue<string>());
            Assert.DoesNotContain(StampAuthorizationFixture.Code, File.ReadAllText(LibraryPath));
            document["SchemaVersion"] = 1;
            File.WriteAllText(LibraryPath, document.ToJsonString());
            Assert.Throws<InvalidDataException>(() => StampLibraryStore.Load(LibraryPath));
        }

        [Theory]
        [InlineData("removed")]
        [InlineData("changed")]
        [InlineData("missing")]
        [InlineData("corrupt")]
        public void SharedLibraryIsCheckedAgainBeforeEveryPage(string change)
        {
            SaveProtectedLibrary();
            var config = AuthorizedConfig();
            var run = new BatchPlotRun(new[] {
                new BatchPage(new PlotFrame(), "page-1.pdf"),
                new BatchPage(new PlotFrame(), "page-2.pdf") }, config);
            int submissions = 0;
            Func<BatchPage, PlotConfig, BatchPageResult> execute = (page, snapshot) =>
            {
                AssertImage(StampOutputGuard.Resolve(snapshot, fixture.Now)!);
                submissions++;
                return new BatchPageResult(true);
            };
            run.Step(execute);
            Assert.Equal(BatchPageState.Succeeded, run.Pages[0].State);
            if (change == "missing") File.Delete(LibraryPath);
            else if (change == "corrupt") File.WriteAllText(LibraryPath, "{broken");
            else
            {
                var library = StampLibraryStore.Load(LibraryPath);
                if (change == "removed") library.Assets.Clear();
                else library.Assets[0].Name = "共享库已修改";
                StampLibraryStore.Save(LibraryPath, library);
            }
            run.Step(execute);
            Assert.Equal(1, submissions);
            Assert.Equal(BatchPageState.Failed, run.Pages[1].State);
            Assert.False(string.IsNullOrWhiteSpace(run.Pages[1].Error));
            Assert.False(run.CanMerge);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PreviouslyUnrestrictedSelectionCannotBypassNewProtectionOrRemoval(bool remove)
        {
            var library = new StampLibrary();
            library.Assets.Add(fixture.Plain.Copy());
            StampLibraryStore.Save(LibraryPath, library);
            var config = AuthorizedConfig();
            config.Stamp = fixture.Plain.Copy();
            config.StampPermit = null;
            AssertImage(StampOutputGuard.Resolve(config, fixture.Now)!);
            if (remove) library.Assets.Clear();
            else library.Assets[0] = fixture.Protected.Copy();
            StampLibraryStore.Save(LibraryPath, library);
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.Now));
        }

        [Fact]
        public void DisabledStampOutputSkipsInvalidAssetAndUnreadableLibrary()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(LibraryPath, "{invalid");
            var config = AuthorizedConfig();
            config.PrintStamps = false;
            config.Stamp!.Protection = null;
            config.StampPermit = null;
            using (var locked = new FileStream(LibraryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Null(StampOutputGuard.Resolve(config, fixture.Expires.AddDays(1)));
            Assert.Equal("{invalid", File.ReadAllText(LibraryPath));
        }

        [Fact]
        public void ProtectedOutputRequiresTheSelectedLibraryIdentityAndPermit()
        {
            SaveProtectedLibrary();
            var config = AuthorizedConfig();
            config.StampPermit = null;
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.Now));
            config = AuthorizedConfig(); config.StampAssetId = Guid.NewGuid().ToString("N");
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.Now));
            config = AuthorizedConfig(); config.StampLibraryPath = "";
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.Now));
        }

        [Fact]
        public void JobSnapshotsAndPermitResultsDoNotExposeMutableProtectedState()
        {
            SaveProtectedLibrary();
            var config = AuthorizedConfig();
            var run = new BatchPlotRun(new[] { new BatchPage(new PlotFrame(), "snapshot.pdf") }, config);
            config.Stamp!.Protection!.ExpiresUtcTicks = fixture.Now.UtcDateTime.Ticks;
            var publicSnapshot = run.Config;
            publicSnapshot.Stamp!.Protection!.Tag = FlipByte(publicSnapshot.Stamp.Protection.Tag);
            var image = fixture.Permit.GetAsset(fixture.Protected, fixture.Now);
            image.PngBase64 = "changed"; image.Name = "changed";
            var managementImage = fixture.Permit.GetAssetForManagement(fixture.Protected);
            managementImage.PixelWidth = 999;
            run.Step((page, snapshot) =>
            {
                AssertImage(StampOutputGuard.Resolve(snapshot, fixture.Now)!);
                return new BatchPageResult(true);
            });
            Assert.True(run.CanMerge);
            AssertImage(fixture.Permit.GetAsset(fixture.Protected, fixture.Now));
            AssertImage(fixture.Permit.GetAssetForManagement(fixture.Protected));
        }

        private void SaveProtectedLibrary()
        {
            var library = new StampLibrary();
            library.Assets.Add(fixture.Protected.Copy());
            StampLibraryStore.Save(LibraryPath, library);
        }

        private PlotConfig AuthorizedConfig() => new PlotConfig {
            PrintStamps = true, Stamp = fixture.Protected.Copy(), StampPermit = fixture.Permit,
            StampLibraryPath = LibraryPath, StampAssetId = fixture.Protected.Id };

        private void AssertImage(StampAsset actual)
        {
            actual.Validate();
            Assert.Null(actual.Protection);
            Assert.Equal(fixture.Plain.Id, actual.Id);
            Assert.Equal(fixture.Plain.Name, actual.Name);
            Assert.Equal(fixture.Plain.PixelWidth, actual.PixelWidth);
            Assert.Equal(fixture.Plain.PixelHeight, actual.PixelHeight);
            Assert.Equal(Convert.FromBase64String(fixture.Plain.PngBase64), Convert.FromBase64String(actual.PngBase64));
        }

        private static string FlipByte(string value)
        {
            var bytes = Convert.FromBase64String(value); bytes[0] ^= 1;
            return Convert.ToBase64String(bytes);
        }

        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class StampDetailsFixture
    {
        public const string Code = "details-only-test-code";
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset ValidUntil => Now.AddHours(1);
        public StampAsset Plain { get; }
        public StampAsset Protected { get; }
        public StampUsePermit Permit { get; }
        public StampDetailsFixture()
        {
            using (var image = new Image<Rgba32>(16, 10))
            using (var stream = new MemoryStream())
            {
                for (int x = 3; x < 13; x++)
                    for (int y = 3; y < 7; y++) image[x, y] = new Rgba32(220, 20, 35, 255);
                image.SaveAsPng(stream);
                Plain = StampAsset.Import("属性测试合成图形", stream.ToArray());
            }
            Plain.Details = new StampDetails { Kind = StampKind.Registration, Sizing = StampSizing.PhysicalSize,
                WidthMm = 40, HeightMm = 20, ValidUntilUtcTicks = ValidUntil.UtcDateTime.Ticks };
            Protected = StampAuthorization.Protect(Plain, Code, Now.AddHours(2), Now);
            Permit = StampAuthorization.Unlock(Protected, Code, Now);
        }
    }

    public sealed class StampDetailsTests : IClassFixture<StampDetailsFixture>, IDisposable
    {
        private readonly StampDetailsFixture fixture;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "StampDetails-" + Guid.NewGuid().ToString("N"));
        private string LibraryPath => Path.Combine(directory, "details.json");
        public StampDetailsTests(StampDetailsFixture fixture) { this.fixture = fixture; }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(100, 0)]
        [InlineData(1, -10)]
        [InlineData(100, -10)]
        [InlineData(100, 10)]
        public void PhysicalDimensionsUseFinalPlotScaleIncludingNegativeMargins(double scale, double margin)
        {
            var plan = Plan(scale, margin);
            var region = StampPlacement.Fit(10 * scale, 10 * scale, 0, 100 * scale, 0, 0, 100 * scale, 16, 10);
            var placed = region.ForOutput(fixture.Plain, plan);
            Assert.Equal(40, Length(placed.Ux, placed.Uy) / plan.ScaleDenominator, 8);
            Assert.Equal(20, Length(placed.Vx, placed.Vy) / plan.ScaleDenominator, 8);
            Assert.Equal(60 * scale, placed.X + (placed.Ux + placed.Vx) / 2, 8);
            Assert.Equal(60 * scale, placed.Y + (placed.Uy + placed.Vy) / 2, 8);
            placed.EnsureWithin(plan);
            if (margin < 0) Assert.True(plan.ScaleDenominator > scale);
            // 指定尺寸的2:1比例不受原始图片16:10像素比例强制约束。
            Assert.Equal(2, Length(placed.Ux, placed.Uy) / Length(placed.Vx, placed.Vy), 8);
        }

        [Theory]
        [InlineData(0, 100, -100, 0)]
        [InlineData(-100, 0, 0, 100)]
        [InlineData(60, 80, -80, 60)]
        public void PhysicalPlacementPreservesRotatedAndMirroredAxes(double ux, double uy, double vx, double vy)
        {
            var placed = StampPlacement.Fit(150, 100, 7, ux, uy, vx, vy, 16, 10).ForOutput(fixture.Plain, Plan());
            Assert.Equal(ux * 0.4, placed.Ux, 8);
            Assert.Equal(uy * 0.4, placed.Uy, 8);
            Assert.Equal(vx * 0.2, placed.Vx, 8);
            Assert.Equal(vy * 0.2, placed.Vy, 8);
            Assert.Equal(150 + (ux + vx) / 2, placed.X + (placed.Ux + placed.Vx) / 2, 8);
            Assert.Equal(100 + (uy + vy) / 2, placed.Y + (placed.Uy + placed.Vy) / 2, 8);
            Assert.Equal(7, placed.Z);
        }

        [Fact]
        public void PhysicalSizingUsesFullTemplateRegionRatherThanPreviouslyFittedImageBounds()
        {
            var asset = fixture.Plain.Copy(); asset.Details!.WidthMm = 80; asset.Details.HeightMm = 90;
            var fitted = StampPlacement.Fit(0, 0, 0, 100, 0, 0, 100, asset.PixelWidth, asset.PixelHeight);
            Assert.Equal(62.5, fitted.Vy, 8);
            var placed = fitted.ForOutput(asset, Plan());
            Assert.Equal(80, placed.Ux, 8); Assert.Equal(90, placed.Vy, 8);
            Assert.Equal(10, placed.X, 8); Assert.Equal(5, placed.Y, 8);
            asset.Details.WidthMm = 30; asset.Details.HeightMm = 40;
            var resized = placed.ForOutput(asset, Plan());
            Assert.Equal(35, resized.X, 8); Assert.Equal(30, resized.Y, 8);
        }

        [Theory]
        [InlineData(101, 20)]
        [InlineData(40, 101)]
        public void OversizePhysicalDimensionsFailWithoutAutomaticShrink(double width, double height)
        {
            var asset = fixture.Plain.Copy(); asset.Details!.WidthMm = width; asset.Details.HeightMm = height;
            var region = StampPlacement.Fit(0, 0, 0, 100, 0, 0, 100, 16, 10);
            Assert.Throws<InvalidOperationException>(() => region.ForOutput(asset, Plan()));
            Assert.Equal(100, region.Ux, 8);
        }

        [Fact]
        public void NegativeMarginCannotSilentlyShrinkAnAlreadyTightPhysicalRegion()
        {
            var region = StampPlacement.Fit(0, 0, 0, 40, 0, 0, 20, 16, 10);
            Assert.Equal(40, region.ForOutput(fixture.Plain, Plan()).Ux, 8);
            Assert.Throws<InvalidOperationException>(() => region.ForOutput(fixture.Plain, Plan(1, -10)));
        }

        [Fact]
        public void LegacyAndFitRegionAssetsRetainTheirOriginalAspectFitting()
        {
            var asset = fixture.Plain.Copy(); asset.Details = null;
            var fitted = StampPlacement.Fit(0, 0, 0, 100, 0, 0, 100, 16, 10);
            Assert.True(fitted.Same(fitted.ForOutput(asset, Plan())));
            asset.Details = new StampDetails { Kind = StampKind.Issue, Sizing = StampSizing.FitRegion };
            Assert.True(fitted.Same(fitted.ForOutput(asset, Plan(100, -10))));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IndependentValidityStopsPlainAndProtectedOutputAtTheExactBoundary(bool protect)
        {
            var selected = protect ? fixture.Protected.Copy() : fixture.Plain.Copy();
            Save(selected);
            var config = Config(selected, protect ? fixture.Permit : null);
            Assert.NotNull(StampOutputGuard.Resolve(config, fixture.ValidUntil.AddTicks(-1)));
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.ValidUntil));
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, fixture.ValidUntil.AddDays(1)));
            Assert.True(fixture.Protected.Protection!.ExpiresUtcTicks > fixture.ValidUntil.UtcDateTime.Ticks);
        }

        [Fact]
        public void LongerAuthorizationCannotExtendAssetValidityAndShorterAuthorizationStillExpiresFirst()
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(fixture.Protected, fixture.ValidUntil));
            var earlier = fixture.Now.AddMinutes(30);
            var encrypted = StampAuthorization.Protect(fixture.Plain, StampDetailsFixture.Code, earlier, fixture.Now);
            var permit = StampAuthorization.Unlock(encrypted, StampDetailsFixture.Code, fixture.Now);
            Assert.NotNull(permit.GetAsset(encrypted, earlier.AddTicks(-1)));
            Assert.Throws<InvalidOperationException>(() => permit.GetAsset(encrypted, earlier));
            var managed = fixture.Permit.GetAssetForManagement(fixture.Protected);
            var renewed = StampAuthorization.Protect(managed, "renewed-test-code", fixture.Now.AddYears(1), fixture.Now);
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(renewed, "renewed-test-code", fixture.ValidUntil));
        }

        [Fact]
        public void DeferredClockDetectsExpiryAfterLibraryAndPermitValidation()
        {
            Save(fixture.Protected.Copy());
            var config = Config(fixture.Protected.Copy(), fixture.Permit);
            int calls = 0;
            Func<DateTimeOffset> crossesDeadline = () => ++calls == 1 ? fixture.ValidUntil.AddTicks(-1) : fixture.ValidUntil;
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, crossesDeadline));
            Assert.True(calls >= 2);
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(fixture.Protected, () => fixture.ValidUntil));
            calls = 0;
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(fixture.Protected, StampDetailsFixture.Code, crossesDeadline));
            Assert.True(calls >= 2);
        }

        [Fact]
        public void DeferredClockIsNotSampledBeforeRejectedLibraryAccessOrWhenOutputIsDisabled()
        {
            Save(fixture.Protected.Copy());
            var config = Config(fixture.Protected.Copy(), fixture.Permit);
            int calls = 0;
            Func<DateTimeOffset> clock = () => { calls++; return fixture.Now; };
            File.WriteAllText(LibraryPath, "{broken");
            Assert.Throws<InvalidDataException>(() => StampOutputGuard.Resolve(config, clock));
            File.Delete(LibraryPath);
            Assert.Throws<InvalidOperationException>(() => StampOutputGuard.Resolve(config, clock));
            config.PrintStamps = false;
            Assert.Null(StampOutputGuard.Resolve(config, clock));
            Assert.Equal(0, calls);
        }

        [Theory]
        [InlineData("kind")]
        [InlineData("sizing")]
        [InlineData("width")]
        [InlineData("height")]
        [InlineData("validity")]
        public void EveryDetailIsAuthenticatedAndCannotBeEditedWithTheOriginalCode(string field)
        {
            var changed = fixture.Protected.Copy();
            switch (field)
            {
                case "kind": changed.Details!.Kind = StampKind.Other; break;
                case "sizing": changed.Details!.Sizing = StampSizing.FitRegion; changed.Details.WidthMm = 0; changed.Details.HeightMm = 0; break;
                case "width": changed.Details!.WidthMm = 41; break;
                case "height": changed.Details!.HeightMm = 21; break;
                case "validity": changed.Details!.ValidUntilUtcTicks = 0; break;
            }
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(changed, StampDetailsFixture.Code, fixture.Now));
            Assert.Throws<InvalidOperationException>(() => fixture.Permit.GetAsset(changed, fixture.Now));
        }

        [Fact]
        public void RemovingDetailsOrDowngradingProtectionCannotBypassTheirAuthentication()
        {
            Assert.Equal(2, fixture.Protected.Protection!.Version);
            var changed = fixture.Protected.Copy(); changed.Details = null;
            Assert.Throws<InvalidDataException>(() => StampAuthorization.Unlock(changed, StampDetailsFixture.Code, fixture.Now));
            changed.Protection!.Version = 1;
            Assert.Throws<InvalidOperationException>(() => StampAuthorization.Unlock(changed, StampDetailsFixture.Code, fixture.Now));
            changed = fixture.Protected.Copy(); changed.Protection!.Version = 1;
            Assert.Throws<InvalidDataException>(() => changed.Validate());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DetailsUpgradeLibraryToSchemaThreeAndCannotBeDowngraded(bool protect)
        {
            Save(protect ? fixture.Protected.Copy() : fixture.Plain.Copy());
            var loaded = StampLibraryStore.Load(LibraryPath);
            Assert.Equal(3, loaded.SchemaVersion);
            AssertDetails(loaded.Assets.Single().Details!);
            if (protect) AssertDetails(fixture.Permit.GetAsset(loaded.Assets.Single(), fixture.Now).Details!);
            loaded.Copy().Assets[0].Details!.WidthMm = 999;
            Assert.Equal(40, loaded.Assets[0].Details!.WidthMm);
            foreach (var olderVersion in new[] { 1, 2 })
            {
                var document = JsonNode.Parse(File.ReadAllText(LibraryPath))!;
                document["SchemaVersion"] = olderVersion;
                File.WriteAllText(LibraryPath, document.ToJsonString());
                Assert.Throws<InvalidDataException>(() => StampLibraryStore.Load(LibraryPath));
            }
        }

        [Fact]
        public void BatchAndPermitCopiesCannotMutateStoredDetails()
        {
            Save(fixture.Protected.Copy());
            var config = Config(fixture.Protected.Copy(), fixture.Permit);
            var run = new BatchPlotRun(new[] { new BatchPage(new PlotFrame(), "details.pdf") }, config);
            config.Stamp!.Details!.WidthMm = 1;
            run.Config.Stamp!.Details!.ValidUntilUtcTicks = fixture.Now.UtcDateTime.Ticks;
            fixture.Permit.GetAsset(fixture.Protected, fixture.Now).Details!.HeightMm = 1;
            fixture.Permit.GetAssetForManagement(fixture.Protected).Details!.Kind = StampKind.Other;
            run.Step((page, snapshot) =>
            {
                AssertDetails(StampOutputGuard.Resolve(snapshot, fixture.Now)!.Details!);
                return new BatchPageResult(true);
            });
            Assert.True(run.CanMerge);
            AssertDetails(fixture.Protected.Details!);
            AssertDetails(fixture.Permit.GetAsset(fixture.Protected, fixture.Now).Details!);
        }

        [Fact]
        public void GenuinePhase18ProtectedFixtureRemainsReadableAndAuthenticated()
        {
            // 由阶段18发布包的 net48 Core 在独立 Windows PowerShell 中生成，未调用 CAD。
            // 原 Core SHA256: 00716437D89D469C80B43D2B5BB37FFDC750CC0A272E4E71E6D1CD5F364BD1BE。
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "phase18-protected-stamp.json");
            Assert.True(File.Exists(path), "缺少阶段18真实旧版 fixture；必须复制到测试输出目录。");
            var library = StampLibraryStore.Load(path);
            Assert.Equal(2, library.SchemaVersion);
            var old = Assert.Single(library.Assets);
            Assert.Null(old.Details); Assert.Equal(1, old.Protection!.Version);
            var permit = StampAuthorization.Unlock(old, "old-only-test-code-2026", fixture.Now);
            var plain = permit.GetAsset(old, fixture.Now);
            Assert.Equal("84a9486c12a14e168b46888c23a91e93", plain.Id);
            Assert.Equal("phase18-synthetic-rectangle", plain.Name);
            using (var image = Image.Load<Rgba32>(Convert.FromBase64String(plain.PngBase64)))
            {
                Assert.Equal(16, image.Width); Assert.Equal(10, image.Height);
                Assert.Equal(0, image[0, 0].A); Assert.Equal(new Rgba32(255, 0, 0, 255), image[5, 4]);
            }
            var copy = new StampLibrary(); copy.Assets.Add(old.Copy());
            StampLibraryStore.Save(LibraryPath, copy);
            var roundTrip = StampLibraryStore.Load(LibraryPath);
            Assert.Equal(2, roundTrip.SchemaVersion);
            Assert.Equal(plain.PngBase64, permit.GetAsset(roundTrip.Assets.Single(), fixture.Now).PngBase64);
            var altered = old.Copy(); altered.Details = new StampDetails();
            Assert.Throws<InvalidDataException>(() => altered.Validate());
        }

        private static PlotPlan Plan(double scale = 1, double margin = 0) => PlotPlanBuilder.Create(
            new PlotFrame { MinX = 0, MinY = 0, MaxX = 420 * scale, MaxY = 297 * scale,
                CalculatedScale = scale, DetectedPaper = new PaperSize("A3", 420, 297) },
            new PlotConfig { MarginMm = margin });
        private static double Length(double x, double y) => Math.Sqrt(x * x + y * y);
        private void Save(StampAsset asset)
        {
            var library = new StampLibrary(); library.Assets.Add(asset); StampLibraryStore.Save(LibraryPath, library);
        }
        private PlotConfig Config(StampAsset asset, StampUsePermit? permit) => new PlotConfig {
            Stamp = asset, StampPermit = permit, StampAssetId = asset.Id, StampLibraryPath = LibraryPath, PrintStamps = true };
        private void AssertDetails(StampDetails details)
        {
            Assert.Equal(StampKind.Registration, details.Kind); Assert.Equal(StampSizing.PhysicalSize, details.Sizing);
            Assert.Equal(40, details.WidthMm); Assert.Equal(20, details.HeightMm);
            Assert.Equal(fixture.ValidUntil.UtcDateTime.Ticks, details.ValidUntilUtcTicks);
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

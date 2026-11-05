using System.IO.Compression;
using System.Text.Json;
using FocusCatAI.Domain.Accessories;
using FluentAssertions;

namespace FocusCatAI.Application.Tests.Accessories;

/// <summary>
/// Pixel-level guard for accessory placement. Reads the real sheets and accessory sprites, so an art change that moves a
/// head, or a placement rule that would clip or float an accessory, fails here rather than on a user's taskbar.
/// </summary>
public class AccessoryAlignmentTests
{
    private static readonly string[] Pets = ["Cat", "ginger", "tuxedo"];
    private static readonly string[] Accessories = ["bowtie", "party_hat", "star_halo"];

    private static string AssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FocusCatAI.Presentation", "Assets");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Assets folder not found above " + AppContext.BaseDirectory);
    }

    private static AccessoryAnchorTable Anchors() =>
        AccessoryAnchorTable.Parse(File.ReadAllText(Path.Combine(AssetsRoot(), "Accessories", "anchors.json")));

    private static Png Load(params string[] parts) => Png.Read(Path.Combine([AssetsRoot(), .. parts]));

    private static IEnumerable<(string Name, string Sheet, int Frames)> Animations(string pet)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AssetsRoot(), "Pets", pet, "pet.json")));
        foreach (var a in doc.RootElement.GetProperty("animations").EnumerateObject())
        {
            yield return (a.Name, a.Value.GetProperty("sheet").GetString()!, a.Value.GetProperty("frames").GetInt32());
        }
    }

    public static IEnumerable<object[]> AllPetsAndAccessories() =>
        from p in Pets from a in Accessories select new object[] { p, a };

    [Fact]
    public void AnchorTableCoversAll14Animations() =>
        Anchors().Animations.Should().HaveCount(14);

    [Theory]
    [InlineData("Cat")]
    [InlineData("ginger")]
    [InlineData("tuxedo")]
    public void AnchorFrameCountsMatchTheManifest(string pet)
    {
        var table = Anchors();
        foreach (var (name, _, frames) in Animations(pet))
        {
            table.FrameCount(name).Should().Be(frames, $"{pet}/{name}");
        }
    }

    [Theory]
    [InlineData("Cat")]
    [InlineData("ginger")]
    [InlineData("tuxedo")]
    public void HeadAnchorSitsOnTheOpaqueTopOfTheHeadInEveryFrame(string pet)
    {
        var table = Anchors();
        foreach (var (name, sheet, frames) in Animations(pet))
        {
            var png = Load("Pets", pet, sheet);
            for (var f = 0; f < frames; f++)
            {
                var a = table.Get(name, f)!.Value;
                var top = png.TopOpaqueRow(f * 64 + a.HeadX, 64);
                top.Should().Be(a.HeadY, $"{pet}/{name}[{f}] head anchor ({a.HeadX},{a.HeadY}) must be the first opaque pixel in its column");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllPetsAndAccessories))]
    public void EveryOpaqueAccessoryPixelStaysInsideTheFrame(string pet, string accessory)
    {
        var table = Anchors();
        var sprite = Load("Accessories", accessory + ".png");
        foreach (var (name, _, frames) in Animations(pet))
        {
            for (var f = 0; f < frames; f++)
            {
                var placement = AccessoryPlacer.Place(accessory, name, table.Get(name, f)!.Value, sprite.Width, sprite.Height);
                if (!placement.Visible)
                {
                    continue;
                }

                foreach (var (x, y) in sprite.OpaquePixels())
                {
                    (placement.X + x).Should().BeInRange(0, 63, $"{pet}/{name}[{f}] {accessory} x");
                    (placement.Y + y).Should().BeInRange(0, 63, $"{pet}/{name}[{f}] {accessory} y");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllPetsAndAccessories))]
    public void PartyHatAndBowtieNeverFloatAwayFromTheCat(string pet, string accessory)
    {
        if (accessory == "star_halo")
        {
            return; // The halo hovers above the head by design; its gap is asserted separately.
        }

        var table = Anchors();
        var sprite = Load("Accessories", accessory + ".png");
        foreach (var (name, sheet, frames) in Animations(pet))
        {
            var cat = Load("Pets", pet, sheet);
            for (var f = 0; f < frames; f++)
            {
                var placement = AccessoryPlacer.Place(accessory, name, table.Get(name, f)!.Value, sprite.Width, sprite.Height);
                if (!placement.Visible)
                {
                    continue;
                }

                var touches = sprite.OpaquePixels().Any(p =>
                    cat.IsOpaque(f * 64 + placement.X + p.X, placement.Y + p.Y + 1)
                    || cat.IsOpaque(f * 64 + placement.X + p.X, placement.Y + p.Y));
                touches.Should().BeTrue($"{pet}/{name}[{f}] {accessory} must rest on the cat, not float");
            }
        }
    }

    [Theory]
    [InlineData("Cat")]
    [InlineData("ginger")]
    [InlineData("tuxedo")]
    public void HaloHoversWithinAFewRowsOfTheHeadInEveryFrame(string pet)
    {
        var table = Anchors();
        var sprite = Load("Accessories", "star_halo.png");
        foreach (var (name, _, frames) in Animations(pet))
        {
            for (var f = 0; f < frames; f++)
            {
                var a = table.Get(name, f)!.Value;
                var placement = AccessoryPlacer.Place("star_halo", name, a, sprite.Width, sprite.Height);
                placement.Visible.Should().BeTrue();
                var gap = a.HeadY - (placement.Y + sprite.Height - 1);
                gap.Should().BeInRange(-sprite.Height, 3, $"{pet}/{name}[{f}] halo gap to head");
            }
        }
    }

    [Theory]
    [InlineData("Cat")]
    [InlineData("ginger")]
    [InlineData("tuxedo")]
    public void BowtieAndHatAreHiddenOrTrackingDuringSleep(string pet)
    {
        var table = Anchors();
        foreach (var (name, _, frames) in Animations(pet).Where(a => a.Name == "Sleep"))
        {
            for (var f = 0; f < frames; f++)
            {
                var anchor = table.Get(name, f)!.Value;
                AccessoryPlacer.Place("bowtie", name, anchor, 11, 5).Visible.Should().BeFalse();
                AccessoryPlacer.Place("party_hat", name, anchor, 9, 8).Visible.Should().BeFalse();
                AccessoryPlacer.Place("star_halo", name, anchor, 13, 5).Visible.Should().BeTrue();
            }
        }
    }

    [Fact]
    public void AccessorySpritesUseTheCatOutlineColour()
    {
        foreach (var accessory in Accessories)
        {
            Load("Accessories", accessory + ".png").HasPixel(0x3A, 0x24, 0x20).Should().BeTrue(accessory);
        }
    }

    [Fact]
    public void PlacementIsDeterministicAndNeverMirrored()
    {
        var a = new AccessoryAnchor(37, 10, 37, 36);
        AccessoryPlacer.Place("party_hat", "WalkLeft", a, 9, 8).Should().Be(AccessoryPlacer.Place("party_hat", "WalkRight", a, 9, 8));
    }

    [Fact]
    public void UnknownAccessoryIsHidden() =>
        AccessoryPlacer.Place("nope", "Idle", new AccessoryAnchor(30, 10, 30, 36), 5, 5).Visible.Should().BeFalse();

    /// <summary>Minimal 8-bit RGBA PNG reader so the tests need no imaging package.</summary>
    private sealed class Png
    {
        private readonly byte[] _rgba;

        private Png(int width, int height, byte[] rgba)
        {
            Width = width;
            Height = height;
            _rgba = rgba;
        }

        public int Width { get; }

        public int Height { get; }

        public bool IsOpaque(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _rgba[((y * Width) + x) * 4 + 3] > 40;

        public int TopOpaqueRow(int x, int limit)
        {
            for (var y = 0; y < limit; y++)
            {
                if (IsOpaque(x, y))
                {
                    return y;
                }
            }

            return limit;
        }

        public IEnumerable<(int X, int Y)> OpaquePixels()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (IsOpaque(x, y))
                    {
                        yield return (x, y);
                    }
                }
            }
        }

        public bool HasPixel(byte r, byte g, byte b) =>
            OpaquePixels().Any(p => _rgba[((p.Y * Width) + p.X) * 4] == r && _rgba[((p.Y * Width) + p.X) * 4 + 1] == g && _rgba[((p.Y * Width) + p.X) * 4 + 2] == b);

        public static Png Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var pos = 8;
            int width = 0, height = 0;
            using var idat = new MemoryStream();
            while (pos < bytes.Length)
            {
                var length = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                var type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
                var data = pos + 8;
                if (type == "IHDR")
                {
                    width = (bytes[data] << 24) | (bytes[data + 1] << 16) | (bytes[data + 2] << 8) | bytes[data + 3];
                    height = (bytes[data + 4] << 24) | (bytes[data + 5] << 16) | (bytes[data + 6] << 8) | bytes[data + 7];
                    if (bytes[data + 8] != 8 || bytes[data + 9] != 6 || bytes[data + 12] != 0)
                    {
                        throw new NotSupportedException($"{path}: only 8-bit non-interlaced RGBA PNG is supported.");
                    }
                }
                else if (type == "IDAT")
                {
                    idat.Write(bytes, data, length);
                }

                pos = data + length + 4;
            }

            idat.Position = 0;
            using var z = new ZLibStream(idat, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            z.CopyTo(raw);
            var src = raw.ToArray();
            var stride = width * 4;
            var rgba = new byte[height * stride];
            for (var y = 0; y < height; y++)
            {
                var filter = src[y * (stride + 1)];
                var line = y * (stride + 1) + 1;
                for (var i = 0; i < stride; i++)
                {
                    int a = i >= 4 ? rgba[(y * stride) + i - 4] : 0;
                    int b = y > 0 ? rgba[((y - 1) * stride) + i] : 0;
                    int c = y > 0 && i >= 4 ? rgba[((y - 1) * stride) + i - 4] : 0;
                    int x = src[line + i];
                    rgba[(y * stride) + i] = (byte)(filter switch
                    {
                        0 => x,
                        1 => x + a,
                        2 => x + b,
                        3 => x + ((a + b) / 2),
                        4 => x + Paeth(a, b, c),
                        _ => throw new InvalidDataException("Bad PNG filter " + filter)
                    });
                }
            }

            return new Png(width, height, rgba);
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}

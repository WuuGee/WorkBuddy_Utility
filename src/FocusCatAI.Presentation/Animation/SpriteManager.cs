using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Presentation.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusCatAI.Presentation.Animation;

public interface ISpriteManager
{
    /// <summary>Loads (and caches) a pet by id; falls back to the configured fallback pet if it is missing or invalid.</summary>
    PetSkin Load(string petId);
}

/// <summary>Also exposes the active pet's animations to the behavior planner.</summary>
public sealed class ActivePetCatalog(ISpriteManager sprites, ISettingsService settings, IPetCatalog pets) : IAnimationCatalog
{
    private PetSkin Skin => sprites.Load(pets.Resolve(settings.Current.Pet.SelectedPetId).Id);

    public bool Has(PetActivity activity) => Skin.Has(activity);

    public IReadOnlyList<PetActivity> WithTag(string tag) => Skin.WithTag(tag);
}

public sealed class SpriteManager(IOptions<PetOptions> options, ILogger<SpriteManager> logger) : ISpriteManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, PetSkin> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PetSkin Load(string petId)
    {
        if (_cache.TryGetValue(petId, out var cached))
        {
            return cached;
        }

        try
        {
            var skin = LoadFromDisk(petId);
            _cache[petId] = skin;
            return skin;
        }
        catch (Exception ex) when (!string.Equals(petId, options.Value.FallbackPetId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(ex, "Failed to load pet '{PetId}', falling back to '{Fallback}'", petId, options.Value.FallbackPetId);
            var fallback = Load(options.Value.FallbackPetId);
            _cache[petId] = fallback;
            return fallback;
        }
    }

    private PetSkin LoadFromDisk(string petId)
    {
        var root = Path.IsPathRooted(options.Value.AssetsRoot)
            ? options.Value.AssetsRoot
            : Path.Combine(AppContext.BaseDirectory, options.Value.AssetsRoot);
        var petDir = Path.Combine(root, "Pets", petId);
        var manifestPath = Path.Combine(petDir, "pet.json");
        logger.LogInformation("Pet manifest: {Path} exists={Exists}", manifestPath, File.Exists(manifestPath));

        var manifest = JsonSerializer.Deserialize<PetManifest>(File.ReadAllText(manifestPath), JsonOptions)
                       ?? throw new InvalidDataException($"Empty manifest: {manifestPath}");
        if (manifest.FrameWidth <= 0 || manifest.FrameHeight <= 0)
        {
            throw new InvalidDataException("frameWidth/frameHeight must be positive.");
        }

        var sheets = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        var clips = new Dictionary<string, SpriteClip>(StringComparer.OrdinalIgnoreCase);
        var tags = new Dictionary<string, List<PetActivity>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, def) in manifest.Animations)
        {
            if (!sheets.TryGetValue(def.Sheet, out var sheet))
            {
                sheet = LoadImage(Path.Combine(petDir, def.Sheet));
                sheets[def.Sheet] = sheet;
            }

            if (def.Frames < 1 || sheet.PixelWidth < def.Frames * manifest.FrameWidth || sheet.PixelHeight < manifest.FrameHeight)
            {
                throw new InvalidDataException($"Sheet '{def.Sheet}' is smaller than {def.Frames} frames of {manifest.FrameWidth}x{manifest.FrameHeight}.");
            }

            logger.LogDebug("Animation {Name}: {Path} exists={Exists} sheet={SheetW}x{SheetH} frames={Frames} frame={FrameW}x{FrameH} lastCrop={Crop}",
                key, Path.Combine(petDir, def.Sheet), File.Exists(Path.Combine(petDir, def.Sheet)), sheet.PixelWidth, sheet.PixelHeight,
                def.Frames, manifest.FrameWidth, manifest.FrameHeight,
                new Int32Rect((def.Frames - 1) * manifest.FrameWidth, 0, manifest.FrameWidth, manifest.FrameHeight));

            var frames = new List<ImageSource>(def.Frames);
            for (var i = 0; i < def.Frames; i++)
            {
                var frame = new CroppedBitmap(sheet, new Int32Rect(i * manifest.FrameWidth, 0, manifest.FrameWidth, manifest.FrameHeight));
                frame.Freeze();
                frames.Add(frame);
            }

            clips[key] = new SpriteClip(frames, Math.Max(def.Fps, 0.1), def.Loop, def.FlipHorizontal);

            foreach (var tag in def.Tags)
            {
                if (!tags.TryGetValue(tag, out var list))
                {
                    tags[tag] = list = [];
                }

                list.Add(new PetActivity(key));
            }
        }

        if (!clips.ContainsKey(PetActivity.Idle.Name))
        {
            throw new InvalidDataException("Manifest must define an 'Idle' animation.");
        }

        logger.LogInformation("Loaded pet '{PetId}' with {Count} animations", petId, clips.Count);
        return new PetSkin(
            manifest,
            clips,
            tags.ToDictionary(t => t.Key, t => (IReadOnlyList<PetActivity>)t.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static BitmapSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FocusCatAI.Domain.Accessories;
using FocusCatAI.Presentation.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FocusCatAI.Presentation.Animation;

/// <summary>What the pet window draws for the accessory layer in the current frame, in unflipped 64x64 frame pixels.</summary>
public readonly record struct AccessoryVisual(ImageSource? Source, double X, double Y, double Width, double Height)
{
    public static AccessoryVisual None { get; } = new(null, 0, 0, 0, 0);
}

/// <summary>Loads accessory sprites and the per-frame anchor table once, and resolves the placement for a frame.</summary>
public sealed class AccessoryRenderer(IOptions<PetOptions> options, ILogger<AccessoryRenderer> logger)
{
    private readonly Dictionary<string, BitmapSource?> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private AccessoryAnchorTable? _anchors;
    private bool _anchorsLoaded;

    private string Folder
    {
        get
        {
            var root = Path.IsPathRooted(options.Value.AssetsRoot) ? options.Value.AssetsRoot : Path.Combine(AppContext.BaseDirectory, options.Value.AssetsRoot);
            return Path.Combine(root, "Accessories");
        }
    }

    /// <summary>The visual for this accessory in this animation frame; <see cref="AccessoryVisual.None"/> if nothing should be drawn.</summary>
    public AccessoryVisual Resolve(string accessoryId, string animation, int frame)
    {
        if (string.IsNullOrWhiteSpace(accessoryId) || accessoryId == AccessoryDefinition.NoneId)
        {
            return AccessoryVisual.None;
        }

        var anchor = Anchors()?.Get(animation, frame);
        var sprite = Sprite(accessoryId);
        if (anchor is null || sprite is null)
        {
            return AccessoryVisual.None;
        }

        var placement = AccessoryPlacer.Place(accessoryId, animation, anchor.Value, sprite.PixelWidth, sprite.PixelHeight);
        return placement.Visible ? new AccessoryVisual(sprite, placement.X, placement.Y, sprite.PixelWidth, sprite.PixelHeight) : AccessoryVisual.None;
    }

    private AccessoryAnchorTable? Anchors()
    {
        if (_anchorsLoaded)
        {
            return _anchors;
        }

        _anchorsLoaded = true;
        var path = Path.Combine(Folder, "anchors.json");
        try
        {
            _anchors = AccessoryAnchorTable.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Accessory anchors could not be loaded from {Path}; accessories are disabled", path);
        }

        return _anchors;
    }

    private BitmapSource? Sprite(string id)
    {
        if (_sprites.TryGetValue(id, out var cached))
        {
            return cached;
        }

        BitmapSource? sprite = null;
        var path = Path.Combine(Folder, id + ".png");
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            sprite = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            logger.LogError(ex, "Accessory sprite {Path} could not be loaded", path);
        }

        _sprites[id] = sprite;
        return sprite;
    }
}

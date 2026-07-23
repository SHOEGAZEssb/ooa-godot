using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Game-wide cache for imported source graphics and immutable OAM compositions.
/// Source Images are private shared read-only values; callers must never mutate
/// an Image returned by this class.
/// </summary>
internal static class OracleGraphicsCache
{

    private static readonly Dictionary<string, Image> SourceImages =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<CompositeKey, Image> CompositeImages = new();
    private static readonly Dictionary<ulong, ulong> ImageHashes = new();
    private static readonly Dictionary<string, AnimationDefinition> AnimationDefinitions =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<OamKey, OamFrame> OamFrames = new();

    internal static int SourceLoadCount { get; private set; }
    internal static int SourceCacheHitCount { get; private set; }
    internal static int CompositeBuildCount { get; private set; }
    internal static int CompositeCacheHitCount { get; private set; }
    internal static int OamBuildCount { get; private set; }
    internal static int OamCacheHitCount { get; private set; }
    internal static int SourceImageCount => SourceImages.Count;
    internal static int AnimationDefinitionCount => AnimationDefinitions.Count;
    internal static int OamFrameCount => OamFrames.Count;

    internal static Image LoadImage(string path)
    {
        if (SourceImages.TryGetValue(path, out Image? cached))
        {
            SourceCacheHitCount++;
            return cached;
        }

        Image image;
        if (ResourceLoader.Exists(path))
        {
            Texture2D texture = ResourceLoader.Load<Texture2D>(
                path, string.Empty, ResourceLoader.CacheMode.Reuse) ??
                throw new InvalidOperationException(
                    $"Could not load graphics resource {path}.");
            image = texture.GetImage();
        }
        else
        {
            // The stable importer can add a PNG that has not yet received a
            // Godot .import sidecar. Decode that generated source directly so
            // the documented importer -> headless-validation workflow works
            // in a clean checkout without an intervening editor launch.
            image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        }
        if (image.IsEmpty())
            throw new InvalidOperationException($"Graphics resource {path} produced an empty image.");
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);

        SourceImages.Add(path, image);
        ImageHashes.Add(image.GetInstanceId(), PixelHash(image));
        SourceLoadCount++;
        return image;
    }

    internal static Image AppendGraphics(Image source, string extraPath)
    {
        Image extra = LoadImage(extraPath);
        int extraOffset = Mathf.CeilToInt(source.GetWidth() / 128.0f) * 128;
        CompositeKey key = new CompositeKey(
            source.GetInstanceId(), extra.GetInstanceId(), extraOffset);
        if (CompositeImages.TryGetValue(key, out Image? cached))
        {
            CompositeCacheHitCount++;
            return cached;
        }

        Image combined = Image.CreateEmpty(
            extraOffset + extra.GetWidth(),
            Math.Max(source.GetHeight(), extra.GetHeight()),
            false,
            Image.Format.Rgba8);
        combined.BlitRect(
            source,
            new Rect2I(0, 0, source.GetWidth(), source.GetHeight()),
            Vector2I.Zero);
        combined.BlitRect(
            extra,
            new Rect2I(0, 0, extra.GetWidth(), extra.GetHeight()),
            new Vector2I(extraOffset, 0));

        CompositeImages.Add(key, combined);
        ImageHashes.Add(combined.GetInstanceId(), PixelHash(combined));
        CompositeBuildCount++;
        return combined;
    }

    internal static OamFrame GetOrCreateOamFrame(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides,
        bool sourceGrayscaleInverted,
        CompositionMode composition,
        Func<OamFrame> factory)
    {
        int loopMarker = encodedOam.LastIndexOf('~');
        if (loopMarker >= 0)
            encodedOam = encodedOam[..loopMarker];

        (ulong colors01, ulong colors23) = PackPalette(paletteOverride);
        ulong sourceId = source.GetInstanceId();
        if (!ImageHashes.TryGetValue(sourceId, out ulong sourceHash))
        {
            sourceHash = PixelHash(source);
            ImageHashes.Add(sourceId, sourceHash);
        }
        OamKey key = new OamKey(
            sourceId,
            sourceHash,
            encodedOam,
            tileBase,
            basePalette,
            paletteOverride is not null,
            colors01,
            colors23,
            PaletteOverridesKey(paletteOverrides),
            sourceGrayscaleInverted,
            composition);
        if (OamFrames.TryGetValue(key, out OamFrame cached))
        {
            OamCacheHitCount++;
            return cached;
        }

        OamFrame created = factory();
        OamFrames.Add(key, created);
        OamBuildCount++;
        return created;
    }

    internal static AnimationDefinition GetAnimationDefinition(string encodedAnimation)
    {
        if (AnimationDefinitions.TryGetValue(
            encodedAnimation, out AnimationDefinition? cached))
        {
            return cached;
        }

        string framesSource = encodedAnimation;
        int loopStart = 0;
        int loopMarker = framesSource.LastIndexOf('~');
        if (loopMarker >= 0 && loopMarker < framesSource.Length - 1 &&
            int.TryParse(framesSource[(loopMarker + 1)..], out int parsedLoopStart))
        {
            loopStart = Math.Max(0, parsedLoopStart);
            framesSource = framesSource[..loopMarker];
        }

        var frames = new List<AnimationFrameDefinition>();
        foreach (string encodedFrame in framesSource.Split(
            '|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            if (separator < 0)
                continue;
            string metadata = encodedFrame[..separator];
            int comma = metadata.IndexOf(',');
            string durationSource = comma < 0 ? metadata : metadata[..comma];
            string parameterSource = comma < 0 ? string.Empty : metadata[(comma + 1)..];
            if (!int.TryParse(durationSource, out int duration) ||
                (parameterSource.Length > 0 &&
                    !int.TryParse(parameterSource, out _)))
            {
                continue;
            }
            int parameter = parameterSource.Length == 0
                ? 0
                : int.Parse(parameterSource);
            frames.Add(new AnimationFrameDefinition(
                Math.Max(1, duration), parameter, encodedFrame[(separator + 1)..]));
        }

        AnimationDefinition definition = new AnimationDefinition(frames.ToArray(), loopStart);
        AnimationDefinitions.Add(encodedAnimation, definition);
        return definition;
    }

    internal static Image LoadRawPngForValidation(string path)
    {
        Image image = new();
        Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not decode validation PNG {path}: {error}.");
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);
        return image;
    }

    internal static ulong PixelHash(Image image)
    {
        ulong hash = 14695981039346656037UL;
        Hash(ref hash, image.GetWidth());
        Hash(ref hash, image.GetHeight());
        Hash(ref hash, (int)image.GetFormat());
        foreach (byte value in image.GetData())
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    internal static void ResetAudit()
    {
        SourceLoadCount = 0;
        SourceCacheHitCount = 0;
        CompositeBuildCount = 0;
        CompositeCacheHitCount = 0;
        OamBuildCount = 0;
        OamCacheHitCount = 0;
    }

    internal static void Shutdown()
    {
        OamFrames.Clear();
        AnimationDefinitions.Clear();
        CompositeImages.Clear();
        SourceImages.Clear();
        ImageHashes.Clear();
        ResetAudit();
    }

    private static (ulong Colors01, ulong Colors23) PackPalette(Color[]? palette)
    {
        if (palette is null)
            return (0, 0);
        if (palette.Length != 4)
            throw new ArgumentException(
                "A cached GBC OBJ palette must contain four colors.", nameof(palette));
        ulong colors01 = palette[0].ToRgba32() | (ulong)palette[1].ToRgba32() << 32;
        ulong colors23 = palette[2].ToRgba32() | (ulong)palette[3].ToRgba32() << 32;
        return (colors01, colors23);
    }

    private static string PaletteOverridesKey(
        IReadOnlyDictionary<int, Color[]>? palettes)
    {
        if (palettes is null || palettes.Count == 0)
            return string.Empty;
        var keys = new List<int>(palettes.Keys);
        keys.Sort();
        var parts = new List<string>(keys.Count);
        foreach (int key in keys)
        {
            Color[] palette = palettes[key];
            if (palette.Length != 4)
            {
                throw new ArgumentException(
                    $"Cached GBC OBJ palette {key} must contain four colors.",
                    nameof(palettes));
            }
            parts.Add($"{key}:" + string.Join(',', Array.ConvertAll(
                palette, color => color.ToRgba32().ToString("x8"))));
        }
        return string.Join(';', parts);
    }

    private static void Hash(ref ulong hash, int value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= 1099511628211UL;
        }
    }
}

internal readonly record struct AnimationFrameDefinition(int Duration, int Parameter, string EncodedOam);

internal enum CompositionMode : byte
{
    Fixed32,
    Positioned
}

internal readonly record struct CompositeKey(ulong BaseImageId, ulong ExtraImageId, int ExtraOffset);

internal readonly record struct OamKey(ulong SourceImageId, ulong SourceHash, string EncodedOam, int TileBase, int BasePalette, bool HasPaletteOverride, ulong PaletteColors01, ulong PaletteColors23, string PaletteOverrides, bool SourceGrayscaleInverted, CompositionMode Composition);

internal sealed record AnimationDefinition(AnimationFrameDefinition[] Frames, int LoopStart);

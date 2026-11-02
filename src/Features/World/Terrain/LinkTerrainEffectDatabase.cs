
namespace Oracles;

/// <summary>
/// Raw terrain-effect OAM drawn around grounded Link by
/// _drawObjectTerrainEffects. Ages selects two green-grass frames from Link's
/// high coordinates and advances four shallow-water frames from the global
/// update counter.
/// </summary>
internal sealed class LinkTerrainEffectDatabase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<LinkTerrainEffectDatabase>> Packs = new(StringComparer.Ordinal);
    static LinkTerrainEffectDatabase() => OracleGraphicsCache.RegisterApplicationCache(Packs.Clear);
    internal static LinkTerrainEffectDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<LinkTerrainEffectDatabase>(() => new(root))).Value;

    public string AssetRoot { get; }

    private readonly IReadOnlyDictionary<byte, LinkTerrainEffectDefinition>
        _definitions;
    private readonly LinkTerrainEffectDefinition _puddle;

    internal LinkTerrainEffectDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "effects/link_terrain_effects.tsv",
            new GeneratedTableSchema(
                "Link terrain effects",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "tile", "frame", "duration", "sound",
                    "sound-start", "sound-period", "sound-duration", "sprite",
                    "tile-base", "palette", "oam", "source"
                ],
                ["kind", "frame"],
                headerRequired: true));
        if (table.Rows.Count != 6)
        {
            throw new InvalidOperationException(
                "Link terrain effects should contain two grass and four " +
                $"puddle frames, got {table.Rows.Count}.");
        }

        var records = new Lookup<string, LinkTerrainEffectFrame>(
            StringComparer.Ordinal);
        var tiles = new Dictionary<string, string>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            string kind = row.RequiredString(0);
            string tileList = row.RequiredString(1);
            string[] tileValues = tileList.Split(',');
            if (!byte.TryParse(tileValues[0], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte tile))
                throw row.Invalid(1, "hexadecimal terrain tile IDs");
            int frame = row.UnsignedDecimal(2);
            int duration = row.UnsignedDecimal(3);
            int sound = row.HexByte(4);
            int soundStart = row.UnsignedDecimal(5);
            int soundPeriod = row.UnsignedDecimal(6);
            int soundDuration = row.UnsignedDecimal(7);
            string sprite = row.RequiredString(8);
            int tileBase = row.UnsignedDecimal(9);
            int palette = row.UnsignedDecimal(10);
            string oam = row.RequiredString(11);
            string source = row.RequiredString(12);
            if (kind is not ("grass" or "puddle") ||
                sprite != "spr_common_sprites" ||
                tileBase != 0 || palette != 0 ||
                duration != (kind == "grass" ? 0 : 8) ||
                sound != (kind == "grass" ? SoundId.MusNone : SoundId.SndSplash) ||
                soundStart != (kind == "grass" ? 0 : 3) ||
                soundPeriod != (kind == "grass" ? 0 : 18) ||
                soundDuration != (kind == "grass" ? 0 : 6))
            {
                throw new InvalidOperationException(
                    $"Imported Link {kind} terrain-effect frame {frame} is invalid.");
            }
            if (tiles.TryGetValue(kind, out string? priorTile) && priorTile != tileList)
            {
                throw new InvalidOperationException(
                    $"Imported Link {kind} terrain-effect frames disagree on " +
                    $"metatile lists {priorTile}/{tileList}.");
            }
            tiles[kind] = tileList;

            Image image = OracleGraphicsCache.LoadImage(
                AssetRoot + $"gfx/{sprite}.png");
            (Texture2D texture, Vector2 offset) =
                OracleOamCompositor.BuildPositionedOamTexture(
                    image,
                    oam,
                    tileBase,
                    palette,
                    paletteOverride: null,
                    sourceGrayscaleInverted: true);
            List<LinkTerrainEffectFrame> frames = records.GetOrAdd(kind);
            if (frame != frames.Count)
            {
                throw new InvalidOperationException(
                    $"Imported Link {kind} terrain-effect frame sequence " +
                    $"expected {frames.Count}, got {frame}.");
            }
            frames.Add(new LinkTerrainEffectFrame(
                kind == "grass"
                    ? LinkTerrainEffectKind.Grass
                    : LinkTerrainEffectKind.Puddle,
                tile,
                frame,
                duration,
                sound,
                soundStart,
                soundPeriod,
                soundDuration,
                texture,
                offset,
                source));
        }

        if (!records.TryGetValues(
                "grass", out IReadOnlyList<LinkTerrainEffectFrame> grass) ||
            grass.Count != 2 ||
            !records.TryGetValues(
                "puddle", out IReadOnlyList<LinkTerrainEffectFrame> puddle) ||
            puddle.Count != 4 ||
            tiles["grass"] == tiles["puddle"])
        {
            throw new InvalidOperationException(
                "Imported Link terrain-effect grass/puddle frame sets are incomplete.");
        }

        _puddle = new LinkTerrainEffectDefinition(puddle);
        var definitions = new Dictionary<byte, LinkTerrainEffectDefinition>();
        foreach (string kind in new[] { "grass", "puddle" })
        {
            var definition = kind == "grass" ? new LinkTerrainEffectDefinition(grass) : _puddle;
            foreach (string value in tiles[kind].Split(','))
            {
                if (!byte.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out byte tile) || !definitions.TryAdd(tile, definition))
                    throw new InvalidOperationException($"Link terrain effects contain an invalid or duplicate tile '{value}' in {AssetRoot}.");
            }
        }
        _definitions = definitions;
    }

    internal LinkTerrainEffectFrame? FrameFor(
        byte tile,
        Vector2 position,
        int globalFrameCounter)
    {
        if (!_definitions.TryGetValue(
                tile, out LinkTerrainEffectDefinition? definition))
        {
            return null;
        }

        IReadOnlyList<LinkTerrainEffectFrame> frames = definition.Frames;
        int duration = frames[0].Duration;
        int index;
        if (duration == 0)
        {
            int xh = Mathf.FloorToInt(position.X);
            int yh = Mathf.FloorToInt(position.Y);
            index = ((xh ^ yh) & 0x04) == 0 ? 0 : 1;
        }
        else
        {
            index = (globalFrameCounter / duration) % frames.Count;
        }
        return frames[index];
    }

    internal bool IsPuddle(byte tile) => _definitions.TryGetValue(tile, out var definition) && ReferenceEquals(definition, _puddle);

}

internal enum LinkTerrainEffectKind
{
    Grass,
    Puddle
}

internal sealed record LinkTerrainEffectFrame(
    LinkTerrainEffectKind Kind,
    byte Tile,
    int Frame,
    int Duration,
    int Sound,
    int SoundStart,
    int SoundPeriod,
    int SoundDuration,
    Texture2D Texture,
    Vector2 Offset,
    string Source);

internal sealed record LinkTerrainEffectDefinition(
    IReadOnlyList<LinkTerrainEffectFrame> Frames);

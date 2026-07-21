using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class OracleRoomData
{
    public const int ViewportWidth = 160;
    public const int ViewportHeight = 128;
    public const int StatusBarHeight = 16;
    public const int GameplayScreenTop = StatusBarHeight;
    public const int ScreenHeight = ViewportHeight + StatusBarHeight;
    public const int MetatileSize = 16;
    public const int LargeRoomWidthInTiles = 15;
    public const int LargeRoomHeightInTiles = 11;

    public int Group { get; }
    public int Id { get; }
    public int TilesetId { get; }
    public int AnimationGroup { get; }
    public int ActiveCollisions { get; }
    public byte TilesetFlags { get; }
    public int WidthInTiles { get; }
    public int HeightInTiles { get; }
    public int Width => WidthInTiles * MetatileSize;
    public int Height => HeightInTiles * MetatileSize;
    public byte[] Layout { get; }
    public byte[] Collisions { get; }
    public Texture2D Texture { get; }

    public enum TerrainType
    {
        Normal,
        Hole,
        WarpHole,
        CrackedFloor,
        Vines,
        Grass,
        Stairs,
        Water,
        Stump,
        UpConveyor,
        RightConveyor,
        DownConveyor,
        LeftConveyor,
        Spike,
        Ice,
        Lava,
        Puddle,
        UpCurrent,
        RightCurrent,
        DownCurrent,
        LeftCurrent,
        RaisableFloor,
        SeaWater,
        Whirlpool
    }

    public enum HazardType
    {
        None,
        Water,
        Hole,
        Lava
    }

    public readonly record struct TerrainInfo(
        byte Tile,
        byte Collision,
        TerrainType Type,
        HazardType Hazard);

    private readonly Image _source;
    private readonly byte[] _originalLayout;
    private readonly byte[] _mappings;
    private readonly Color[,] _palette;
    private Color[,,]? _temporaryBackgroundPalettes;
    private int _temporaryBackgroundPaletteHeader;
    private Color[,]? _temporaryFullBackgroundPalette;
    private float _temporaryFullBackgroundPaletteBlend;
    private readonly Color[] _commonBgPalette0;
    private readonly OracleAnimationData _animations;
    private readonly int _layoutStride;
    private readonly Dictionary<int, byte> _positionCollisionOverrides = new();
    private readonly Dictionary<int, byte[]> _positionMappingOverrides = new();
    private readonly Dictionary<int, byte> _positionVisualOverrides = new();
    private readonly Dictionary<int, DynamicBackgroundTile> _dynamicBackgroundTiles = new();
    private int _animationSignature;
    private int[] _activeAnimationHeaders;

    internal int CurrentAnimationSignature => _animationSignature;
    internal float TemporaryBackgroundPaletteBlend => _temporaryFullBackgroundPaletteBlend;
    internal readonly record struct DynamicBackgroundTile(Image Source, int Tile);

    internal OracleRoomData(
        int group,
        int id,
        int tilesetId,
        int animationGroup,
        int activeCollisions,
        byte tilesetFlags,
        byte[] layout,
        byte[] collisions,
        Image source,
        byte[] mappings,
        Color[,] palette,
        Color[] commonBgPalette0,
        OracleAnimationData animations)
    {
        Group = group;
        Id = id;
        TilesetId = tilesetId;
        AnimationGroup = animationGroup;
        ActiveCollisions = activeCollisions;
        TilesetFlags = tilesetFlags;
        Layout = layout;
        _originalLayout = (byte[])layout.Clone();
        Collisions = collisions;
        _source = source;
        _mappings = mappings;
        _palette = palette;
        _commonBgPalette0 = commonBgPalette0;
        _animations = animations;

        (WidthInTiles, HeightInTiles, _layoutStride) = layout.Length switch
        {
            80 => (10, 8, 10),
            // Large-room data keeps the Game Boy's 16-byte wRoomLayout row
            // stride, but LARGE_ROOM_WIDTH is $0f. The final byte of each row
            // is padding and must never be rendered or treated as playable.
            176 => (LargeRoomWidthInTiles, LargeRoomHeightInTiles, 16),
            _ => throw new InvalidOperationException(
                $"Room {group:x1}:{id:x2} has unsupported layout size {layout.Length}.")
        };
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, 0);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        Texture = ImageTexture.CreateFromImage(RenderRoom(activeHeaders));
    }

    public bool UpdateAnimation(long tick)
    {
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        int signature = GetAnimationSignature(activeHeaders);
        if (signature == _animationSignature)
            return false;

        _animationSignature = signature;
        _activeAnimationHeaders = activeHeaders;
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
        return true;
    }

    internal void SetTemporaryBackgroundPalette(Color[,,] palettes, int header)
    {
        if (palettes.GetLength(0) != 4 || palettes.GetLength(1) != 6 ||
            palettes.GetLength(2) != 4 || header < 0 || header >= palettes.GetLength(0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(palettes), "The Maku Tree effect requires four headers of six 4-color palettes.");
        }
        _temporaryBackgroundPalettes = palettes;
        _temporaryBackgroundPaletteHeader = header;
        ((ImageTexture)Texture).Update(RenderRoom(_activeAnimationHeaders));
    }

    internal void SetTemporaryBackgroundPalette(Color[,] palette, float blend)
    {
        if (palette.GetLength(0) != 6 || palette.GetLength(1) != 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(palette), "A full room override requires six 4-color BG palettes (2-7).");
        }
        _temporaryFullBackgroundPalette = palette;
        _temporaryFullBackgroundPaletteBlend = Mathf.Clamp(blend, 0.0f, 1.0f);
        ((ImageTexture)Texture).Update(RenderRoom(_activeAnimationHeaders));
    }

    internal void ClearTemporaryBackgroundPalette(long tick)
    {
        if (_temporaryBackgroundPalettes is null)
        {
            if (_temporaryFullBackgroundPalette is null)
                return;
        }
        _temporaryBackgroundPalettes = null;
        _temporaryFullBackgroundPalette = null;
        _temporaryFullBackgroundPaletteBlend = 0.0f;
        _activeAnimationHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        _animationSignature = GetAnimationSignature(_activeAnimationHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(_activeAnimationHeaders));
    }

    /// <summary>
    /// Builds one dynamically loaded BG tile with this room's active tileset
    /// palette. Shop prices use this path because $47 writes tile and
    /// attribute bytes directly into w3VramTiles instead of creating OAM.
    /// </summary>
    internal Texture2D BuildBackgroundTileTexture(
        Image source,
        int tile,
        int rawPalette)
    {
        if (tile < 0 || rawPalette is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(tile));
        int sourceX = tile % 16 * 8;
        int sourceY = tile / 16 * 8;
        if (sourceX + 8 > source.GetWidth() || sourceY + 8 > source.GetHeight())
            throw new ArgumentOutOfRangeException(nameof(tile));

        Image output = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        int paletteIndex = Mathf.Clamp(rawPalette - 2, 0, 5);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(sourceX + x, sourceY + y);
            int shade = Mathf.Clamp(
                Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
            Color color = rawPalette == 0
                ? _commonBgPalette0[shade]
                : _palette[paletteIndex, shade];
            output.SetPixel(x, y, color);
        }
        return ImageTexture.CreateFromImage(output);
    }

    public ulong GetAnimationChecksum(long tick)
    {
        byte[] pixels = RenderRoom(_animations.GetActiveHeaders(AnimationGroup, tick)).GetData();
        ulong hash = 14695981039346656037UL;
        foreach (byte value in pixels)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    internal Color GetRenderedPixelForValidation(Vector2I point)
    {
        if (point.X < 0 || point.X >= Width || point.Y < 0 || point.Y >= Height)
            throw new ArgumentOutOfRangeException(nameof(point));
        return RenderRoom(_activeAnimationHeaders).GetPixel(point.X, point.Y);
    }

    internal bool HasAnimationOverride(int destinationTile, long tick)
    {
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        return _animations.TryGetOverride(
            activeHeaders, destinationTile, out _, out _);
    }

    public bool IsSolid(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return false;

        int layoutIndex = tileY * _layoutStride + tileX;
        byte metatile = Layout[layoutIndex];
        byte collision = _positionCollisionOverrides.TryGetValue(
            layoutIndex, out byte collisionOverride)
            ? collisionOverride
            : Collisions[metatile];
        int inTileX = Mathf.PosMod(Mathf.FloorToInt(localPoint.X), MetatileSize);
        int inTileY = Mathf.PosMod(Mathf.FloorToInt(localPoint.Y), MetatileSize);

        if (collision == 0x10)
            return false;

        if (collision is >= 0x11 and <= 0x1f)
        {
            // Port of bank0.s:@specialCollisions for Link. Values $11-$17
            // describe eight two-pixel vertical strips; $18-$1f describe
            // horizontal strips. In particular, stairs ($18) are fully open.
            byte mask = SpecialCollisionMasks[collision - 0x10];
            int axisPosition = collision < 0x18 ? inTileX : inTileY;
            int strip = axisPosition >> 1;
            return (mask & (1 << strip)) != 0;
        }

        if (collision >= 0x20)
            return true;

        int bit = (inTileY < 8 ? 2 : 0) + (inTileX < 8 ? 1 : 0);
        return (collision & (1 << bit)) != 0;
    }

    public byte GetMetatile(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return 0xff;
        return Layout[tileY * _layoutStride + tileX];
    }

    public TerrainInfo GetTerrainInfo(Vector2 localPoint)
    {
        byte metatile = GetMetatile(localPoint);
        if (metatile == 0xff)
            return new TerrainInfo(metatile, 0xff, TerrainType.Normal, HazardType.None);

        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        int layoutIndex = tileY * _layoutStride + tileX;
        byte collision = _positionCollisionOverrides.TryGetValue(
            layoutIndex, out byte collisionOverride)
            ? collisionOverride
            : Collisions[metatile];
        return new TerrainInfo(
            metatile,
            collision,
            GetTerrainType(ActiveCollisions, metatile),
            GetHazardType(ActiveCollisions, metatile));
    }

    public int GetPackedPosition(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        return (tileY << 4) | tileX;
    }

    public byte GetCollision(byte metatile) => Collisions[metatile];

    public byte GetOriginalMetatile(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return 0xff;
        return _originalLayout[tileY * _layoutStride + tileX];
    }

    public bool ReplaceMetatile(Vector2 localPoint, byte expected, byte replacement, long animationTick)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return false;

        int index = tileY * _layoutStride + tileX;
        if (Layout[index] != expected)
            return false;

        Layout[index] = replacement;
        _positionCollisionOverrides.Remove(index);
        _positionMappingOverrides.Remove(index);
        _positionVisualOverrides.Remove(index);
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
        return true;
    }

    internal void SetPositionTileAndCollision(
        Vector2 localPoint,
        byte tile,
        byte? collision,
        long animationTick,
        bool preserveRenderedTile = false)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            throw new ArgumentOutOfRangeException(nameof(localPoint));

        int index = tileY * _layoutStride + tileX;
        byte renderedBefore = GetRenderedMetatile(index);
        bool hadMappingOverride = _positionMappingOverrides.ContainsKey(index);
        Layout[index] = tile;
        if (preserveRenderedTile)
        {
            if (!hadMappingOverride)
                _positionVisualOverrides[index] = renderedBefore;
        }
        else
        {
            _positionMappingOverrides.Remove(index);
            _positionVisualOverrides.Remove(index);
        }
        bool redraw = (!preserveRenderedTile && hadMappingOverride) ||
            renderedBefore != GetRenderedMetatile(index);
        if (collision.HasValue)
            _positionCollisionOverrides[index] = collision.Value;
        else
            _positionCollisionOverrides.Remove(index);

        if (!redraw)
            return;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
    }

    /// <summary>
    /// Mirrors setInterleavedTile: render one half-frame assembled from two
    /// metatile mappings while separately installing tile1 in wRoomLayout.
    /// The existing collision byte is retained until the caller performs the
    /// final ordinary tile write.
    /// </summary>
    internal void SetInterleavedMetatile(
        Vector2 localPoint,
        byte tile1,
        byte tile2,
        int type,
        long animationTick)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            throw new ArgumentOutOfRangeException(nameof(localPoint));
        if (type is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(type));

        int index = tileY * _layoutStride + tileX;
        byte collisionBefore = _positionCollisionOverrides.TryGetValue(
            index, out byte collisionOverride)
            ? collisionOverride
            : Collisions[Layout[index]];
        var mapping = new byte[8];
        Array.Copy(_mappings, tile1 * 8, mapping, 0, mapping.Length);
        int tile2Offset = tile2 * 8;
        switch (type)
        {
            // Top uses tile2's bottom half; bottom retains tile1's bottom half.
            case 0:
                CopyMappingQuarter(mapping, 0, tile2Offset + 2);
                CopyMappingQuarter(mapping, 1, tile2Offset + 3);
                break;
            // Right uses tile2's left half; left retains tile1's left half.
            case 1:
                CopyMappingQuarter(mapping, 1, tile2Offset);
                CopyMappingQuarter(mapping, 3, tile2Offset + 2);
                break;
            // Bottom uses tile2's top half; top retains tile1's top half.
            case 2:
                CopyMappingQuarter(mapping, 2, tile2Offset);
                CopyMappingQuarter(mapping, 3, tile2Offset + 1);
                break;
            // Left uses tile2's right half; right retains tile1's right half.
            case 3:
                CopyMappingQuarter(mapping, 0, tile2Offset + 1);
                CopyMappingQuarter(mapping, 2, tile2Offset + 3);
                break;
        }

        Layout[index] = tile1;
        _positionCollisionOverrides[index] = collisionBefore;
        _positionVisualOverrides.Remove(index);
        _positionMappingOverrides[index] = mapping;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
    }

    internal void ResetAndApplyRoomInitializationChanges(
        IReadOnlyDictionary<int, byte> changes,
        long animationTick)
    {
        ApplyRoomInitializationChanges(changes, animationTick, resetToOriginal: true);
    }

    internal void ApplyRoomInitializationChanges(
        IReadOnlyDictionary<int, byte> changes,
        long animationTick)
    {
        ApplyRoomInitializationChanges(changes, animationTick, resetToOriginal: false);
    }

    private void ApplyRoomInitializationChanges(
        IReadOnlyDictionary<int, byte> changes,
        long animationTick,
        bool resetToOriginal)
    {
        bool redraw = false;
        if (resetToOriginal)
        {
            for (int index = 0; index < Layout.Length; index++)
                redraw |= Layout[index] != _originalLayout[index];
            Array.Copy(_originalLayout, Layout, Layout.Length);
            _positionCollisionOverrides.Clear();
            redraw |= _positionMappingOverrides.Count != 0;
            _positionMappingOverrides.Clear();
            redraw |= _positionVisualOverrides.Count != 0;
            _positionVisualOverrides.Clear();
            redraw |= _dynamicBackgroundTiles.Count != 0;
            _dynamicBackgroundTiles.Clear();
        }

        foreach ((int position, byte tile) in changes)
        {
            int x = position & 0x0f;
            int y = position >> 4;
            if (x < 0 || x >= WidthInTiles || y < 0 || y >= HeightInTiles)
                throw new ArgumentOutOfRangeException(nameof(changes));
            int index = y * _layoutStride + x;
            redraw |= Layout[index] != tile;
            Layout[index] = tile;
            _positionCollisionOverrides.Remove(index);
            redraw |= _positionMappingOverrides.Remove(index);
            redraw |= _positionVisualOverrides.Remove(index);
        }

        if (!redraw)
            return;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
    }

    internal void ApplyMetatileSubstitutions(
        IReadOnlyDictionary<byte, byte> substitutions,
        long animationTick)
    {
        if (substitutions.Count == 0)
            return;

        bool redraw = false;
        for (int y = 0; y < HeightInTiles; y++)
        for (int x = 0; x < WidthInTiles; x++)
        {
            int index = y * _layoutStride + x;
            if (!substitutions.TryGetValue(Layout[index], out byte replacement))
                continue;
            redraw |= Layout[index] != replacement;
            Layout[index] = replacement;
            _positionCollisionOverrides.Remove(index);
            _positionMappingOverrides.Remove(index);
            _positionVisualOverrides.Remove(index);
        }

        if (!redraw)
            return;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
    }

    /// <summary>
    /// Applies a source-style copyRectangleToRoomLayoutAndCollisions block in
    /// one redraw. Gasha growth uses this because its 2x2 tree changes both
    /// layout and collision bytes after ordinary room initialization.
    /// </summary>
    internal void SetMetatileRectangle(
        Vector2 localTopLeft,
        int width,
        IReadOnlyList<byte> tiles,
        IReadOnlyList<byte> collisions,
        long animationTick)
    {
        if (width <= 0 || tiles.Count == 0 || tiles.Count != collisions.Count ||
            tiles.Count % width != 0)
        {
            throw new ArgumentException("Invalid metatile rectangle.", nameof(tiles));
        }

        int startX = Mathf.FloorToInt(localTopLeft.X / MetatileSize);
        int startY = Mathf.FloorToInt(localTopLeft.Y / MetatileSize);
        int height = tiles.Count / width;
        if (startX < 0 || startY < 0 || startX + width > WidthInTiles ||
            startY + height > HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(localTopLeft));
        }

        for (int offset = 0; offset < tiles.Count; offset++)
        {
            int x = startX + offset % width;
            int y = startY + offset / width;
            int index = y * _layoutStride + x;
            Layout[index] = tiles[offset];
            _positionCollisionOverrides[index] = collisions[offset];
            _positionMappingOverrides.Remove(index);
            _positionVisualOverrides.Remove(index);
        }
        Redraw(animationTick);
    }

    /// <summary>
    /// Replaces the 4x4 background-subtile block covering a 2x2 metatile
    /// object. Flip/bank bits are retained while the requested BG palette
    /// replaces the low bits, matching the Gasha disappearance code.
    /// </summary>
    internal void SetSubtileRectangle(
        Vector2 localTopLeft,
        IReadOnlyList<byte> tileIds,
        int rawPalette,
        byte collision,
        long animationTick)
    {
        if (tileIds.Count != 16 || rawPalette is < 0 or > 7)
            throw new ArgumentException("A 2x2 metatile override requires 16 subtiles.", nameof(tileIds));

        int startX = Mathf.FloorToInt(localTopLeft.X / MetatileSize);
        int startY = Mathf.FloorToInt(localTopLeft.Y / MetatileSize);
        if (startX < 0 || startY < 0 || startX + 2 > WidthInTiles ||
            startY + 2 > HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(localTopLeft));
        }

        for (int metatileY = 0; metatileY < 2; metatileY++)
        for (int metatileX = 0; metatileX < 2; metatileX++)
        {
            int index = (startY + metatileY) * _layoutStride + startX + metatileX;
            int mappingOffset = Layout[index] * 8;
            var mapping = new byte[8];
            for (int quarterY = 0; quarterY < 2; quarterY++)
            for (int quarterX = 0; quarterX < 2; quarterX++)
            {
                int quarter = quarterY * 2 + quarterX;
                int sourceRow = metatileY * 2 + quarterY;
                int sourceColumn = metatileX * 2 + quarterX;
                mapping[quarter] = tileIds[sourceRow * 4 + sourceColumn];
                byte attributes = _positionMappingOverrides.TryGetValue(
                    index, out byte[]? previous)
                    ? previous[4 + quarter]
                    : _mappings[mappingOffset + 4 + quarter];
                mapping[4 + quarter] = (byte)((attributes & 0xf0) | rawPalette);
            }
            _positionMappingOverrides[index] = mapping;
            _positionVisualOverrides.Remove(index);
            _positionCollisionOverrides[index] = collision;
        }
        Redraw(animationTick);
    }

    internal void SetDynamicBackgroundTiles(
        IReadOnlyDictionary<int, DynamicBackgroundTile> tiles,
        long animationTick)
    {
        _dynamicBackgroundTiles.Clear();
        foreach ((int destination, DynamicBackgroundTile tile) in tiles)
        {
            int columns = tile.Source.GetWidth() / 8;
            int rows = tile.Source.GetHeight() / 8;
            if (destination is < 0 or > 0xff || tile.Tile < 0 ||
                tile.Source.GetWidth() % 8 != 0 ||
                tile.Source.GetHeight() % 8 != 0 ||
                columns == 0 || rows == 0 || tile.Tile >= columns * rows)
            {
                throw new ArgumentOutOfRangeException(nameof(tiles));
            }
            _dynamicBackgroundTiles.Add(destination, tile);
        }
        Redraw(animationTick);
    }

    internal void CompleteGashaHarvest(
        Vector2 localTopLeft,
        byte replacement,
        long animationTick)
    {
        int startX = Mathf.FloorToInt(localTopLeft.X / MetatileSize);
        int startY = Mathf.FloorToInt(localTopLeft.Y / MetatileSize);
        if (startX < 0 || startY < 0 || startX + 2 > WidthInTiles ||
            startY + 2 > HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(localTopLeft));
        }
        for (int y = 0; y < 2; y++)
        for (int x = 0; x < 2; x++)
        {
            int index = (startY + y) * _layoutStride + startX + x;
            Layout[index] = replacement;
            _positionCollisionOverrides.Remove(index);
            _positionMappingOverrides.Remove(index);
            _positionVisualOverrides.Remove(index);
        }
        _dynamicBackgroundTiles.Clear();
        Redraw(animationTick);
    }

    private void Redraw(long animationTick)
    {
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
    }

    private static readonly byte[] SpecialCollisionMasks =
    {
        0x00, 0xc3, 0x03, 0xc0, 0x00, 0xc3, 0xc3, 0x00,
        0x00, 0xc3, 0x03, 0xc0, 0xc0, 0xc1, 0xff, 0x00
    };

    private static TerrainType GetTerrainType(int activeCollisions, byte tile)
    {
        return activeCollisions switch
        {
            0 or 4 => tile switch
            {
                0xf3 => TerrainType.Hole,
                0xd4 or 0xd5 or 0xd6 => TerrainType.Vines,
                0xf8 => TerrainType.Grass,
                0xd0 => TerrainType.Stairs,
                0xe9 => TerrainType.Whirlpool,
                0xea => TerrainType.Ice,
                0xf9 => TerrainType.Puddle,
                0xfa or 0xfe or 0xff => TerrainType.Water,
                0xfc => TerrainType.SeaWater,
                0xe0 => TerrainType.UpCurrent,
                0xe3 => TerrainType.RightCurrent,
                0xe1 => TerrainType.DownCurrent,
                0xe2 => TerrainType.LeftCurrent,
                0xe4 or 0xe5 or 0xe6 or 0xe7 or 0xe8 => TerrainType.Lava,
                _ => TerrainType.Normal
            },
            1 or 2 or 5 => tile switch
            {
                0x0e or 0x0f => TerrainType.RaisableFloor,
                0xf3 or 0xf4 or 0xf5 or 0xf6 or 0xf7 => TerrainType.Hole,
                0xf9 => TerrainType.Puddle,
                0xfa or 0xfe or 0xff => TerrainType.Water,
                0xfc => TerrainType.SeaWater,
                0x61 or 0x62 or 0x63 or 0x64 or 0x65 => TerrainType.Lava,
                0x50 or 0x51 or 0x52 or 0x53 => TerrainType.Stairs,
                0x48 or 0x49 or 0x4a or 0x4b => TerrainType.WarpHole,
                0x4d => TerrainType.CrackedFloor,
                0x54 => TerrainType.UpConveyor,
                0x55 => TerrainType.RightConveyor,
                0x56 => TerrainType.DownConveyor,
                0x57 => TerrainType.LeftConveyor,
                0x60 => TerrainType.Spike,
                0x8a => TerrainType.Ice,
                _ => TerrainType.Normal
            },
            _ => TerrainType.Normal
        };
    }

    private static HazardType GetHazardType(int activeCollisions, byte tile)
    {
        return activeCollisions switch
        {
            0 or 4 => tile switch
            {
                0xfa or 0xfc or 0xfe or 0xff or 0xe0 or 0xe1 or 0xe2 or 0xe3 or 0xe9 => HazardType.Water,
                0xf3 => HazardType.Hole,
                0xe4 or 0xe5 or 0xe6 or 0xe7 or 0xe8 => HazardType.Lava,
                _ => HazardType.None
            },
            1 or 2 or 5 => tile switch
            {
                0xfa or 0xfc => HazardType.Water,
                0xf3 or 0xf4 or 0xf5 or 0xf6 or 0xf7 or 0x48 or 0x49 or 0x4a or 0x4b => HazardType.Hole,
                0x61 or 0x62 or 0x63 or 0x64 or 0x65 => HazardType.Lava,
                _ => HazardType.None
            },
            _ => HazardType.None
        };
    }

    private Image RenderRoom(int[] activeHeaders)
    {
        var output = Image.CreateEmpty(
            WidthInTiles * MetatileSize,
            HeightInTiles * MetatileSize,
            false,
            Image.Format.Rgba8);

        for (int roomY = 0; roomY < HeightInTiles; roomY++)
        for (int roomX = 0; roomX < WidthInTiles; roomX++)
        {
            int layoutIndex = roomY * _layoutStride + roomX;
            byte[]? mappingOverride = _positionMappingOverrides.TryGetValue(
                layoutIndex, out byte[]? value) ? value : null;
            int mappingOffset = GetRenderedMetatile(layoutIndex) * 8;

            for (int quarter = 0; quarter < 4; quarter++)
            {
                byte tileId = mappingOverride is null
                    ? _mappings[mappingOffset + quarter]
                    : mappingOverride[quarter];
                byte attributes = mappingOverride is null
                    ? _mappings[mappingOffset + 4 + quarter]
                    : mappingOverride[4 + quarter];
                int sourceIndex = tileId >= 0x80 ? tileId - 0x80 : tileId + 0x80;
                Image tileSource = _source;
                int sourceTile = sourceIndex;
                if (_dynamicBackgroundTiles.TryGetValue(
                    sourceIndex, out DynamicBackgroundTile dynamicTile))
                {
                    tileSource = dynamicTile.Source;
                    sourceTile = dynamicTile.Tile;
                }
                else if (_animations.TryGetOverride(
                    activeHeaders, sourceIndex, out Image overrideSource, out int overrideTile))
                {
                    tileSource = overrideSource;
                    sourceTile = overrideTile;
                }
                int sourceColumns = tileSource.GetWidth() / 8;
                int sourceX = (sourceTile % sourceColumns) * 8;
                int sourceY = (sourceTile / sourceColumns) * 8;
                bool flipX = (attributes & 0x20) != 0;
                bool flipY = (attributes & 0x40) != 0;
                int rawPalette = attributes & 0x07;
                int tilesetPaletteIndex = Mathf.Clamp(rawPalette - 2, 0, 5);
                int quarterX = quarter % 2;
                int quarterY = quarter / 2;

                for (int pixelY = 0; pixelY < 8; pixelY++)
                for (int pixelX = 0; pixelX < 8; pixelX++)
                {
                    int readX = sourceX + (flipX ? 7 - pixelX : pixelX);
                    int readY = sourceY + (flipY ? 7 - pixelY : pixelY);
                    Color sourceColor = tileSource.GetPixel(readX, readY);
                    int shade = Mathf.Clamp(Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
                    int writeX = roomX * 16 + quarterX * 8 + pixelX;
                    int writeY = roomY * 16 + quarterY * 8 + pixelY;
                    // initializeGame loads PALH_0f before the tileset palette.
                    // Chest metatiles $f0/$f1 use its red background palette 0.
                    // Palette 1 is transient (for example, textbox colors), so
                    // preserve the existing tileset fallback until that state is
                    // modeled independently.
                    Color color;
                    if (rawPalette == 0)
                    {
                        color = _commonBgPalette0[shade];
                    }
                    else if (_temporaryFullBackgroundPalette is not null && rawPalette is >= 2 and <= 7)
                    {
                        color = _palette[tilesetPaletteIndex, shade].Lerp(
                            _temporaryFullBackgroundPalette[tilesetPaletteIndex, shade],
                            _temporaryFullBackgroundPaletteBlend);
                    }
                    else if (_temporaryBackgroundPalettes is not null && rawPalette is >= 2 and <= 7)
                    {
                        color = _temporaryBackgroundPalettes[
                            _temporaryBackgroundPaletteHeader, rawPalette - 2, shade];
                    }
                    else
                    {
                        color = _palette[tilesetPaletteIndex, shade];
                    }
                    output.SetPixel(writeX, writeY, color);
                }
            }
        }

        return output;
    }

    private byte GetRenderedMetatile(int layoutIndex) =>
        _positionVisualOverrides.TryGetValue(layoutIndex, out byte visualOverride)
            ? visualOverride
            : Layout[layoutIndex];

    private void CopyMappingQuarter(byte[] destination, int quarter, int sourceOffset)
    {
        destination[quarter] = _mappings[sourceOffset];
        destination[4 + quarter] = _mappings[sourceOffset + 4];
    }

    private static int GetAnimationSignature(int[] headers)
    {
        int signature = 17;
        foreach (int header in headers)
            signature = signature * 31 + header;
        return signature;
    }
}

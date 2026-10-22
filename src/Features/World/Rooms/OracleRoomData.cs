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
    public byte TilesetFlags { get; private set; }
    private readonly byte _importedTilesetFlags;
    internal void OrTilesetFlags(byte flags) => TilesetFlags |= flags;
    internal bool IsCompanionRegion { get; init; }
    internal bool LoadsUniqueGraphicsAfterScroll { get; init; }
    internal int TilesetPaletteId { get; init; }
    private Image? _liveGraphics;
    private bool _freezeGraphicsOverrides;
    public int WidthInTiles { get; }
    public int HeightInTiles { get; }
    public int Width => WidthInTiles * MetatileSize;
    public int Height => HeightInTiles * MetatileSize;
    public byte[] Layout { get; }
    public byte[] Collisions { get; }
    internal Action ClearNativeCutsceneTerrain()
    {
        // endgameCutscenes.s:$09 stage1 -> CUTSCENE_CREDITS clears the
        // live wRoomLayout/wRoomCollisions, retaining imported room data.
        var layout=(byte[])Layout.Clone();var collisions=(byte[])Collisions.Clone();
        Array.Clear(Layout);Array.Clear(Collisions);
        _roomTextureDirty = true;
        return ()=>{layout.CopyTo(Layout,0);collisions.CopyTo(Collisions,0);ReplaceRoomTexture(_activeAnimationHeaders);};
    }
    private Texture2D? _texture;
    public Texture2D Texture
    {
        get
        {
            if (_texture is null)
            {
                using var diagnostics = OracleRoomLoadDiagnostics.Begin("publish-room", Group, Id);
                using Image image = RenderRoom(_activeAnimationHeaders);
                _texture = ImageTexture.CreateFromImage(image);
                RememberRoomTextureInputs();
            }
            return _texture;
        }
    }
    private Texture2D? _underwaterBackgroundTexture;
    internal Texture2D UnderwaterBackgroundTexture
    {
        get
        {
            if (_underwaterBackgroundTexture is null)
            {
                using Image image = RenderUnderwaterBackground();
                _underwaterBackgroundTexture = ImageTexture.CreateFromImage(image);
                RememberRoomTextureInputs();
            }
            return _underwaterBackgroundTexture;
        }
    }
    private Texture2D? _clearedTilemapTexture;
    public Texture2D ClearedTilemapTexture
    {
        get
        {
            if (_clearedTilemapTexture is null)
            {
                using var diagnostics = OracleRoomLoadDiagnostics.Begin("publish-clear", Group, Id);
                _clearedTilemapTexture = _clearTile.Texture;
            }
            return _clearedTilemapTexture;
        }
    }

    private readonly Image _source;
    private readonly RoomClearTile _clearTile;
    private readonly byte[] _originalLayout;
    private readonly byte[] _underlyingLayout;
    private readonly byte[] _mappings;
    private readonly Color[,] _tilesetPalette;
    private readonly BackgroundPaletteState _backgroundPalettes;
    private readonly RoomTexturePublication? _texturePublication;
    private bool _temporaryBackgroundPaletteActive;
    private float _temporaryFullBackgroundPaletteBlend;
    private int? _temporaryBackgroundPaletteOffset;
    private readonly OracleAnimationData _animations;
    private readonly int _layoutStride;
    private readonly Dictionary<int, byte> _positionCollisionOverrides = new();
    private readonly Dictionary<int, byte[]> _positionMappingOverrides = new();
    private readonly Dictionary<int, byte> _positionVisualOverrides = new();
    private readonly Dictionary<int, DynamicBackgroundTile> _dynamicBackgroundTiles = new();
    private int _animationSignature;
    private int[] _activeAnimationHeaders;
    private readonly Color[,] _renderedBackgroundPalettes = new Color[8, 4];
    private Image? _renderedLiveGraphics;
    private bool _renderedFreezeGraphicsOverrides;
    private int _renderedAnimationSignature;
    private bool _roomTextureDirty;
    private byte[]? _renderPixels, _underwaterRenderPixels;
    private readonly byte[] _palettePixels = new byte[128];
    private readonly Color[,] _capturedPalette = new Color[8, 4];
    private bool _paletteCaptured;
    private readonly Dictionary<Image, (OracleImagePixels Snapshot, byte[] Pixels, int Width)> _renderSources = new();
    private readonly int[] _paletteRevisions = new int[8];
    private RasterTile[]? _renderTiles, _underwaterRenderTiles;
    private readonly record struct RasterTile(OracleImagePixels Pixels, int Tile, byte Attributes, int PaletteRevision);

    internal int CurrentAnimationSignature => _animationSignature;
    internal void CopyLayoutToUnderlying() => Array.Copy(Layout, _underlyingLayout, Layout.Length);
    internal float TemporaryBackgroundPaletteBlend => _temporaryFullBackgroundPaletteBlend;
    internal int? TemporaryBackgroundPaletteOffset => _temporaryBackgroundPaletteOffset;
    internal BackgroundPaletteState BackgroundPalettes => _backgroundPalettes;

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
        Image hudGraphics,
        byte[] mappings,
        Color[,] palette,
        BackgroundPaletteState backgroundPalettes,
        OracleAnimationData animations,
        RoomTexturePublication? texturePublication = null,
        RoomClearTile? clearTile = null)
    {
        using var diagnostics = OracleRoomLoadDiagnostics.Begin("logical-room", group, id);
        Group = group;
        Id = id;
        TilesetId = tilesetId;
        AnimationGroup = animationGroup;
        ActiveCollisions = activeCollisions;
        TilesetFlags = tilesetFlags;
        _importedTilesetFlags = tilesetFlags;
        Layout = layout;
        _originalLayout = (byte[])layout.Clone();
        _underlyingLayout = (byte[])layout.Clone();
        Collisions = collisions;
        _source = source;
        _clearTile = clearTile ?? new RoomClearTile(hudGraphics, backgroundPalettes);
        _mappings = mappings;
        _tilesetPalette = palette;
        _backgroundPalettes = backgroundPalettes;
        _texturePublication = texturePublication;
        _animations = animations;
        diagnostics?.Mark("state");

        (WidthInTiles, HeightInTiles, _layoutStride) = layout.Length switch
        {
            80 => (10, 8, 10),
            // Large-room data keeps the Game Boy's 16-byte wRoomLayout row
            // stride, but LARGE_ROOM_WIDTH is $0f. The final byte of each row
            // is padding and must never be treated as playable. Underwater
            // SCX waves can expose its pixels beyond the playable boundary.
            176 => (LargeRoomWidthInTiles, LargeRoomHeightInTiles, 16),
            _ => throw new InvalidOperationException(
                $"Room {group:x1}:{id:x2} has unsupported layout size {layout.Length}.")
        };
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, 0);
        diagnostics?.Mark("initial-animation");
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        // Loading logical room state must not publish an image under the
        // previous room's palette. Presenters acquire textures after palette,
        // persistent-layout and live VRAM preparation at their normal boundary.
        // Once acquired, texture identity and per-update publication stay stable.
    }

    public bool UpdateAnimation(long tick)
    {
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        int signature = GetAnimationSignature(activeHeaders);
        if (signature == _animationSignature)
            return false;

        _animationSignature = signature;
        _activeAnimationHeaders = activeHeaders;
        ReplaceRoomTexture(activeHeaders);
        return true;
    }

    internal void SetTemporaryBackgroundPalette(Color[,,] palettes, int header)
    {
        _temporaryBackgroundPaletteActive = true;
        _backgroundPalettes.LoadPaletteHeader(palettes, header);
    }

    internal void SetTemporaryBackgroundPalette(Color[,] palette, float blend)
    {
        if (palette.GetLength(0) != 6 || palette.GetLength(1) != 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(palette), "A full room override requires six 4-color BG palettes (2-7).");
        }
        _temporaryBackgroundPaletteActive = true;
        _temporaryFullBackgroundPaletteBlend = Mathf.Clamp(blend, 0.0f, 1.0f);
        _backgroundPalettes.BlendTileset(
            _tilesetPalette, palette, _temporaryFullBackgroundPaletteBlend);
    }

    internal void ClearTemporaryBackgroundPalette(long tick)
    {
        if (!_temporaryBackgroundPaletteActive)
            return;
        _temporaryBackgroundPaletteActive = false;
        _temporaryFullBackgroundPaletteBlend = 0.0f;
        _activeAnimationHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        _animationSignature = GetAnimationSignature(_activeAnimationHeaders);
        if (_temporaryBackgroundPaletteOffset.HasValue)
        {
            _backgroundPalettes.OffsetTileset(
                _tilesetPalette, _temporaryBackgroundPaletteOffset.Value);
        }
        else
        {
            _backgroundPalettes.LoadTileset(_tilesetPalette);
        }
        RefreshPaletteTextures();
    }

    /// <summary>
    /// Applies the signed GBC component offset used by the room-darkening
    /// palette thread to BG palettes 2-7. BG palettes 0-1, sprites, and the
    /// screen-space HUD remain unchanged.
    /// </summary>
    internal void SetTemporaryBackgroundPaletteOffset(int offset)
    {
        if (offset is < -31 or > 31)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (_temporaryBackgroundPaletteOffset == offset)
            return;
        _temporaryBackgroundPaletteOffset = offset;
        if (!_temporaryBackgroundPaletteActive)
            _backgroundPalettes.OffsetTileset(_tilesetPalette, offset);
    }

    /// <summary>
    /// Resolves one active Game Boy background-palette color. Text control
    /// <c>\item</c> writes raw attribute $84, so its 2bpp trade-item glyphs
    /// use room BG palette 4 rather than either ordinary textbox palette.
    /// </summary>
    internal Color ResolveBackgroundPaletteColor(int rawPalette, int shade)
    {
        return _backgroundPalettes.Resolve(rawPalette, shade);
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
        OracleImagePixels pixels = OracleGraphicsCache.GetImagePixels(source);
        if (sourceX + 8 > pixels.Width || sourceY + 8 > pixels.Height)
            throw new ArgumentOutOfRangeException(nameof(tile));

        var output = new OracleImageCanvas(8, 8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = pixels.GetPixel(sourceX + x, sourceY + y);
            int shade = Mathf.Clamp(
                Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
            output.SetPixel(
                x, y, _backgroundPalettes.Resolve(rawPalette, shade));
        }
        using Image image = output.ToImage();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Mirrors itemMimicBgTile for a lifted metatile. The four current BG
    /// quarters retain their mapping, flips, animation replacement, and
    /// active palette, while graphics color 0 becomes transparent as it does
    /// after that BG palette is copied into OBJ palette 7.
    /// </summary>
    internal Texture2D BuildMimickedMetatileTexture(Vector2 point)
    {
        int tileX = Mathf.FloorToInt(point.X / MetatileSize);
        int tileY = Mathf.FloorToInt(point.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles ||
            tileY < 0 || tileY >= HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }

        int layoutIndex = tileY * _layoutStride + tileX;
        byte[]? mappingOverride = _positionMappingOverrides.TryGetValue(
            layoutIndex, out byte[]? value) ? value : null;
        int mappingOffset = GetRenderedMetatile(layoutIndex) * 8;
        return BuildMimickedMetatileTexture(mappingOffset, mappingOverride);
    }

    /// <summary>
    /// Mirrors objectMimicBgTile when the source passes an explicit metatile
    /// index rather than reading the room-layout position.
    /// </summary>
    internal Texture2D BuildMimickedMetatileTexture(byte metatile) =>
        BuildMimickedMetatileTexture(metatile * 8, mappingOverride: null);

    private Texture2D BuildMimickedMetatileTexture(
        int mappingOffset,
        byte[]? mappingOverride)
    {
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        for (int quarter = 0; quarter < 4; quarter++)
        {
            byte tileId = mappingOverride is null
                ? _mappings[mappingOffset + quarter]
                : mappingOverride[quarter];
            byte attributes = mappingOverride is null
                ? _mappings[mappingOffset + 4 + quarter]
                : mappingOverride[4 + quarter];
            int sourceIndex = tileId >= 0x80 ? tileId - 0x80 : tileId + 0x80;
            ResolveBackgroundGraphics(_activeAnimationHeaders, sourceIndex, out Image tileSource, out int sourceTile);

            int sourceColumns = tileSource.GetWidth() / 8;
            int sourceX = sourceTile % sourceColumns * 8;
            int sourceY = sourceTile / sourceColumns * 8;
            bool flipX = (attributes & 0x20) != 0;
            bool flipY = (attributes & 0x40) != 0;
            int rawPalette = attributes & 0x07;
            int quarterX = quarter % 2;
            int quarterY = quarter / 2;

            for (int pixelY = 0; pixelY < 8; pixelY++)
            for (int pixelX = 0; pixelX < 8; pixelX++)
            {
                int readX = sourceX + (flipX ? 7 - pixelX : pixelX);
                int readY = sourceY + (flipY ? 7 - pixelY : pixelY);
                Color sourceColor = tileSource.GetPixel(readX, readY);
                int shade = Mathf.Clamp(
                    Mathf.RoundToInt((1.0f - sourceColor.R) * 3.0f), 0, 3);
                Color color;
                if (shade == 0)
                {
                    color = Colors.Transparent;
                }
                else
                {
                    color = _backgroundPalettes.Resolve(rawPalette, shade);
                }

                output.SetPixel(
                    quarterX * 8 + pixelX,
                    quarterY * 8 + pixelY,
                    color);
            }
        }

        return ImageTexture.CreateFromImage(output);
    }

    public ulong GetAnimationChecksum(long tick)
    {
        using Image rendered = RenderRoom(_animations.GetActiveHeaders(AnimationGroup, tick));
        byte[] pixels = rendered.GetData();
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
        using Image rendered = RenderRoom(_activeAnimationHeaders);
        return rendered.GetPixel(point.X, point.Y);
    }

    internal byte GetBackgroundSubtileForValidation(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= WidthInTiles * 2 ||
            tileY < 0 || tileY >= HeightInTiles * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(tileX));
        }

        int index = tileY / 2 * _layoutStride + tileX / 2;
        int quarter = tileY % 2 * 2 + tileX % 2;
        if (_positionMappingOverrides.TryGetValue(
                index, out byte[]? mappingOverride))
        {
            return mappingOverride[quarter];
        }
        return _mappings[GetRenderedMetatile(index) * 8 + quarter];
    }

    internal byte GetBackgroundAttributeForValidation(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= WidthInTiles * 2 ||
            tileY < 0 || tileY >= HeightInTiles * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(tileX));
        }

        int index = tileY / 2 * _layoutStride + tileX / 2;
        int quarter = tileY % 2 * 2 + tileX % 2;
        if (_positionMappingOverrides.TryGetValue(
                index, out byte[]? mappingOverride))
        {
            return mappingOverride[4 + quarter];
        }
        return _mappings[GetRenderedMetatile(index) * 8 + 4 + quarter];
    }

    internal bool HasAnimationOverride(int destinationTile, long tick)
    {
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, tick);
        return _animations.TryGetOverride(
            activeHeaders, destinationTile, out _, out _);
    }

    // bank0.objectCheckSimpleCollision treats the raw collision byte as four
    // quarter bits, including values used by the special-collision helpers.
    internal bool HasSimpleCollision(Vector2 localPoint)
    {
        int x=OracleObjectPosition.HighByte(localPoint.X),y=OracleObjectPosition.HighByte(localPoint.Y);
        int bit=((y&8)==0?2:0)+((x&8)==0?1:0);
        return (GetTerrainInfo(new(x,y)).Collision & (1<<bit)) != 0;
    }

    public bool IsSolid(Vector2 localPoint) =>
        IsSolid(localPoint, SpecialCollisionMasks);

    // link.s @checkTileCollisionAt_allowRaisedFl clears collision $1e.
    // Its rst_addAToHl also replaces A with the table address's low byte
    // ($1e + index in clean US), so the subsequent cp $08 always selects Y.
    // Preserve this native quirk even for normally vertical $11-$17 strips.
    internal bool IsSolidForRaisedFloorLink(Vector2 localPoint) =>
        GetTerrainInfo(localPoint).Collision != 0x1e &&
        IsSolid(localPoint, SpecialCollisionMasks, horizontalSpecialStrips: true);

    /// <summary>
    /// Applies checkTileCollisionAt_disallowHoles when holesAreWalls is true.
    /// In the source, SPECIALCOLLISION_HOLE $10 also covers water and lava.
    /// </summary>
    internal bool IsSolidForEnemyMovement(
        Vector2 localPoint,
        bool holesAreWalls) =>
        IsSolid(
            localPoint,
            holesAreWalls
                ? EnemySpecialCollisionMasks
                : SpecialCollisionMasks);

    private bool IsSolid(Vector2 localPoint, byte[] specialCollisionMasks, bool horizontalSpecialStrips = false)
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
        return IsSolidCollisionByte(localPoint, collision, specialCollisionMasks, horizontalSpecialStrips);
    }

    internal bool IsSolidCollisionByte(Vector2 localPoint, byte collision, bool holesAreWalls) =>
        IsSolidCollisionByte(localPoint, collision, holesAreWalls ? EnemySpecialCollisionMasks : SpecialCollisionMasks);

    private static bool IsSolidCollisionByte(Vector2 localPoint, byte collision, byte[] specialCollisionMasks,
        bool horizontalSpecialStrips = false)
    {
        int inTileX = Mathf.PosMod(Mathf.FloorToInt(localPoint.X), MetatileSize);
        int inTileY = Mathf.PosMod(Mathf.FloorToInt(localPoint.Y), MetatileSize);

        if (collision is >= 0x10 and <= 0x1f)
        {
            // Port of bank0.s:@specialCollisions for Link. Values $11-$17
            // describe eight two-pixel vertical strips; $18-$1f describe
            // horizontal strips. In particular, stairs ($18) are fully open.
            byte mask = specialCollisionMasks[collision - 0x10];
            int axisPosition = !horizontalSpecialStrips && collision < 0x18 ? inTileX : inTileY;
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
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        // $ff is also a real tile (including waterfall bottoms). Only an
        // out-of-room coordinate represents the collision-buffer sentinel.
        if (tileX < 0 || tileY < 0 || tileX >= WidthInTiles || tileY >= HeightInTiles)
            return new TerrainInfo(metatile, 0xff, TerrainType.Normal, HazardType.None);
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

    internal byte GetPackedStorageMetatile(byte position)
    {
        int x = position&15, y = position>>4;
        if (x >= _layoutStride || y >= HeightInTiles)
            throw new InvalidOperationException($"Room${Group:x1}:${Id:x2} has no layout storage at${position:x2}.");
        return Layout[y*_layoutStride+x];
    }

    internal byte ReadNativeLayoutByte(byte position, OracleRuntimeState memory)
    {
        // bank0.loadRoomLayout clears $cf00-$cfbf, including unused rows
        // and small-room padding. The rest of the addressed page aliases
        // live script variables ($cfc0-$cfff), not additional room storage.
        if (position >= 0xc0) return memory.ReadWramByte(0xcf00 + position);
        int x = position & 15, y = position >> 4;
        return x < _layoutStride && y < HeightInTiles ? Layout[y * _layoutStride + x] : (byte)0;
    }

    internal byte[] CaptureStorageCollisions()
    {
        byte[] result=new byte[Layout.Length];
        for (int index=0;index<result.Length;index++)
            result[index]=_positionCollisionOverrides.TryGetValue(index,out byte value) ? value : Collisions[Layout[index]];
        return result;
    }

    internal byte[] CaptureBackgroundMappings(bool nativeStorage=false)
    {
        int width=nativeStorage?32:WidthInTiles*2, height=nativeStorage?22:HeightInTiles*2;
        byte[] result=new byte[width*height*2];
        for (int y=0;y<height;y++) for (int x=0;x<width;x++)
        {
            int index=y/2*_layoutStride+x/2, quarter=(y&1)*2+(x&1);
            bool playable=x<WidthInTiles*2&&y<HeightInTiles*2;
            int metatile=playable?GetRenderedMetatile(index):_layoutStride==16&&index<Layout.Length?Layout[index]:0;
            byte[] mapping=playable&&_positionMappingOverrides.TryGetValue(index,out byte[]? value) ? value :
                _mappings[(metatile*8)..(metatile*8+8)];
            int output=(y*width+x)*2;
            result[output]=mapping[quarter]; result[output+1]=mapping[quarter+4];
        }
        return result;
    }

    public byte GetOriginalMetatile(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return 0xff;
        return _originalLayout[tileY * _layoutStride + tileX];
    }

    internal byte GetUnderlyingMetatile(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles ||
            tileY < 0 || tileY >= HeightInTiles)
        {
            return 0xff;
        }
        return _underlyingLayout[tileY * _layoutStride + tileX];
    }

    internal void SetUnderlyingMetatile(Vector2 localPoint, byte tile)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles ||
            tileY < 0 || tileY >= HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(localPoint));
        }
        _underlyingLayout[tileY * _layoutStride + tileX] = tile;
    }

    internal void SetUnderlyingStorageMetatile(int packedPosition, byte tile)
    {
        int tileX = packedPosition & 0x0f;
        int tileY = packedPosition >> 4;
        if (packedPosition < 0 || tileX >= _layoutStride ||
            tileY < 0 || tileY >= HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(packedPosition));
        }
        _underlyingLayout[tileY * _layoutStride + tileX] = tile;
    }

    internal byte GetUnderlyingStorageMetatile(int position) => _underlyingLayout[position];
    internal void SetStorageLayoutWithoutGraphics(int position, byte tile)
    {
        // A direct WRAM layout write does not update the background mapping.
        // Preserve it even if no earlier queued setTile write was accepted.
        if (!_positionMappingOverrides.ContainsKey(position))
            _positionVisualOverrides[position] = GetRenderedMetatile(position);
        if (position % _layoutStride >= WidthInTiles && Layout[position] != tile)
            _roomTextureDirty = true;
        Layout[position] = tile;
    }

    internal void SetStorageTileAndCollision(int position, byte tile, byte collision, long tick)
    {
        if ((position & 15) < WidthInTiles)
            SetPositionTileAndCollision(new((position & 15) * 16 + 8, (position >> 4) * 16 + 8), tile, collision, tick,
                preserveRenderedTile: true);
        else
        {
            if (Layout[position] != tile) _roomTextureDirty = true;
            Layout[position] = tile;
            _positionCollisionOverrides[position] = collision;
        }
    }

    internal void SetPackedTileCollision(byte position, byte? collision)
    {
        int index = (position >> 4) * _layoutStride + (position & 15);
        if (collision.HasValue)
            _positionCollisionOverrides[index] = collision.Value;
        else
            _positionCollisionOverrides.Remove(index);
    }

    internal bool RestoreUnderlyingMetatileRange(
        byte firstTile,
        int count,
        long animationTick)
    {
        if (count <= 0 || firstTile + count > 0x100)
            throw new ArgumentOutOfRangeException(nameof(count));

        var changes = new Dictionary<int, byte>();
        for (int y = 0; y < HeightInTiles; y++)
        for (int x = 0; x < WidthInTiles; x++)
        {
            int index = y * _layoutStride + x;
            byte tile = _underlyingLayout[index];
            if (tile < firstTile || tile >= firstTile + count ||
                Layout[index] == tile)
            {
                continue;
            }
            changes[(y << 4) | x] = tile;
        }

        if (changes.Count == 0)
            return false;
        ApplyRoomInitializationChanges(changes, animationTick);
        return true;
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
        ReplaceRoomTexture(activeHeaders);
        return true;
    }

    // setTile changes terrain immediately; its queued graphics must retain
    // the previously displayed mapping until updateChangedTileQueue runs.
    internal void SetTileWithoutGraphicsReload(ChangedTileWrite write, long animationTick)
    {
        Vector2 position = new((write.Position & 15) * MetatileSize + 8,
            (write.Position >> 4) * MetatileSize + 8);
        SetPositionTileAndCollision(position,write.Tile,null,animationTick,preserveRenderedTile: true);
    }

    internal void ApplyQueuedTileGraphics(ChangedTileWrite write, long animationTick)
    {
        int x = write.Position & 15, y = write.Position >> 4;
        if (x >= WidthInTiles || y >= HeightInTiles)
            throw new ArgumentOutOfRangeException(nameof(write),$"Queued tile position${write.Position:x2} is outside the room.");
        int index = y * _layoutStride + x;
        bool changed = _positionMappingOverrides.Remove(index);
        changed |= GetRenderedMetatile(index) != write.Tile;
        // A later queued write may already have changed Layout[index].
        // Display this entry's value without reverting logical terrain.
        if (Layout[index] == write.Tile) _positionVisualOverrides.Remove(index);
        else _positionVisualOverrides[index] = write.Tile;
        if (!changed) return;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ReplaceRoomTexture(activeHeaders);
    }

    internal void SetPositionTileAndCollision(
        Vector2 localPoint,
        byte tile,
        byte? collision,
        long animationTick,
        bool preserveRenderedTile = false)
    {
        if (WritePositionTileAndCollision(localPoint, tile, collision, preserveRenderedTile))
            Redraw(animationTick);
    }

    // Apply an ordered initialization batch before publishing its final image.
    // Ordinary gameplay writes and the native changed-tile queue retain their
    // individual publication boundaries. Even a partial, rejected batch must
    // leave the already-applied logical and displayed terrain consistent.
    internal void SetTilesAndCollisions(ReadOnlySpan<RoomTileWrite> writes, long animationTick)
    {
        bool redraw = false;
        try
        {
            foreach (RoomTileWrite write in writes)
                redraw |= WritePositionTileAndCollision(write.Position, write.Tile, write.Collision, false);
        }
        finally
        {
            if (redraw) Redraw(animationTick);
        }
    }

    private bool WritePositionTileAndCollision(Vector2 localPoint, byte tile, byte? collision,
        bool preserveRenderedTile)
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

        return redraw;
    }

    /// <summary>
    /// Mirrors setInterleavedTile: render one half-frame assembled from two
    /// metatile mappings. Callers that separately write wRoomLayout may also
    /// install tile1; simple-script graphics commands leave that byte intact.
    /// The existing collision byte is retained until the caller performs the
    /// final ordinary tile write.
    /// </summary>
    internal void SetInterleavedMetatile(
        Vector2 localPoint,
        byte tile1,
        byte tile2,
        int type,
        long animationTick,
        bool writeLayout = true)
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

        if (writeLayout) Layout[index] = tile1;
        _positionCollisionOverrides[index] = collisionBefore;
        _positionVisualOverrides.Remove(index);
        _positionMappingOverrides[index] = mapping;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _activeAnimationHeaders = activeHeaders;
        _animationSignature = GetAnimationSignature(activeHeaders);
        ReplaceRoomTexture(activeHeaders);
    }

    internal void ResetAndApplyRoomInitializationChanges(
        IReadOnlyDictionary<int, byte> changes,
        long animationTick)
    {
        TilesetFlags = _importedTilesetFlags;
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
        ReplaceRoomTexture(activeHeaders);
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
        ReplaceRoomTexture(activeHeaders);
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
        long animationTick,
        bool preserveBackgroundMappings = false)
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
            if (!preserveBackgroundMappings)
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

    /// <summary>
    /// Mirrors copyRectangleFromTmpGfxBuffer for an even-sized background
    /// tilemap rectangle. The imported tile IDs replace only the four tile
    /// bytes in each covered metatile; source attribute bytes remain intact.
    /// </summary>
    internal void SetBackgroundSubtileRectangle(
        Vector2 localTopLeft,
        int width,
        IReadOnlyList<byte> tileIds,
        long animationTick)
    {
        if (width <= 0 || tileIds.Count == 0 || tileIds.Count % width != 0)
        {
            throw new ArgumentException(
                "Invalid background-subtile rectangle.", nameof(tileIds));
        }
        int height = tileIds.Count / width;
        if ((width & 1) != 0 || (height & 1) != 0)
        {
            throw new ArgumentException(
                "Background-subtile rectangles must cover whole metatiles.",
                nameof(tileIds));
        }

        int pixelX = Mathf.RoundToInt(localTopLeft.X);
        int pixelY = Mathf.RoundToInt(localTopLeft.Y);
        if (pixelX % MetatileSize != 0 || pixelY % MetatileSize != 0)
            throw new ArgumentException(
                "Background-subtile rectangles must be metatile-aligned.",
                nameof(localTopLeft));
        int startX = pixelX / MetatileSize;
        int startY = pixelY / MetatileSize;
        int metatileWidth = width / 2;
        int metatileHeight = height / 2;
        if (startX < 0 || startY < 0 ||
            startX + metatileWidth > WidthInTiles ||
            startY + metatileHeight > HeightInTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(localTopLeft));
        }

        for (int metatileY = 0; metatileY < metatileHeight; metatileY++)
        for (int metatileX = 0; metatileX < metatileWidth; metatileX++)
        {
            int index =
                (startY + metatileY) * _layoutStride + startX + metatileX;
            int mappingOffset = Layout[index] * 8;
            byte[] mapping = _positionMappingOverrides.TryGetValue(
                    index, out byte[]? previous)
                ? (byte[])previous.Clone()
                : _mappings[mappingOffset..(mappingOffset + 8)];
            for (int quarterY = 0; quarterY < 2; quarterY++)
            for (int quarterX = 0; quarterX < 2; quarterX++)
            {
                int quarter = quarterY * 2 + quarterX;
                int sourceRow = metatileY * 2 + quarterY;
                int sourceColumn = metatileX * 2 + quarterX;
                mapping[quarter] =
                    tileIds[sourceRow * width + sourceColumn];
            }
            _positionMappingOverrides[index] = mapping;
            _positionVisualOverrides.Remove(index);
        }
        Redraw(animationTick);
    }

    /// <summary>drawRectangleToVramTiles writes tile/attribute pairs, including partial metatiles.</summary>
    internal void SetBackgroundMappingRectangle(
        Vector2I topLeft, int width, IReadOnlyList<byte> pairs, long animationTick)
    {
        if (width <= 0 || pairs.Count == 0 || pairs.Count % (width * 2) != 0)
            throw new ArgumentException("Invalid tile/attribute rectangle.", nameof(pairs));
        int height = pairs.Count / (width * 2);
        if (topLeft.X < 0 || topLeft.Y < 0 || topLeft.X % 8 != 0 || topLeft.Y % 8 != 0 ||
            topLeft.X + width * 8 > WidthInTiles * 16 || topLeft.Y + height * 8 > HeightInTiles * 16)
            throw new ArgumentOutOfRangeException(nameof(topLeft));
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int subtileX = topLeft.X / 8 + x;
            int subtileY = topLeft.Y / 8 + y;
            int index = subtileY / 2 * _layoutStride + subtileX / 2;
            int quarter = (subtileY & 1) * 2 + (subtileX & 1);
            if (!_positionMappingOverrides.TryGetValue(index, out byte[]? mapping))
            {
                int offset = Layout[index] * 8;
                mapping = _mappings[offset..(offset + 8)];
                _positionMappingOverrides[index] = mapping;
            }
            int source = (y * width + x) * 2;
            mapping[quarter] = pairs[source];
            mapping[quarter + 4] = pairs[source + 1];
            _positionVisualOverrides.Remove(index);
        }
        Redraw(animationTick);
    }

    // generateW3VramTilesAndAttributes rebuilds mappings from the current
    // layout without reloading layout, collision or shared underlying bytes.
    internal void RegenerateBackgroundMappings(long animationTick)
    {
        _positionMappingOverrides.Clear();
        _positionVisualOverrides.Clear();
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
        ReplaceRoomTexture(activeHeaders);
    }

    private static readonly byte[] SpecialCollisionMasks =
    {
        0x00, 0xc3, 0x03, 0xc0, 0x00, 0xc3, 0xc3, 0x00,
        0x00, 0xc3, 0x03, 0xc0, 0xc0, 0xc1, 0xff, 0x00
    };

    private static readonly byte[] EnemySpecialCollisionMasks =
    {
        0xff, 0xc3, 0x03, 0xc0, 0x00, 0xc3, 0xc3, 0x00,
        0x00, 0xc3, 0x03, 0xc0, 0xc1, 0xc1, 0xff, 0xff
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
            // hazardCollisionTable@sidescrolling; also used by bubble $91.
            3 when tile is >= 0x1a and <= 0x1f => HazardType.Water,
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

    private void ReplaceRoomTexture(int[] activeHeaders)
    {
        // Explicit VRAM writes must remain dirty even when the Image identity
        // and palette return to their previous values during preparation.
        if (_texturePublication?.TryDefer(this) == true)
        {
            _roomTextureDirty = true;
            return;
        }
        if (_texture is not null)
            ReplaceTextureImage(_texture, RenderRoom(activeHeaders));
        if (_underwaterBackgroundTexture is not null)
            ReplaceTextureImage(_underwaterBackgroundTexture, RenderRoom(activeHeaders, storageWidth: 256));
        RememberRoomTextureInputs();
    }

    private Image RenderRoom(int[] activeHeaders, int storageWidth = 0)
    {
        int outputWidth = storageWidth == 0 ? Width : storageWidth;
        byte[] output = storageWidth == 0
            ? _renderPixels ??= new byte[outputWidth * Height * 4]
            : _underwaterRenderPixels ??= new byte[outputWidth * Height * 4];
        byte[] colors = CapturePalettePixels();
        int tileCount = outputWidth / 16 * HeightInTiles * 4;
        RasterTile[] tiles = storageWidth == 0
            ? _renderTiles ??= new RasterTile[tileCount]
            : _underwaterRenderTiles ??= new RasterTile[tileCount];
        // Read each source once per redraw, including the mutable shared VRAM.
        // A persistent image cache here would miss transition/scripted writes.
        var sources = _renderSources;
        sources.Clear();

        for (int roomY = 0; roomY < HeightInTiles; roomY++)
        for (int roomX = 0; roomX < outputWidth / 16; roomX++)
        {
            int layoutIndex = roomY * _layoutStride + roomX;
            bool playable = roomX < WidthInTiles;
            byte[]? mappingOverride = playable && _positionMappingOverrides.TryGetValue(
                layoutIndex, out byte[]? value) ? value : null;
            // bank0.s:loadRoomLayout clears the 16-byte storage rows first;
            // loadTilesToRam.s renders all 16 columns, including padding.
            // SCX wraps at 256 pixels, not at the playable room width.
            int metatile = playable ? GetRenderedMetatile(layoutIndex)
                : _layoutStride == 16 ? Layout[layoutIndex] : 0;
            int mappingOffset = metatile * 8;

            for (int quarter = 0; quarter < 4; quarter++)
            {
                byte tileId = mappingOverride is null
                    ? _mappings[mappingOffset + quarter]
                    : mappingOverride[quarter];
                byte attributes = mappingOverride is null
                    ? _mappings[mappingOffset + 4 + quarter]
                    : mappingOverride[4 + quarter];
                int sourceIndex = tileId >= 0x80 ? tileId - 0x80 : tileId + 0x80;
                ResolveBackgroundGraphics(activeHeaders, sourceIndex, out Image tileSource, out int sourceTile);
                if (!sources.TryGetValue(tileSource, out var sourcePixels))
                {
                    OracleImagePixels pixels = OracleGraphicsCache.GetImagePixels(tileSource);
                    sourcePixels = (pixels, pixels.RgbaPixels ?? ReadRgbaPixels(tileSource), pixels.Width);
                    sources.Add(tileSource, sourcePixels);
                }
                int rasterIndex = (roomY * (outputWidth / 16) + roomX) * 4 + quarter;
                var rasterTile = new RasterTile(sourcePixels.Snapshot, sourceTile, attributes,
                    _paletteRevisions[attributes & 7]);
                // Immutable art keeps its snapshot identity. Mutable VRAM gets
                // a fresh snapshot each redraw, preserving in-place uploads.
                if (tiles[rasterIndex] == rasterTile) continue;
                tiles[rasterIndex] = rasterTile;
                int sourceColumns = sourcePixels.Width / 8;
                int sourceX = (sourceTile % sourceColumns) * 8;
                int sourceY = (sourceTile / sourceColumns) * 8;
                bool flipX = (attributes & 0x20) != 0;
                bool flipY = (attributes & 0x40) != 0;
                int rawPalette = attributes & 0x07;
                int quarterX = quarter % 2;
                int quarterY = quarter / 2;

                for (int pixelY = 0; pixelY < 8; pixelY++)
                for (int pixelX = 0; pixelX < 8; pixelX++)
                {
                    int readX = sourceX + (flipX ? 7 - pixelX : pixelX);
                    int readY = sourceY + (flipY ? 7 - pixelY : pixelY);
                    int red = sourcePixels.Pixels[(readY * sourcePixels.Width + readX) * 4];
                    int shade = (255 - red + 42) / 85;
                    int writeX = roomX * 16 + quarterX * 8 + pixelX;
                    int writeY = roomY * 16 + quarterY * 8 + pixelY;
                    colors.AsSpan((rawPalette * 4 + shade) * 4, 4)
                        .CopyTo(output.AsSpan((writeY * outputWidth + writeX) * 4, 4));
                }
            }
        }

        sources.Clear();
        return Image.CreateFromData(outputWidth, Height, false, Image.Format.Rgba8, output);
    }

    internal Image RenderUnderwaterBackground() =>
        RenderRoom(_activeAnimationHeaders, storageWidth: 256);

    private byte[] CapturePalettePixels()
    {
        bool changed = !_paletteCaptured;
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            changed |= _capturedPalette[palette, shade] != _backgroundPalettes.Resolve(palette, shade);
        if (!changed) return _palettePixels;
        // Let Godot quantize the 32 colors exactly as Image.SetPixel did.
        // Full-room composition then stays in managed memory instead of
        // making two native calls for every displayed pixel.
        using Image colors = Image.CreateEmpty(4, 8, false, Image.Format.Rgba8);
        for (int palette = 0; palette < 8; palette++)
        {
            bool paletteChanged = !_paletteCaptured;
            for (int shade = 0; shade < 4; shade++)
            {
                Color color = _backgroundPalettes.Resolve(palette, shade);
                paletteChanged |= _capturedPalette[palette, shade] != color;
                colors.SetPixel(shade, palette, color);
                _capturedPalette[palette, shade] = color;
            }
            if (paletteChanged) _paletteRevisions[palette]++;
        }
        colors.GetData().CopyTo(_palettePixels, 0);
        _paletteCaptured = true;
        return _palettePixels;
    }

    private static byte[] ReadRgbaPixels(Image source)
    {
        if (source.GetFormat() == Image.Format.Rgba8) return source.GetData();
        using Image converted = (Image)source.Duplicate();
        converted.Convert(Image.Format.Rgba8);
        return converted.GetData();
    }

    internal void LoadTilesetPalette(bool resetLiveGraphics = false)
    {
        _temporaryBackgroundPaletteActive = false;
        _temporaryFullBackgroundPaletteBlend = 0.0f;
        _temporaryBackgroundPaletteOffset = null;
        if (resetLiveGraphics)
        {
            _liveGraphics = null;
            _freezeGraphicsOverrides = false;
        }
        _backgroundPalettes.LoadTileset(_tilesetPalette);
        RefreshPaletteTextures();
    }

    internal void SetLiveGraphics(Image? graphics, bool freezeOverrides)
    {
        _liveGraphics = graphics;
        _freezeGraphicsOverrides = freezeOverrides;
        RedrawForPaletteChange();
    }

    // Capture the actual frozen tile bytes, including animation and scripted
    // uploads already present when scrolling starts. Never mutate cached art.
    internal Image CaptureLiveGraphics()
    {
        Image output = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
        for (int tile = 0; tile < 256; tile++)
        {
            ResolveBackgroundGraphics(_activeAnimationHeaders, tile, out Image source, out int sourceTile);
            int columns = source.GetWidth() / 8;
            output.BlitRect(source, new Rect2I(sourceTile % columns * 8, sourceTile / columns * 8, 8, 8),
                new Vector2I(tile % 16 * 8, tile / 16 * 8));
        }
        return output;
    }

    private void ResolveBackgroundGraphics(int[] headers, int tile, out Image source, out int sourceTile)
    {
        source = _liveGraphics ?? _source;
        sourceTile = tile;
        if (_freezeGraphicsOverrides) return;
        if (_dynamicBackgroundTiles.TryGetValue(tile, out DynamicBackgroundTile dynamicTile))
        {
            source = dynamicTile.Source;
            sourceTile = dynamicTile.Tile;
        }
        else if (_animations.TryGetOverride(headers, tile, out Image animation, out int animationTile))
        {
            source = animation;
            sourceTile = animationTile;
        }
    }

    internal void RedrawForPaletteChange()
    {
        // Explicit graphics publications (including writes into the shared
        // scrolling VRAM Image) must redraw even when its identity is stable.
        ReplaceRoomTexture(_activeAnimationHeaders);
        RefreshClearedTilemap();
    }

    internal void RefreshPaletteTextures()
    {
        if (_texturePublication?.TryDefer(this) == true) return;
        if (_texture is null && _underwaterBackgroundTexture is null)
        {
            RefreshClearedTilemap();
            return;
        }
        int changedPalettes = 0;
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            if (_renderedBackgroundPalettes[palette, shade] != _backgroundPalettes.Resolve(palette, shade))
                changedPalettes |= 1 << palette;
        bool graphicsChanged = _roomTextureDirty || !ReferenceEquals(_renderedLiveGraphics, _liveGraphics) ||
            _renderedFreezeGraphicsOverrides != _freezeGraphicsOverrides ||
            _renderedAnimationSignature != _animationSignature;
        if (graphicsChanged || UsesBackgroundPalettes(changedPalettes))
            ReplaceRoomTexture(_activeAnimationHeaders);
        else if (changedPalettes != 0)
            RememberRoomTextureInputs();
        RefreshClearedTilemap();
    }

    private bool UsesBackgroundPalettes(int palettes)
    {
        if (palettes == 0) return false;
        int columns = _underwaterBackgroundTexture is null ? WidthInTiles : 16;
        for (int y = 0; y < HeightInTiles; y++)
        for (int x = 0; x < columns; x++)
        {
            int index = y * _layoutStride + x;
            bool playable = x < WidthInTiles;
            byte[]? mapping = playable && _positionMappingOverrides.TryGetValue(index, out var value) ? value : null;
            int metatile = playable ? GetRenderedMetatile(index) : _layoutStride == 16 ? Layout[index] : 0;
            for (int quarter = 0; quarter < 4; quarter++)
            {
                int attributes = mapping is null ? _mappings[metatile * 8 + 4 + quarter] : mapping[4 + quarter];
                if ((palettes & (1 << (attributes & 7))) != 0) return true;
            }
        }
        return false;
    }

    private void RememberRoomTextureInputs()
    {
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
            _renderedBackgroundPalettes[palette, shade] = _backgroundPalettes.Resolve(palette, shade);
        _renderedLiveGraphics = _liveGraphics;
        _renderedFreezeGraphicsOverrides = _freezeGraphicsOverrides;
        _renderedAnimationSignature = _animationSignature;
        _roomTextureDirty = false;
    }

    private void RefreshClearedTilemap()
    {
        if (_clearedTilemapTexture is null) return;
        if (_texturePublication?.TryDefer(this) == true) return;
        // The clear map repeats immutable gfx_hud tile $00 with BG palette 0.
        // Scrolling VRAM uploads and writes to BG1-7 cannot change its pixels.
        _clearTile.Refresh();
    }

    private byte GetRenderedMetatile(int layoutIndex) =>
        _positionVisualOverrides.TryGetValue(layoutIndex, out byte visualOverride)
            ? visualOverride
            : Layout[layoutIndex];

    private static void ReplaceTextureImage(Texture2D texture, Image image)
    {
        // Every room mutation produces a complete CPU-composited image. A
        // fresh backing upload keeps consecutive palette and animated-tile
        // redraws from retaining stale GPU regions in the embedded renderer.
        using (image) ((ImageTexture)texture).SetImage(image);
    }

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

public readonly record struct TerrainInfo(byte Tile, byte Collision, TerrainType Type, HazardType Hazard);

public enum HazardType
{
    None,
    Water,
    Hole,
    Lava
}

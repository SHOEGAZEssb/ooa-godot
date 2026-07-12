using Godot;
using System;

namespace oracleofages;

public sealed class OracleRoomData
{
    public const int ViewportWidth = 160;
    public const int ViewportHeight = 128;
    public const int MetatileSize = 16;

    public int Group { get; }
    public int Id { get; }
    public int TilesetId { get; }
    public int AnimationGroup { get; }
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
    private readonly byte[] _mappings;
    private readonly Color[,] _palette;
    private readonly OracleAnimationData _animations;
    private int _animationSignature;

    internal OracleRoomData(
        int group,
        int id,
        int tilesetId,
        int animationGroup,
        byte[] layout,
        byte[] collisions,
        Image source,
        byte[] mappings,
        Color[,] palette,
        OracleAnimationData animations)
    {
        Group = group;
        Id = id;
        TilesetId = tilesetId;
        AnimationGroup = animationGroup;
        Layout = layout;
        Collisions = collisions;
        _source = source;
        _mappings = mappings;
        _palette = palette;
        _animations = animations;

        (WidthInTiles, HeightInTiles) = layout.Length switch
        {
            80 => (10, 8),
            176 => (16, 11),
            _ => throw new InvalidOperationException(
                $"Room {group:x1}:{id:x2} has unsupported layout size {layout.Length}.")
        };
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, 0);
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
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
        return true;
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

    public bool IsSolid(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return false;

        byte metatile = Layout[tileY * WidthInTiles + tileX];
        byte collision = Collisions[metatile];
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
        return Layout[tileY * WidthInTiles + tileX];
    }

    public TerrainInfo GetTerrainInfo(Vector2 localPoint)
    {
        byte metatile = GetMetatile(localPoint);
        if (metatile == 0xff)
            return new TerrainInfo(metatile, 0xff, TerrainType.Normal, HazardType.None);

        byte collision = Collisions[metatile];
        return new TerrainInfo(
            metatile,
            collision,
            GetTerrainType(Group, metatile),
            GetHazardType(Group, metatile));
    }

    public int GetPackedPosition(Vector2 localPoint)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        return (tileY << 4) | tileX;
    }

    public bool ReplaceMetatile(Vector2 localPoint, byte expected, byte replacement, long animationTick)
    {
        int tileX = Mathf.FloorToInt(localPoint.X / MetatileSize);
        int tileY = Mathf.FloorToInt(localPoint.Y / MetatileSize);
        if (tileX < 0 || tileX >= WidthInTiles || tileY < 0 || tileY >= HeightInTiles)
            return false;

        int index = tileY * WidthInTiles + tileX;
        if (Layout[index] != expected)
            return false;

        Layout[index] = replacement;
        int[] activeHeaders = _animations.GetActiveHeaders(AnimationGroup, animationTick);
        _animationSignature = GetAnimationSignature(activeHeaders);
        ((ImageTexture)Texture).Update(RenderRoom(activeHeaders));
        return true;
    }

    private static readonly byte[] SpecialCollisionMasks =
    {
        0x00, 0xc3, 0x03, 0xc0, 0x00, 0xc3, 0xc3, 0x00,
        0x00, 0xc3, 0x03, 0xc0, 0xc0, 0xc1, 0xff, 0x00
    };

    private static TerrainType GetTerrainType(int group, byte tile)
    {
        return GetCollisionMode(group) switch
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

    private static HazardType GetHazardType(int group, byte tile)
    {
        return GetCollisionMode(group) switch
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

    private static int GetCollisionMode(int group)
    {
        return group switch
        {
            0 or 1 => 0,
            2 or 3 => 1,
            4 or 5 => 2,
            _ => 0
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
            int metatile = Layout[roomY * WidthInTiles + roomX];
            int mappingOffset = metatile * 8;

            for (int quarter = 0; quarter < 4; quarter++)
            {
                byte tileId = _mappings[mappingOffset + quarter];
                byte attributes = _mappings[mappingOffset + 4 + quarter];
                int sourceIndex = tileId >= 0x80 ? tileId - 0x80 : tileId + 0x80;
                Image tileSource = _source;
                int sourceTile = sourceIndex;
                if (_animations.TryGetOverride(
                    activeHeaders, sourceIndex, out Image overrideSource, out int overrideTile))
                {
                    tileSource = overrideSource;
                    sourceTile = overrideTile;
                }
                int sourceX = (sourceTile % 16) * 8;
                int sourceY = (sourceTile / 16) * 8;
                bool flipX = (attributes & 0x20) != 0;
                bool flipY = (attributes & 0x40) != 0;
                int paletteIndex = Mathf.Clamp((attributes & 0x07) - 2, 0, 5);
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
                    output.SetPixel(writeX, writeY, _palette[paletteIndex, shade]);
                }
            }
        }

        return output;
    }

    private static int GetAnimationSignature(int[] headers)
    {
        int signature = 17;
        foreach (int header in headers)
            signature = signature * 31 + header;
        return signature;
    }
}

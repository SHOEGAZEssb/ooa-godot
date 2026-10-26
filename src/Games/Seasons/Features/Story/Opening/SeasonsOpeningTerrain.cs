using System;

namespace Oracles;

internal static class SeasonsOpeningTerrain
{
    // data/seasons/tile_properties/{tileTypeMappings,hazards}.s:@overworld.
    internal static TerrainType Type(int collisions, byte tile)
    {
        if (collisions != 0) throw new NotSupportedException($"Seasons opening: collision mode ${collisions:x2} is outside its overworld rooms.");
        return tile switch
        {
            0x20 => TerrainType.Stump, 0xf3 or 0xf4 => TerrainType.Hole,
            0xdd or 0xde or 0xdf => TerrainType.Vines, 0xf8 or 0xf9 => TerrainType.Grass,
            0xd0 => TerrainType.Stairs, 0xdb => TerrainType.CrackedFloor, 0xdc => TerrainType.Ice,
            0xfd => TerrainType.Water, 0xfa or 0xfb or 0xfc => TerrainType.Puddle,
            0xd1 => TerrainType.UpCurrent, 0xd4 => TerrainType.RightCurrent,
            0xd2 => TerrainType.DownCurrent, 0xd3 => TerrainType.LeftCurrent,
            0xc9 => TerrainType.CrackedFloor, >= 0x7b and <= 0x7f => TerrainType.Lava,
            _ => TerrainType.Normal
        };
    }

    internal static HazardType Hazard(int collisions, byte tile) => tile switch
    {
        0xf3 => HazardType.Hole,
        0xfd or 0xfe or 0xff or >= 0xd1 and <= 0xd4 => HazardType.Water,
        >= 0x7b and <= 0x7f => HazardType.Lava,
        _ => HazardType.None
    };
}

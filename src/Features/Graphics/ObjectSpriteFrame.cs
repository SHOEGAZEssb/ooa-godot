namespace Oracles;

public readonly record struct ObjectSpriteFrame(int Duration, int SourceOffset, int BasePalette, ObjectSpritePart[] Parts);

public readonly record struct ObjectSpritePart(int Y, int X, int Tile, int Flags);

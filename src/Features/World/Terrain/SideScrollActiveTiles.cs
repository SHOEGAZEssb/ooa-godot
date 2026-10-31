namespace Oracles;

// The three bytes published by sidescrollUpdateActiveTile before item parents.
public readonly record struct SideScrollActiveTiles(
    byte ActiveTile,
    SideScrollTileType ActiveType,
    SideScrollTileType BelowType)
{
    internal SideScrollTileType CombinedType => ActiveType | BelowType;
}

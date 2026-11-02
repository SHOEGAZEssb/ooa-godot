namespace Oracles;

internal sealed partial class TokaySeedlingDecorationRoomEntity : AnimatedDecorationRoomEntity
{
    internal TokaySeedlingPlotRecord Record { get; }
    internal TokaySeedlingDecorationRoomEntity(TokaySeedlingPlotRecord record)
        : base(AgesGameContent.AssetRoot, "TokayScentSeedling", new(record.X, record.Y),
            record.Sprite, record.Animation, record.TileBase, record.Palette) => Record = record;
}

internal sealed record TokaySeedlingDecorationSpawn(TokaySeedlingPlotRecord Record)
    : RoomEntitySpawn(UpdateThisFrame: true);

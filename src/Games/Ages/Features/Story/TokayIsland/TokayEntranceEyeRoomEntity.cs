
namespace Oracles;

internal sealed partial class TokayEntranceEyeRoomEntity : AnimatedDecorationRoomEntity
{
    internal TokayEntranceEyeRecord Record { get; }
    internal TokayEntranceEyeRoomEntity(TokayEntranceEyeRecord record)
        : base(AgesGameContent.AssetRoot, $"TokayEntranceEye_{record.SubId:x2}", new(record.X, record.Y),
            record.Sprite, record.Animation, record.TileBase, record.Palette, new Vector2(-16, -16)) => Record = record;
}

internal sealed record TokayEntranceEyeSpawn(TokayEntranceEyeRecord Record)
    : RoomEntitySpawn;

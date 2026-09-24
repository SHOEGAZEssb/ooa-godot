using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_DUNGEON_STUFF $12:$04 never increments state. It remains eligible
// under text/disabled-object masks, but returnIfScrollMode01Unset holds it
// during scrolling. Its room flag causes deletion on the following update.
internal sealed partial class EnemyClearStairsRoomEntity(
    DungeonMechanicDatabaseRecord record, OracleRoomData room,
    DungeonMechanicDatabase data, OracleSaveData save, Func<int> enemyCount,
    Func<long> animationTick, Action<int> playSound, Func<Vector2, bool> tryPuff) :
    Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (save.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80))
        {
            Finished = true;
            return;
        }
        if (enemyCount() != 0) return;
        playSound(data.SolveSound);
        save.SetRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80);
        // Original decrements C from $af until zero, excluding position$00.
        for (int packed = 0xaf; packed > 0; packed--)
        {
            Vector2 position = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            int tile = room.GetMetatile(position);
            if (tile is < 0x40 or > 0x43) continue;
            room.SetPositionTileAndCollision(position,
                (byte)data.EnemyStairTile(tile), null, animationTick());
            tryPuff(position);
        }
    }
}

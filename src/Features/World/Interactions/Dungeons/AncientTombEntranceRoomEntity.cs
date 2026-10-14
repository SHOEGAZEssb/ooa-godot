using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscellaneous2.s:interactiondc_subid0E. The placed slot watches the grave
// after reserved INTERAC_PUSHBLOCK $14 has published its stationary tile.
internal sealed partial class AncientTombEntranceRoomEntity(
    DungeonMechanicDatabaseRecord record, OracleRoomData room, OracleSaveData save,
    OracleRuntimeState runtime, DungeonMechanicDatabase data, Action<byte, byte> setTile,
    Func<Vector2, bool> createPuff, Action<int> sound)
    : DungeonMechanicRoomEntity(record.PackedPosition, "AncientTombEntrance"), IFixedRoomEntity,
      IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    private void Initialize()
    {
        Finished = save.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80);
        State = 1;
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) Initialize();
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        if (State == 1)
        {
            // checkLinkVulnerable includes menu/collision/spinner gates and
            // var2a/invincibility/knockback, but no ground-height requirement.
            if (room.GetMetatile(Position) != data.AncientTombGraveTile ||
                frame.Player.NativeContactSignal || !frame.Player.NativePuzzleResetVulnerable) return;
            runtime.SetWramByte(WramAddress.wDisabledObjects, 0x81);
            runtime.SetWramByte(WramAddress.wMenuDisabled, 0x81);
            Counter = data.AncientTombSolveWait;
            State = 2;
            IReadOnlyList<DungeonTilePatternRecord> tiles = data.TilePattern(record.Id, record.SubId);
            // All four setTile calls precede every allocation. Each failed
            // getFreeInteractionSlot skips only that puff, without retrying.
            foreach (var tile in tiles)
                setTile(checked((byte)tile.PackedPosition), checked((byte)tile.Tile));
            foreach (var tile in tiles)
                createPuff(new Vector2((tile.PackedPosition & 15) * 16 + 8,
                    (tile.PackedPosition >> 4) * 16 + 8));
            return;
        }
        if (--Counter != 0) return;
        sound(data.SolveSound);
        save.SetRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlag80);
        ReleaseInput();
        Finished = true;
    }

    internal void Cancel()
    {
        if (State == 2 && !Finished) ReleaseInput();
        Finished = true;
    }

    private void ReleaseInput()
    {
        runtime.SetWramByte(WramAddress.wDisabledObjects, 0);
        runtime.SetWramByte(WramAddress.wMenuDisabled, 0);
    }
}

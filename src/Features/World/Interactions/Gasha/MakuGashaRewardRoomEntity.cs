using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Room-load INTERAC $90:$1b-$1d. The helper occupies its slot even if the
// essence/item gate fails; createTreasure failure also deletes it permanently.
internal sealed partial class MakuGashaRewardRoomEntity(MakuGashaRewardRecord record,
    OracleSaveData save, InventoryState inventory, Action<GroundTreasureGrantRequest> createTreasure) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Dispatch()
    {
        if (Finished) return;
        if (inventory.HasTreasure(TreasureId.Essence) &&
            (save.ReadWramByte(AgesWramAddress.wEssencesObtained) & record.EssenceMask) != 0)
        {
            Position = new((record.Position & 15) * 16 + 8, (record.Position >> 4) * 16 + 8);
            if (!save.HasRoomFlag(record.Group, record.Room, OracleSaveData.RoomFlagItem))
                createTreasure(new(record.Group, record.Room, 0, (int)Position.Y, (int)Position.X,
                    "TREASURE_OBJECT_GASHA_SEED_07", record.Source) {
                        SpawnMode = TreasureSpawnMode.Instant, GrabMode = TreasureGrabMode.TwoHands,
                        ExpectedTreasureId = TreasureId.GashaSeed, ExpectedSubId = 0x07, ExpectedObjectParameter = 1 });
        }
        Finished = true;
    }
}

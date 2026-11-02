
namespace Oracles;

// code/interactableTiles.s:nextToSubrosiaKeydoor uses Rosa's live date flag.
internal sealed class SubrosiaKeyDoorController(RoomSession rooms,RoomEntityManager entities,OracleSoundEngine sound)
{
    private readonly int _datingRosa = new GlobalFlagDatabase(rooms.AssetRoot).GetId("GLOBALFLAG_DATING_ROSA");

    internal bool UpdatePushAttempt(Vector2 position,Vector2I facing,Vector2 input)
    {
        Vector2 front=position+InteractableTilePushGeometry.FrontTileOffset(facing);
        byte tile=rooms.CurrentRoom.GetMetatile(front);
        if(tile!=0xec)
            throw new NotSupportedException($"interactableTiles.s:nextToSubrosiaKeydoor has no native key graphic for tile${tile:x2}.");
        if(!InteractableTilePushGeometry.TryGetCardinalInput(input,out _) ||
            !InteractableTilePushGeometry.IsAlignedForPush(position))
        {
            rooms.TilePushCounter=20;
            return false;
        }
        if(facing!=Vector2I.Up)return false;
        int counter=rooms.TilePushCounter;
        bool expired=PushingAgainstTileCounter.DecrementTwiceToZero(ref counter);
        rooms.TilePushCounter=(byte)counter;
        if(!expired)return false;
        if(!rooms.SaveData.HasGlobalFlag(_datingRosa))
        {
            entities.RequiredReservedPushBlock.RequestTileInfo(0x5109);
            return entities.TextActiveSource();
        }
        sound.PlaySound(SoundId.SndOpenChest);
        rooms.SaveData.SetRoomFlag(rooms.ActiveGroup,rooms.CurrentRoom.Id,0x40);
        byte packed=(byte)rooms.CurrentRoom.GetPackedPosition(front);
        rooms.TrySetTile(packed,0xe8); // TILEINDEX_OPEN_CAVE_DOOR; no key debit or input mask.
        if(entities.InteractionSlotAvailable)
            entities.Spawn<DungeonKeyUseEffect>(new DungeonKeyUseSpawn(
                new((packed&15)*16+8,(packed>>4)*16+8),entities.Treasures.GetObjectVisual(0x42)));
        return true;
    }
}


namespace Oracles;

public sealed partial class RoomEntityManager
{
    internal IEnumerable<IRoomEntity> CreateNativeObjectStream(int group, OracleRoomData room, int entry,
        EnemyPlacementContext placement)
    {
        var data=NativeObjectStreamDatabase.For(AssetRoot);
        var reader=new NativeObjectStreamReader(data,_runtimeState,()=>_saveData?.GetRoomFlags(group,room.Id) ??
            throw new InvalidOperationException($"Room ${group:x2}:${room.Id:x2}: native object pointers require the save owner."));
        foreach (var instruction in reader.Read(entry))
        {
            if (instruction.Opcode is 1 or 2 || instruction.Opcode==9 && instruction.Flags==0)
            {
                if (!InteractionSlotAvailable) { reader.AllocationFailed(); continue; }
                if (_factory.CreateStreamInteraction(instruction,room,placement) is { } interaction) yield return interaction;
                continue;
            }
            RoomObjectKind kind=instruction.Opcode switch
            {
                6=>RoomObjectKind.RandomEnemy,7=>RoomObjectKind.FixedEnemy,
                9 when instruction.Flags==1=>RoomObjectKind.ParameterEnemy,
                8=>RoomObjectKind.ReservingPart,9 when instruction.Flags==2=>RoomObjectKind.ParameterPart,
                10=>RoomObjectKind.ItemDrop,
                _=>throw new NotSupportedException($"{instruction.Source}: opcode ${instruction.Opcode:x2} has no native construction binding.")
            };
            int count=kind==RoomObjectKind.RandomEnemy ? (instruction.Flags>>5)&7 : 1;
            if (count==0) count=256; // Op$06 decrements a byte after its first allocation.
            int packed=instruction.Opcode is 8 or 10 ? instruction.Position : (instruction.Y&0xf0)|(instruction.X>>4);
            var source=new RoomObjectRecord(group,room.Id,0,kind,instruction.Opcode==10 ? EnemyId.ItemDropProducer : instruction.Id,
                instruction.Opcode==10 ? instruction.Id : instruction.SubId,
                instruction.Opcode==9 ? 0 : instruction.Flags&0x1f,count,instruction.Y,instruction.X,packed,0xff)
                {Var03=instruction.Var03,SourceOverride=instruction.Source};
            foreach (var entity in PlaceRoomObject(source,room,placement,onAllocationFailure:reader.AllocationFailed)) yield return entity;
        }
    }

    internal void ParseNativeObjectStream(string label,EnemyPlacementContext placement)
    {
        var room=_roomForActiveEntities;
        foreach (var entity in CreateNativeObjectStream(room.Group,room,NativeObjectStreamDatabase.For(AssetRoot).Entry(label),placement))
            AddEntity(entity);
    }
}

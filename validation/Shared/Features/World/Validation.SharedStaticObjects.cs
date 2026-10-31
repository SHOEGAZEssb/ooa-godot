using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedStaticObjects(FrontendValidationRoot root)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var memory = rooms.RuntimeState;
        var data = StaticObjectState.Data(memory);
        var room = rooms.CurrentRoom;
        byte[] before = Enumerable.Range(0, data.Slots * 8).Select(offset => memory.ReadWramByte(data.Start + offset)).ToArray();
        void FillInteractions(int group, OracleRoomData target)
        {
            while (entities.InteractionSlotAvailable)
            {
                var actor = new StationaryDebrisEffect();
                actor.Initialize(target.AssetRoot, new(24,24), SoundId.MusNone);
                entities.AddEntity(new InteractionSlotValidationEntity(actor, _ => { }));
            }
        }
        try
        {
            // Room parsing does not load a dungeon list or repair a consumed buffer.
            StaticObjectState.Clear(memory);
            entities.LoadRoom(rooms.ActiveGroup, room);
            FailIf(Enumerable.Range(0, data.Slots * 8).Any(offset => memory.ReadWramByte(data.Start + offset) != 0),
                $"{memory.Game}: ordinary static parsing must preserve an empty buffer.");
            // A full native pool skips construction; free capacity diagnoses unbound content.
            StaticObjectState.Write(memory, new(data.Slots - 2, 3, room.Id, 0x7e, 0x5a, 24, 40, "static constructor fixture"));
            entities.RoomInitializing += FillInteractions;
            try { entities.LoadRoom(rooms.ActiveGroup, room); }
            finally { entities.RoomInitializing -= FillInteractions; }
            FailIf(entities.InteractionSlotAvailable || memory.ReadWramByte(data.Start + (data.Slots - 2) * 8) != 3,
                $"{memory.Game}: native static allocation failure must preserve its record, including upper Seasons slots.");
            bool rejected = false;
            try { entities.LoadRoom(rooms.ActiveGroup, room); }
            catch (NotSupportedException error)
            {
                rejected = error.Message.Contains("staticObjects.parseStaticObjects") && error.Message.Contains("$7e:$5a");
            }
            FailIf(!rejected, $"{memory.Game}: admitted unsupported static content needs its source, ID and subid diagnostic.");
        }
        finally
        {
            for (int offset = 0; offset < before.Length; offset++) memory.SetWramByte(data.Start + offset, before[offset]);
            entities.LoadRoom(rooms.ActiveGroup, room);
        }
    }
}

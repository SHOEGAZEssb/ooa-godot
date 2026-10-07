using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockQueue()
    {
        int fixture = 0;
        foreach (int queued in new[] { 0,29,30,31 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x77,0);
            FailIf(_currentRoom.Layout[0x37] != 0x2a || _currentRoom.Layout[0x57] != 0x2a,
                "Source room049e.bin must retain its original statues$37/$57.");
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool push = false) => StepSomariaMotionRom(rom,count,batch,push ? 0 : 0xff,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,
                    $"Source queue queued={queued}, batch={batch}, update={++update}");
                FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    "Synchronized source writes and native four-entry graphics draining must preserve queue count.");
            });
            for (int wait = 0; _pushBlocks.RemainingPushFrames != 1 && wait < 80; wait++) Step(push:true);
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "Actual floor approach must stop one native contact update before the push.");
            for (int index = 0; index < queued; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,0xa0),"Source queue fixture fill must fit.");
                rom.SetTile(0x11,0xa0);
            }
            Step(push:true);
            // setTile rejects before CF/CE/underlying writes. If reserved$d1
            // leaves$57 intact, the descending$bd scan allocates it again.
            int firstCount = queued == 31 ? 2 : 1;
            var first = _entities.Entities<PushBlockController>().OrderBy(_entities.InteractionSlot).ToArray();
            FailIf(!_pushBlocks.Active || first.Length != firstCount ||
                _currentRoom.Layout[0x57] != (queued == 31 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x37] != (queued >= 30 ? 0x2a : 0xa0),
                "Native queue capacity must determine source floor writes before the synchronizer scan.");
            for (int index = 0; index < first.Length; index++)
                FailIf(first[index].BlockTopLeft != new Vector2(112,(queued == 31 && index == 0 ? 80 : 48)-0.5f),
                    "Rejected source tiles must allocate descending duplicates and move in that same object pass.");
            Step();
            FailIf(_entities.Entities<PushBlockController>().Count != firstCount,
                "Original synchronizer state2 must return to state1 before rescanning.");
            Step();
            int delayed = queued == 31 ? 2 : queued == 30 ? 1 : 0;
            FailIf(_entities.Entities<PushBlockController>().Count != firstCount+delayed ||
                _currentRoom.Layout[0x57] != 0xa0 || _currentRoom.Layout[0x37] != 0xa0 ||
                sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1+firstCount+delayed,
                "Next state1 scan must allocate duplicates for rejected source writes, clear them and play ordered movement cues.");
            Step(29);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != delayed ||
                _currentRoom.Layout[0x47] != 0x2a || _currentRoom.Layout[0x27] != 0x2a,
                "Initial blocks must complete on update32 while later duplicates retain their counters.");
            Step();
            FailIf(_entities.Entities<PushBlockController>().Count != delayed,"Duplicate movement must retain its update31 boundary.");
            Step(); Step(3);
            FailIf(_entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.Layout[0x47] != 0x2a || _currentRoom.Layout[0x27] != 0x2a,
                "Duplicates must complete on global update34 and remain retired after graphics draining.");
        }
        fixture = 0;
        foreach (int free in new[] { 0,1,2 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x77,0);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,bool push = false) => StepSomariaMotionRom(rom,count,batch,push ? 0 : 0xff,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,
                    $"Destination queue free={free}, batch={batch}, update={++update}");
                FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    "Ordered reserved/dynamic destination writes must preserve the original queue count.");
            });
            for (int wait = 0; !_pushBlocks.Active && wait < 80; wait++) Step(push:true);
            FailIf(!_pushBlocks.Active,"Destination queue comparison must start an actual statue push.");
            Step(30);
            FailIf(!_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 1,
                "Both source actors must remain active through movement update31.");
            for (int index = 0; index < 31-free; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,0xa0),"Destination queue fixture fill must fit.");
                rom.SetTile(0x11,0xa0);
            }
            Step();
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != 0 ||
                _currentRoom.Layout[0x47] != (free >= 1 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x27] != (free == 2 ? 0x2a : 0xa0),
                "Reserved$d1 must spend the first free queue entry; rejected final writes still delete both actors.");
            Step(8);
            FailIf(_currentRoom.Layout[0x47] != (free >= 1 ? 0x2a : 0xa0) ||
                _currentRoom.Layout[0x27] != (free == 2 ? 0x2a : 0xa0),
                "Native graphics draining must not retry rejected destination writes.");
        }
    }
}

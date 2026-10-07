using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockAllocation()
    {
        int fixture = 0;
        foreach (int free in new[] { 0,1 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9b,0x25,2);
            FailIf(new[] { 0x35,0x43,0x56 }.Any(position => _currentRoom.Layout[position] != 0x2e),
                "Source room049b.bin must retain its three blue blocks$35/$43/$56.");
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            var fillers = new List<(PuzzlePuffEffect Effect,int Slot)>();
            void Step(int count = 1,bool push = false) => StepSomariaMotionRom(rom,count,batch,push ? 16 : 0xff,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                string context = $"Synchronized capacity free={free}, batch={batch}, update={++update}";
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,context);
                foreach (var (puff,slot) in fillers)
                {
                    bool alive = rom[slot] != 0 && rom[slot+1] == 5;
                    FailIf(puff.Finished == alive,context+": original filler puff lifetime/slot reuse differs.");
                    if (!alive) continue;
                    FailIf(_entities.InteractionSlot(puff) != (slot>>8)-0xd0 ||
                        puff.Initialized != (rom[slot+4] != 0) || puff.CurrentParameter != rom[slot+0x21] ||
                        SomariaPrivate<int>(puff,"_animationCounter") != rom[slot+0x20] ||
                        puff.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                        puff.Visible != ((rom[slot+0x1a]&0x80) != 0),context+": original physical puff slot/initialization/animation/XY/visibility differs.");
                }
                FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    context+": original push writes/graphics queue count differs.");
            });
            Step(); // Publish this room's wall probes/reset contact clock before held input.
            for (int wait = 0; _pushBlocks.RemainingPushFrames != 1 && wait < 80; wait++) Step(push:true);
            FailIf(_pushBlocks.RemainingPushFrames != 1 || _pushBlocks.Active,
                "Actual blue-block floor approach must stop one contact update before pushing$35.");
            // Retain$bd at$d2, fill remaining slots with actual INTERAC$05:$80
            // effects. The original creation routine and native animation own
            // allocation/deletion; no reflected pool entries or fake timers.
            for (int index = 0; index < 13-free; index++)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                int slot = 0xd000+_entities.InteractionSlot(puff)*256+0x40;
                int linkX = rom.Word(0xd00c),linkY = rom.Word(0xd00a);
                rom.Word(0xd00c,200*256); rom.Word(0xd00a,120*256); rom[0xffae] = 0;
                byte[] caller = [0x01,0x80,0x05,0x16,0xd0,0xcd,0xc5,0x24,0xc9]; // objectCreateInteraction BC=$0580.
                for (int offset = 0; offset < caller.Length; offset++) rom[0xc100+offset] = caller[offset];
                SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,0);
                rom.Word(0xd00c,linkX); rom.Word(0xd00a,linkY);
                FailIf(slot != ((0xd3+index)<<8|0x40) || rom[slot] == 0 || rom[slot+1] != 5 || rom[slot+2] != 0x80,
                    "Native first-free creation must fill every declared capacity slot in order.");
                fillers.Add((puff,slot));
            }
            Step(push:true);
            var first = _entities.Entities<PushBlockController>();
            FailIf(!_pushBlocks.Active || first.Count != free || free == 1 &&
                (first[0].BlockTopLeft != new Vector2(96,80.5f) || _entities.InteractionSlot(first[0]) != 15),
                "A single free slot must admit descending source$56 before$43; a full pool must reject both.");
            Step(18);
            FailIf(_entities.Entities<PushBlockController>().Count != free || fillers.Any(row => row.Effect.Finished),
                "Repeated native scans must not allocate through live puff slots.");
            Step();
            FailIf(_entities.Entities<PushBlockController>().Count != free || fillers.Any(row => !row.Effect.Finished),
                "Puff deletion on update20 must not retroactively satisfy the earlier synchronizer pass.");
            Step();
            var children = _entities.Entities<PushBlockController>().OrderBy(_entities.InteractionSlot).ToArray();
            FailIf(children.Length != 2 || children[0].BlockTopLeft !=
                (free == 0 ? new Vector2(96,80.5f) : new Vector2(48,64.5f)),
                "Following state1 scan must retry skipped partners in descending source order and reuse deleted slots.");
            Step(11);
            FailIf(_pushBlocks.Active || _entities.Entities<PushBlockController>().Count != (free == 0 ? 2 : 1),
                "Primary update32 must retire only actors that started with it.");
            Step(19);
            FailIf(_entities.Entities<PushBlockController>().Count != (free == 0 ? 2 : 1),
                "Retried partners must retain their independent counters through movement update31.");
            Step(); Step(3);
            FailIf(_entities.Entities<PushBlockController>().Count != 0 ||
                new[] { 0x45,0x53,0x66 }.Any(position => _currentRoom.Layout[position] != 0x2e),
                "Retried partners must complete their own update32 and remain retired after the primary reservation is gone.");
        }
    }
}

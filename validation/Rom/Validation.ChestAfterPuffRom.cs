using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareChestAfterPuffGatesRom()
    {
        foreach (int branch in new[] { 0,1,2,3,4,5 })
        foreach (bool batch in RomHostSchedules(branch))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,0xba,OracleSaveData.RoomFlagItem,branch == 5);
            LoadValidationRoom(4,0xba); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,120.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Crown chest script fixture requires original south floor$77.");
            var data = new CrownDungeonDatabase();
            var record = data.GetRoomRecords(4,0xba).Single();
            FailIf(record.Id != 0x20 || record.SubId != 2 || record.Order != 0 || record.X != 120 || record.Y != 88,
                "Original Crown$4:$ba must place INTERAC$20:$02 at$58/$78 first.");
            var script = new DungeonTriggerChestScriptRoomEntity(record.Position,_runtimeState,7,15,() => _entities.ActiveTriggers,
                () => _saveData.HasRoomFlag(4,0xba,OracleSaveData.RoomFlagItem),_entities.IsScriptTextActive,
                _sound.PlaySound,() => _rooms.TrySetTile(0x57,0xf1));
            _entities.AddEntity(script);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,120,120);
            rom.Word(0xd00a,120*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 5;
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd242] = 2; rom[0xd24b] = 88; rom[0xd24d] = 120;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                CompareSomariaMotionRom(rom,$"Chest script branch={branch}, batch={batch}, update{++update}");
                FailIf(script.Finished != (rom[0xd240] == 0) || !script.Finished && script.Counter >= 0 && script.Counter != rom[0xd246] ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Chest branch={branch} update{update}: controller={script.Finished}:{script.Counter}/{rom[0xd240]:x2}:{rom[0xd246]}, triggers={_entities.ActiveTriggers:x2}/{rom[0xcca0]:x2}, queue={_rooms.PendingTileGraphics}/{((rom[0xcce0]-rom[0xccdf])&31)}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                var puffs = _entities.Entities<PuzzlePuffEffect>();
                int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40).Where(slot => rom[slot] != 0 && rom[slot+1] == 5).ToArray();
                FailIf(puffs.Count != native.Length,"Chest puff must allocate after its controller into the shared INTERACTION pool.");
                if (puffs.Count != 0) FailIf(_entities.InteractionSlot(puffs[0]) != (native[0]>>8)-0xd0 ||
                    puffs[0].Position != new Vector2(rom[native[0]+0xd],rom[native[0]+0xb]) ||
                    puffs[0].Initialized != (rom[native[0]+4] != 0) || puffs[0].CurrentParameter != rom[native[0]+0x21],
                    $"Chest puff update{update}: slot={_entities.InteractionSlot(puffs[0]):x2}/{(native[0]>>8)-0xd0:x2}, XY={puffs[0].Position}/{rom[native[0]+0xd]},{rom[native[0]+0xb]}, initialized={puffs[0].Initialized}/{rom[native[0]+4]}, param={puffs[0].CurrentParameter:x2}/{rom[native[0]+0x21]:x2}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Chest script must preserve original global RNG order.");
            });
            _entities.SetTrigger(7,branch != 4); _entities.SetTrigger(0,true); _entities.SetTrigger(1,true); _entities.SetTrigger(2,true);
            rom[0xcca0] = branch == 4 ? (byte)7 : (byte)0x87;
            _dialogue.ShowGameplayMessage("Pending chest script",120); rom[0xcba0] = branch is 4 or 5 ? (byte)0x80 : (byte)1;
            var allowScripts = _entities.TextAllowsScriptSource;
            try
            {
                _entities.TextAllowsScriptSource = () => branch is 4 or 5;
                Step(3);
            }
            finally { _entities.TextAllowsScriptSource = allowScripts; }
            if (branch == 5)
            {
                FailIf(script.Finished,"State0 must retain an ended script until the next eligible state1 pass.");
                _saveData.SetRoomFlag(4,0xba,OracleSaveData.RoomFlagItem,false); rom[0xc9ba] &= 0xdf;
            }
            if (branch is 2 or 4) { _saveData.SetRoomFlag(4,0xba,OracleSaveData.RoomFlagItem); rom[0xc9ba] |= 0x20; }
            if (branch == 4) { _entities.SetTrigger(7,true); rom[0xcca0] = 0x87; }
            _dialogue.Close(); rom[0xcba0] = 0; Step(branch == 4 ? 1 : 2);
            if (branch is 2 or 5)
            {
                FailIf(!script.Finished || sounds.Requests.Any(),"The first eligible stopifitemflagset must observe a flag written during initialization text.");
                continue;
            }
            if (branch != 4)
            {
                FailIf(script.Counter != -1 || sounds.Requests.Any(),"Exact trigger$87 must not satisfy checkmemoryeq$07.");
                _entities.SetTrigger(7,false); rom[0xcca0] = 7; Step();
                FailIf(script.Counter != -1 || sounds.Requests.Any(),"checkmemoryeq must yield after accepting$07.");
                Step();
            }
            FailIf(script.Counter != -1 || sounds.Requests.Count != 1,"playsound must yield before createpuff.");
            _entities.SetTrigger(2,false); rom[0xcca0] = 3;
            if (branch != 3) { _saveData.SetRoomFlag(4,0xba,OracleSaveData.RoomFlagItem); rom[0xc9ba] |= 0x20; }
            Step(); FailIf(script.Counter != -1,"createpuff must yield before wait15.");
            Step(); FailIf(script.Counter != 15,"wait15 must install its full counter independently of later trigger/item changes.");
            Step(14);
            if (branch == 3)
            {
                var cancelledRoom = _currentRoom;
                LoadValidationRoom(4,0xbb); _entities.Clear();
                // Declare the room-replacement boundary, then execute the
                // original outgoing interaction clear rather than its scripts.
                rom.SetOutgoingInteractions(); rom.ClearOutgoingInteractions();
                StepGameplayUpdates(20,Vector2.Zero,batched:batch,afterUpdate:() => {
                    rom.AdvanceInteractions(_entities.FrameCounter); rom.AdvanceTileGraphics();
                    FailIf(rom[0xd240] != 0 || _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count != 0 ||
                        cancelledRoom.GetMetatile(new(120,88)) == 0xf1 || rom[0xcf57] == 0xf1 ||
                        !sounds.Requests.SequenceEqual(rom.Sounds),
                        "Room replacement at wait1 must release the chest script without its terminal tile or further cues.");
                });
                LoadValidationRoom(4,0xba); StepGameplayUpdates(2,Vector2.Zero,batched:batch);
                FailIf(_entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single().Counter != -1 ||
                    _currentRoom.GetMetatile(new(120,88)) == 0xf1,
                    "An uncollected cancelled chest must re-enter at its trigger gate with a fresh script lifetime.");
                continue;
            }
            byte filler = _currentRoom.GetMetatile(new(24,24));
            for (int index = 0; index < (branch == 1 ? 31 : 0); index++) { FailIf(!_rooms.TrySetTile(0x11,filler),"Chest queue must accept31 fixture writes."); rom.SetTile(0x11,filler); }
            Step();
            FailIf(!script.Finished || (_currentRoom.GetMetatile(new(120,88)) == 0xf1) != (branch != 1),
                "Chest script must finish at wait zero whether its shared tile write succeeds or fails.");
            Step(12);
            FailIf((_currentRoom.GetMetatile(new(120,88)) == 0xf1) != (branch != 1) || _rooms.PendingTileGraphics != 0,
                "Queue draining must not replay rejected chest commands.");
            LoadValidationRoom(4,0xba); StepGameplayUpdates(2,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count != 0,
                "A newly initialized Crown chest script must stop on the collected-item flag.");
        }
    }
}

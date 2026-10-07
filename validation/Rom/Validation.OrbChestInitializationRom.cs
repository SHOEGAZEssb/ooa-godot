using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareOrbChestInitializationRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0x74,0x92 })
        foreach (byte text in new byte[] { 1,0x80 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,room,OracleSaveData.RoomFlagItem);
            LoadValidationRoom(4,room);
            FailIf(_entities.Entities<DungeonOrbChestRoomEntity>().Count != 1,
                $"Collected-item room$4:${room:x2} must still place its unconditional INTERAC$20 controller.");
            var record = new SkullDungeonDatabase().GetRoomRecords(4,room).Single(row => row.Kind == DungeonObjectKind.OrbChest);
            FailIf(record.Predicate != DungeonObjectCondition.Always,"stopifitemflagset is a script command, not a placement condition.");
            _entities.Clear();
            var chest = new DungeonOrbChestRoomEntity(record,_runtimeState,_sound.PlaySound,_entities.IsScriptTextActive,
                () => _saveData.HasRoomFlag(4,room,OracleSaveData.RoomFlagItem),
                () => _rooms.TrySetTile((byte)_currentRoom.GetPackedPosition(record.Position),0xf1));
            _entities.AddEntity(chest);
            _inventory.EquipA(0); _inventory.EquipB(0);
            var point = room == 0x74 ? new Vector2(184.25f,120.5f) : new Vector2(128.25f,96.5f);
            _player.WarpTo(point); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,(int)point.X,(int)point.Y);
            rom.Word(0xd00a,(int)(point.Y*256)); rom.Word(0xd00c,(int)(point.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 4;
            rom[0xd240] = 1; rom[0xd241] = 0x20; rom[0xd242] = (byte)record.SubId;
            rom[0xd24b] = (byte)record.Y; rom[0xd24d] = (byte)record.X;
            _runtimeState.SetWramByte(0xcfc1,0x91); _runtimeState.SetWramByte(0xcfc2,0x82);
            rom[0xcfc1] = 0x91; rom[0xcfc2] = 0x82;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter); rom.AdvanceTileGraphics();
                CompareSomariaMotionRom(rom,$"Collected orb chest$4:${room:x2}, text={text:x2}, batch={batch}");
                FailIf(chest.Finished != (rom[0xd240] == 0) || chest.Counter != -1 ||
                    _runtimeState.ReadWramByte(0xcfc1) != rom[0xcfc1] || _runtimeState.ReadWramByte(0xcfc2) != rom[0xcfc2] ||
                    !chest.Finished && _entities.InteractionSlot(chest) != 2 ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Collected orb chest$4:${room:x2}: deleted={chest.Finished}/{rom[0xd240]:x2}, state={rom[0xd244]}, clear={_runtimeState.ReadWramByte(0xcfc1):x2}/{rom[0xcfc1]:x2},{_runtimeState.ReadWramByte(0xcfc2):x2}/{rom[0xcfc2]:x2}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Collected orb script must preserve original RNG order.");
            });
            _dialogue.ShowGameplayMessage("Collected orb script",120); rom[0xcba0] = text;
            var allowScripts = _entities.TextAllowsScriptSource;
            try { _entities.TextAllowsScriptSource = () => text == 0x80; Step(3); }
            finally { _entities.TextAllowsScriptSource = allowScripts; }
            FailIf(chest.Finished,"Initialized script must retain its slot throughout ordinary text eligibility.");
            if (text == 0x80)
            {
                // state0 installed stubScript. Clearing the flag afterward
                // cannot undo that ended script or re-enter its toggle gate.
                _saveData.SetRoomFlag(4,room,OracleSaveData.RoomFlagItem,false); rom[0xc900+room] &= 0xdf;
            }
            _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            FailIf(!chest.Finished || rom.Sounds.Count != 0,"Collected orb script must delete silently on its first eligible state1 pass.");
        }
    }
}

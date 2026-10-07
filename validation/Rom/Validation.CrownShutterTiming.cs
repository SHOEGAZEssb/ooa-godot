using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownShutterTiming()
    {
        CompareShutterButtonQueueRom();
        int fixture = 0;
        foreach (bool clearedLater in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            // Declare the shared enemy-shutter variant at this directional
            // shutter position. Enemy-stream/count publication has separate
            // regressions; this input isolates its original common script.
            var record = data.GetRoomRecords(4,0x9d).Single(row => row.Id == 0x1e) with { SubId = 10 };
            int enemies = clearedLater ? 1 : 0;
            var door = new DungeonDoorRoomEntity(record,_currentRoom,data,() => enemies,_ => false,
                p => p-new Vector2(80,48),() => (long)_animationTicks,_sound.PlaySound,
                default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal);
            _entities.AddEntity(door);
            Vector2 start = new(184.25f,104.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Enemy-shutter script fixture requires unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,184,104);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 10; rom[0xd24b] = 0xa7;
            var sounds = _sound.AttachPlayRequestAudit();
            // commonScripts/scripting yields: radii1, angle2, branch3;
            // empty: state2 at4, interleave5. Clear at20: check20, sound21,
            // wait8 at22, state2 at30, interleave31, final write37.
            int interleave = clearedLater ? 31 : 5, finish = interleave+6;
            for (int tick = 1; tick <= finish+3; tick++)
            {
                if (tick == 20) enemies = 0;
                rom[0xcdd1] = (byte)enemies;
                int soundStart = sounds.Requests.Count;
                StepSomariaMotionRom(rom,1,batch,afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    string context = $"Enemy-shutter cleared={clearedLater}, batch={batch}, update={tick}";
                    FailIf(door.Finished != (rom[0xd240] == 0) ||
                        SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                        _entities.BossEntrySignal != rom[0xcc93] ||
                        _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                        context+": original script lifetime/wait/interleave/shared shutter signal/queue differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                        context+": ordered solve/door cues or RNG differ.");
                });
                int[] expected = tick == interleave || tick == finish ? [SoundId.SndDoorClose] :
                    clearedLater && tick == 21 ? [SoundId.SndSolvePuzzle] : [];
                FailIf(!sounds.Requests.Skip(soundStart).SequenceEqual(expected) ||
                    _currentRoom.GetMetatile(door.Position) != (tick < interleave ? 0x7a : 0xa0) ||
                    _currentRoom.GetTerrainInfo(door.Position).Collision != (tick < finish ? 0x0f : 0) ||
                    door.Finished != (tick >= finish),
                    "Native enemy shutter must retain independently traced command yields, six-update collision boundary and scriptend deletion.");
            }
        }
    }
}

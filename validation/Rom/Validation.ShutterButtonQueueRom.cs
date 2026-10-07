using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareShutterButtonQueueRom()
    {
        int fixture = 0;
        foreach (int full in new[] { 0,1,2 }) // normal, rejected opening, rejected closing
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9d); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var records = data.GetRoomRecords(4,0x9d);
            var doorRecord = records.Single(row => row.Id == 0x1e);
            var buttonRecord = records.Single(row => row.Id == 9);
            FailIf(doorRecord.SubId != 6 || doorRecord.PackedPosition != 0xa7 ||
                buttonRecord.SubId != 0x80 || buttonRecord.PackedPosition != 0x8c,
                "Shutter handoff requires original room4:9d INTERAC$1e:$06/$a7 and PART$09:$80/$8c.");
            var door = new DungeonDoorRoomEntity(doorRecord,_currentRoom,data,() => 0,
                _entities.TriggerIsActive,p => p-new Vector2(80,48),() => (long)_animationTicks,
                _sound.PlaySound,default,true,_rooms.TrySetTile,_entities.UpdateBossShutterSignal);
            var button = new GroundButtonRoomEntity(buttonRecord,_currentRoom,data,_entities.SetTrigger,
                (packed,tile) => _rooms.TrySetTile(packed,tile),_sound.PlaySound);
            _entities.AddEntity(door); _entities.AddEntity(button); _entities.SetTrigger(7,true);
            Vector2 start = new(200.25f,104.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start),"Shutter button must be reached through unchanged room4:9d floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,200,104) { HostilePartsEnabled = true };
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffaa] = 48; rom[0xffac] = 80; rom[0xcca0] = 0x80;
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = 6; rom[0xd24b] = 0xa7;
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = 0x80;
            rom[0xd0cb] = 136; rom[0xd0cd] = 200;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() => {
                    rom.AdvanceTileGraphics();
                    string context = $"Button/shutter full={full}, batch={batch}, update={++update}";
                    FailIf(door.Finished != (rom[0xd240] == 0) ||
                        SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                        button.Pressed != (rom[0xd0f0] != 0) || button.ReleaseCounter != rom[0xd0c6] ||
                        _entities.ActiveTriggers != rom[0xcca0] ||
                        _entities.BossEntrySignal != rom[0xcc93] ||
                        _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                        context+": door/button lifetime/counter/shared trigger/queue differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                        context+": ordered button/solve/door cues or RNG differ.");
                });
            void FillQueue()
            {
                for (int write = 0; write < 31; write++)
                {
                    FailIf(!_rooms.TrySetTile(0x11,0xa0),"Shutter queue must accept31 writes before completion.");
                    rom.SetTile(0x11,0xa0);
                }
            }
            void WaitForInterleave(int state)
            {
                for (int wait = 0; !(rom[0xd244] == state && rom[0xd245] == 1) && wait < 40; wait++) Step();
                FailIf(rom[0xd244] != state || rom[0xd246] != 6,
                    "Shutter must begin its native six-update interleave after the button handoff.");
                Step(5);
                FailIf(rom[0xd246] != 1,"Shutter must retain its final collision boundary after five updates.");
            }
            Step();
            for (int walk = 0; !button.Pressed && walk < 40; walk++) Step(angle:16);
            FailIf(!button.Pressed || _currentRoom.IsSolid(_player.Position) || _entities.ActiveTriggers != 0x81,
                "Actual collision approach must press only triggerbit0 and preserve bit7.");
            WaitForInterleave(2);
            if (full == 1) FillQueue();
            Step();
            FailIf(_currentRoom.GetMetatile(door.Position) != 0xa0 ||
                _currentRoom.IsSolid(door.Position) != (full == 1),
                "Rejected opening must retain collision despite the interleave's early layout$a0 write.");
            if (full == 1)
            {
                Step(24);
                FailIf(!_currentRoom.IsSolid(door.Position) || sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 2,
                    "Active trigger must not retry rejected opening once its directional closed layout byte is gone.");
            }
            else
            {
                for (int walk = 0; button.Pressed && walk < 24; walk++) Step(angle:0);
                FailIf(button.Pressed || _entities.ActiveTriggers != 0x80,"Walking away must release the actual reusable button.");
                WaitForInterleave(3);
                if (full == 2) FillQueue();
                Step();
                FailIf(_currentRoom.IsSolid(door.Position) != (full != 2),
                    "Rejected closing must retain floor collision while consuming its completion update/cue.");
                Step(24);
                FailIf(!_currentRoom.IsSolid(door.Position) || _currentRoom.GetMetatile(door.Position) != 0x7a ||
                    sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != (full == 2 ? 6 : 4),
                    "Inactive trigger must naturally retry a rejected closing after queue drain and then retain its closed shutter.");
            }
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Button/shutter handoff must solve exactly once across rejection, completion and retry.");
        }
    }
}

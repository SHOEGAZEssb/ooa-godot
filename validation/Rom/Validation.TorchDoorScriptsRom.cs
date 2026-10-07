using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareTorchDoorScriptsRom()
    {
        int fixture = 0;
        foreach (var c in new[] { (Sub:0x14,Direction:0,Required:2,Y:10,X:8),
            (Sub:0x15,Direction:3,Required:2,Y:8,X:10),
            (Sub:0x16,Direction:2,Required:1,Y:10,X:8),
            (Sub:0x17,Direction:3,Required:1,Y:8,X:10) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xce); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,24.5f)); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(_player.Position),"Torch door must retain the source room's floor between its two torches.");
            var data = new DungeonMechanicDatabase();
            var source = data.GetRoomRecords(4,0xce).Single(row => row.Id == 0x1e);
            FailIf(source.SubId != 0x14 || source.Order != 2 || source.PackedPosition != 7 || source.Parameter != 0 ||
                data.DoorAngle(c.Sub) != 0x10+c.Direction*2 || data.DoorRadiusY(c.Sub) != c.Y ||
                data.DoorRadiusX(c.Sub) != c.X || data.DoorTorchCount(c.Sub) != c.Required,
                "Original INTERAC$1e:$14-$17 script profiles and room$4:$ce placement must be imported independently.");
            // Declared external wNumTorchesLit producer, including surplus
            // count. The source room's actual PART/Ember handoff is separate.
            int lit = 0;
            var record = source with { SubId = c.Sub };
            _currentRoom.SetPositionTileAndCollision(new(120,8),(byte)(0x78+c.Direction),0x0f,0);
            var door = new DungeonDoorRoomEntity(record,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p,() => _entities.FrameCounter,_sound.PlaySound,default,true,_rooms.TrySetTile,
                _entities.UpdateBossShutterSignal,_entities.IsScriptTextActive,_entities.IsOutgoingEntity,
                () => _entities.DoorPaletteFadeActive,() => lit);
            _entities.AddEntity(door);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,120,24);
            rom.Word(0xd00a,24*256+128); rom.Word(0xd00c,120*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = (byte)c.Sub; rom[0xd24b] = 7;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batch,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                var random = _random.CaptureState();
                FailIf(door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    door.Counter2Alias != rom[0xd247] || _entities.BossEntrySignal != rom[0xcc93] ||
                    rom[0xd240] != 0 && (door.Position != new Vector2(rom[0xd24d],rom[0xd24b]) ||
                        SomariaPrivate<int>(door,"_angle") != rom[0xd249] || SomariaPrivate<int>(door,"_speed") != rom[0xd250]) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls,
                    $"Torch door${c.Sub:x2}, batch={batch}, count={lit}: runtime={SomariaPrivate<DoorState>(door,"_state")}/finished={door.Finished}/counter={SomariaPrivate<int>(door,"_counter")}; native=${rom[0xd244]:x2}:${rom[0xd245]:x2}/alive={rom[0xd240]}/counter={rom[0xd246]}.");
            });
            Step(12);
            FailIf(SomariaPrivate<DoorState>(door,"_state") != DoorState.WatchingTorches || !_currentRoom.IsSolid(door.Position),
                "The initialized torch script must wait after its skipped solid closing tile.");
            lit = c.Required+1; rom[0xcc8f] = (byte)lit; Step(6);
            FailIf(sounds.Requests.Count != 0 || SomariaPrivate<DoorState>(door,"_state") != DoorState.WatchingTorches,
                "Surplus torches must fail the original equality check without starting a solve delay.");
            lit = c.Required; rom[0xcc8f] = (byte)lit; Step(2);
            FailIf(SomariaPrivate<DoorState>(door,"_state") != DoorState.TorchDelay || rom[0xd246] != 30,
                "Exact torch equality must select the script's separate wait30 command.");
            // Once selected, the wait does not re-read a later count change.
            lit = 0; rom[0xcc8f] = 0;
            _dialogue.ShowGameplayMessage("Torch wait pause",120); rom[0xcba0] = 1; Step(3);
            FailIf(rom[0xd246] != 30,"Text must hold the script-owned torch delay.");
            _dialogue.Close(); rom[0xcba0] = 0; Step(29);
            FailIf(rom[0xd246] != 1 || sounds.Requests.Count != 0 || !_currentRoom.IsSolid(door.Position),
                "The torch wait must not solve on update29.");
            Step();
            FailIf(!sounds.Requests.SequenceEqual(new[] { SoundId.SndSolvePuzzle }),
                "The terminal thirtieth eligible update must play the solve command before selecting opening.");
            Step(8); Step(3);
            FailIf(!door.Finished || _currentRoom.Layout[7] != 0xa0 || _currentRoom.IsSolid(door.Position) ||
                !sounds.Requests.SequenceEqual(new[] { SoundId.SndSolvePuzzle,SoundId.SndDoorClose,SoundId.SndDoorClose }),
                "Torch scripts must open once after wait30 and six interleaved updates, then retire.");
        }
    }
}

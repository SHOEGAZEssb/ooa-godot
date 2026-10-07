using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareGroundButtonPreloadRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xbc); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0xbc).Single(row => row.Id == 9 && row.PackedPosition == 0x34);
            var source = new GroundButtonRoomEntity(record,_currentRoom,data,_entities.SetTrigger,
                (packed,tile) => _rooms.TrySetTile(packed,tile),_sound.PlaySound);
            _entities.AddEntity(source); _player.WarpTo(new(72.25f,88.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Button preload must begin on unchanged source floor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,72,88) { HostilePartsEnabled = true };
            rom.Word(0xd00c,72*256+64); rom.Word(0xd00a,88*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = 0x80;
            rom[0xd0cb] = 56; rom[0xd0cd] = 72;
            var sounds = _sound.AttachPlayRequestAudit();
            for (int wait = 0; !source.Pressed && wait < 40; wait++)
                StepSomariaMotionRom(rom,1,batch,0);
            FailIf(!source.Pressed || rom[0xd0f0] == 0 || _currentRoom.IsSolid(_player.Position),
                "Native floor approach must press the button before its preload boundary.");
            // Declare same-room preload while Link overlaps its real button.
            // Incoming PART prefix is transcribed from mainData.s; original
            // marking, state0 dispatch and cleanup execute in the ROM. This
            // isolates the owner handoff, not scroll travel or incoming AI.
            rom.SetOutgoingParts(); rom[0xcd00] = 8;
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            byte[] positions = [0x34,0x3a,0x74,0x7a];
            for (int index = 0; index < positions.Length; index++)
            {
                int slot = 0xd1c0+index*256,packed = positions[index];
                rom[slot] = 1; rom[slot+1] = 9; rom[slot+2] = (byte)(0x80+index);
                rom[slot+0xb] = (byte)((packed>>4)*16+8); rom[slot+0xd] = (byte)((packed&15)*16+8);
            }
            rom.AdvanceParts(_entities.FrameCounter);
            var incoming = _entities.Entities<GroundButtonRoomEntity>().OrderBy(row => row.SubId).ToArray();
            FailIf(incoming.Length != 4 || incoming.Where((button,index) =>
                button.PackedPosition != positions[index] || button.SubId != 0x80+index).Any(),
                "Source mainData.s must retain all four incoming PART$09 records in original order.");
            void Compare()
            {
                for (int index = 0; index < incoming.Length; index++)
                {
                    int slot = 0xd1c0+index*256;
                    FailIf(incoming[index].Pressed != (rom[slot+0x30] != 0) ||
                        incoming[index].ReleaseCounter != rom[slot+6] || incoming[index].UpdatesDuringDialogue ||
                        incoming[index].Visible || rom[slot+4] != 1,
                        "Native scrolling state0 must fall through to pressure once, then retain initialized invisible buttons.");
                }
                FailIf(_entities.ActiveTriggers != rom[0xcca0] || !sounds.Requests.SequenceEqual(rom.Sounds),
                    "Button preload must retain native shared trigger lifetime and ordered pressure cues.");
                foreach (byte packed in positions)
                {
                    Vector2 point = new((packed&15)*16+8,(packed>>4)*16+8);
                    FailIf(_currentRoom.Layout[packed] != rom[0xcf00+packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(packed),
                        "Preload pressure must preserve native button layout/collision/underlying bytes.");
                }
            }
            Compare();
            FailIf(!incoming[0].Pressed,"Incoming state0 must press the overlapping original button during preload.");
            _player.WarpTo(new(24.25f,24.5f)); rom.Word(0xd00c,24*256+64); rom.Word(0xd00a,24*256+128);
            StepGameplayUpdates(8,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceParts(_entities.FrameCounter); Compare();
            });
            FailIf(!incoming[0].Pressed,"Initialized incoming buttons must hold pressure throughout frozen scrolling.");
            _entities.FinishScreenTransition(); rom.ClearOutgoingParts(); rom[0xcd00] = 1;
            FailIf(rom[0xd0c0] != 0,"Native bulk cleanup must release the old PART slot while retaining incoming buttons.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:() => {
                    rom.AdvanceParts(_entities.FrameCounter); rom.AdvanceTileGraphics(); Compare();
                });
                FailIf(incoming[0].Pressed,"First resumed PART update must release pressure without repeating state0.");
            }
        }
    }

    private void CompareGroundButtonGameplayRom()
    {
        int fixture = 0;
        foreach (bool reusable in new[] { true,false })
        foreach (bool objectPressure in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,reusable ? 0xbc : 0x08);
            var data = new DungeonMechanicDatabase();
            int position = reusable ? 0x34 : 0x17;
            var record = data.GetRoomRecords(4,_currentRoom.Id).Single(row => row.Id == 9 && row.PackedPosition == position);
            FailIf(record.SubId != (reusable ? 0x80 : 0) || _currentRoom.Layout[position] != 0x0c,
                "Native button fixture requires original PART$09 subid$80/$00 and tile$0c.");
            _entities.Clear();
            var button = new GroundButtonRoomEntity(record,_currentRoom,data,_entities.SetTrigger,
                (packed,tile) => _rooms.TrySetTile(packed,tile),_sound.PlaySound);
            _entities.AddEntity(button); _entities.SetTrigger(7,true);
            _inventory.GiveTreasure(TreasureId.Feather,1); _inventory.EquipA(TreasureId.Feather);
            Vector2 start = button.Position + Vector2.Down * (reusable ? 32 : 16);
            _player.WarpTo(start); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(start),"Button approach must start on original room floor.");
            if (objectPressure) _currentRoom.SetPositionTileAndCollision(button.Position,0x2a,null,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,(int)start.X,(int)start.Y) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcca0] = 0x80;
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = (byte)record.SubId;
            rom[0xd0cb] = (byte)button.Position.Y; rom[0xd0cd] = (byte)button.Position.X;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int pressed = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held:pressed,pressed:pressed,afterUpdate:() =>
                {
                    string context = $"PART$09 reusable={reusable}, object={objectPressure}, batch={batch}, update={++update}";
                    FailIf((_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                        _player.TopDownAirborne != ((rom[0xcc5c] & 15) != 0) ||
                        button.Finished != (rom[0xd0c0] == 0) || _entities.ActiveTriggers != rom[0xcca0] ||
                        !button.Finished && (button.Pressed != (rom[0xd0f0] != 0) || button.ReleaseCounter != rom[0xd0c6]),
                        context + ": lifetime/trigger/pressure/release counter differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": gameplay cues/shared RNG differs.");
                });
            // updateParts admits state0 during text, then suppresses the
            // initialized button on subsequent updates of the same modal.
            _dialogue.ShowMessage("Button initialization pause.",120); rom[0xcba0] = 1;
            Step(); Step(2); _dialogue.Close(); rom[0xcba0] = 0;
            if (objectPressure)
            {
                FailIf((!reusable) != button.Finished || _entities.ActiveTriggers != 0x81 ||
                    _currentRoom.Layout[position] != 0x2a || _currentRoom.GetUnderlyingMetatile(button.Position) != 0x0d ||
                    reusable && button.ReleaseCounter != 28,
                    "Object pressure must preserve the covering tile, write underlying$0d and retain unrelated trigger bit$80.");
                Step(5);
                _currentRoom.SetPositionTileAndCollision(button.Position,0x0d,null,0);
                rom[0xcf00 + position] = 0x0d; rom[0xce00 + position] = _currentRoom.GetCollision(0x0d);
                _dialogue.ShowMessage("Button pause.",120); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(27);
                FailIf(reusable && (!button.Pressed || button.ReleaseCounter != 1),
                    "Uncovered reusable button must retain pressure through release update27.");
                Step();
                FailIf(_entities.ActiveTriggers != (reusable ? 0x80 : 0x81) ||
                    _currentRoom.Layout[position] != (reusable ? 0x0c : 0x0d),
                    "Reusable object pressure releases on28; one-shot pressure remains after deletion.");
            }
            else
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    for (int wait = 0; !button.Pressed && !button.Finished && wait < 40; wait++)
                    {
                        Step(angle:0);
                        FailIf(_currentRoom.IsSolid(_player.Position),"Button approach crossed original collision geometry.");
                    }
                    FailIf(_entities.ActiveTriggers != 0x81 || _currentRoom.Layout[position] != 0x0d ||
                        !reusable && !button.Finished,"Ground Link contact must press native button without an object release delay.");
                    _dialogue.ShowMessage("Pressed button pause.",120); rom[0xcba0] = 1;
                    Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                    if (reusable)
                    {
                        Step(pressed:1);
                        FailIf(!_player.TopDownAirborne || !button.Pressed,
                            "Physical Feather takeoff while overlapping a pressed button must retain pressure at nonzero Link zh.");
                    }
                    Step(16,16);
                    FailIf(_entities.ActiveTriggers != (reusable ? 0x80 : 0x81) ||
                        reusable && (button.Pressed || button.ReleaseCounter != 0),
                        "Walking away must release a reusable Link-held button immediately and retain a deleted one-shot trigger.");
                    if (!reusable) break;
                    Step(20); // Complete the physical jump before the next ground approach.
                }
            }
            LoadValidationRoom(0,0x60); LoadValidationRoom(4,reusable ? 0xbc : 0x08);
            FailIf(_entities.Entities<GroundButtonRoomEntity>().All(row => row.PackedPosition != position) ||
                _currentRoom.Layout[position] != 0x0c,"Button room re-entry must reconstruct its original allocation/tile.");
        }
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareTileInfoContactRom()
    {
        int fixture = 0;
        // Independent original rooms/ages/large/room04xx.bin and handler3:
        // $56/$14=$1f, $8d/$02=$30, $1b/$1a=$08. Approach on real floor.
        foreach (var row in new[] {
            (Room:0x56,Position:0x14,Tile:0x1f,Start:new Vector2(56,24),Direction:1,Text:0x5105,Mask:0x10),
            (Room:0x8d,Position:0x02,Tile:0x30,Start:new Vector2(40,24),Direction:0,Text:0x5106,Mask:0x20),
            (Room:0x1b,Position:0x1a,Tile:0x08,Start:new Vector2(168,40),Direction:0,Text:0x5108,Mask:0x20) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,row.Room); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword); _inventory.EquipB(0);
            if (row.Room == 0x56)
            {
                _inventory.GiveTreasure(TreasureId.Feather,1);
                _inventory.GiveTreasure(TreasureId.Shield,1);
                _inventory.EquipB(TreasureId.Feather);
            }
            _player.WarpTo(row.Start); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(row.Direction * 8));
            Vector2 center = new((row.Position & 15) * 16 + 8,(row.Position >> 4) * 16 + 8);
            FailIf(_collision.Collides(row.Start) || _currentRoom.GetMetatile(center) != row.Tile,
                $"Info tile$4:${row.Room:x2}/${row.Position:x2} must retain original floor approach/tile${row.Tile:x2}.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,row.Direction,(int)row.Start.X,(int)row.Start.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int pressed = 0,int held = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held | pressed,pressed,() =>
                {
                    string context = $"Info TX_{row.Text:x4} batch={batch}, update={++update}";
                    if (row.Room == 0x56)
                    {
                        FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                            SomariaPrivate<int>(_player,"_shieldParentButton") !=
                                (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                            SomariaPrivate<bool>(_player,"_shieldParentInitialized") != (rom[0xd504] != 0),
                            context + ": Shield raised/parent/button state differs.");
                        FailIf(_player.KnockbackFrames != rom[0xd02d] ||
                            _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                            _inventory.HealthQuarters != rom[0xc6aa],
                            context + ": accepted-contact recoil/invincibility/health differs.");
                        FailIf((_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014) ||
                            _player.TopDownAirborne != (rom[0xcc5c] != 0),
                            context + ": Feather Z/velocity/airborne state differs.");
                        if (_player.TopDownAirborne)
                        {
                            int phase = rom[0xd031] switch
                            {
                                0xe4 => 0, 0xe8 => 1, 0xec => 2, 0x80 => 3,
                                _ => throw new System.InvalidOperationException(
                                    $"{context}: unknown native jump graphic ${rom[0xd031]:x2}.")
                            };
                            FailIf(!_player.AirborneLinkUsesJumpAnimation || rom[0xd030] != 0x18 ||
                                _player.AirborneLinkBodyFrame != phase ||
                                phase < 3 && _player.LinkAnimationCounter != rom[0xd020],
                                context + ": Feather animation phase/counter differs.");
                        }
                    }
                    FailIf(_rooms.TilePushCounter != rom[0xcc6a] ||
                        _pushBlocks.RemainingPushFrames != rom[0xcc6a] ||
                        _keyDoors.RemainingPushFrames != rom[0xcc6a] ||
                        _keyholes.RemainingPushFrames != rom[0xcc6a] ||
                        _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": shared contact byte/hint mask/modal differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue is not (SoundId.SndText or SoundId.SndDamageLink))
                            .SequenceEqual(rom.Sounds.Where(cue => cue != SoundId.SndDamageLink)),
                        context + $": gameplay cues/shared RNG differ: runtime cues={string.Join(',',sounds.Requests)}, native={string.Join(',',rom.Sounds)}, RNG=${random.Rng1:x2}/${random.Rng2:x2}/{random.Calls - seed.Calls}, native=${rom[0xff94]:x2}/${rom[0xff95]:x2}/{rom.RandomCalls}.");
                },contextPrefix:$"Info TX_{row.Text:x4}");
            Step();
            int direction = row.Direction * 8;
            if (row.Room == 0x56)
            {
                for (int wait = 0; _rooms.TilePushCounter != 10 && wait < 32; wait++) Step(angle:direction);
                FailIf(_rooms.TilePushCounter != 10,"Feather contact probe must reach its actual cracked block first.");
                Step(angle:direction,pressed:2);
                FailIf(!_player.TopDownAirborne || rom[0xcc5c] == 0 || _rooms.TilePushCounter != 9,
                    "Front contact must decrement before the newly requested Feather sets airborne state.");
                Step(4,direction);
                FailIf(_rooms.TilePushCounter != 9,"Airborne Link bypasses front dispatch and retains its countdown.");
                _dialogue.ShowMessage("Airborne contact pause.",_player.Position.Y); rom[0xcba0] = 1;
                Step(3,direction); _dialogue.Close(); rom[0xcba0] = 0;
                for (int wait = 0; _player.TopDownAirborne && wait < 48; wait++) Step(angle:direction);
                FailIf(_player.TopDownAirborne || _rooms.TilePushCounter != 9,
                    "Feather landing retains the counter through its final airborne dispatch.");
                Step();
                FailIf(_rooms.TilePushCounter != 20,"Neutral contact after landing resets the shared wait before retry.");
                _inventory.EquipB(TreasureId.Shield); rom[0xc688] = TreasureId.Shield;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    for (int wait = 0; _rooms.TilePushCounter != 10 && wait < 24; wait++) Step(angle:direction);
                    FailIf(_rooms.TilePushCounter != 10,"Shield probe must regain actual wall contact before raising.");
                    Step(angle:direction,pressed:2);
                    FailIf(!_player.IsUsingShield || _rooms.TilePushCounter != 9 ||
                        _playerWorld.TilePushingDirection != 0xff,
                        "Shield raises after front contact, then suppresses the next update's push signal.");
                    Step(4,direction,held:2);
                    FailIf(_rooms.TilePushCounter != 20 || _dialogue.IsOpen,
                        "Held Shield resets tile contact instead of spending its hint wait.");
                    _dialogue.ShowMessage("Shield contact pause.",_player.Position.Y); rom[0xcba0] = 1;
                    Step(3,direction);
                    FailIf(!_player.IsUsingShield,"Text freezes the raised parent even after its button is released.");
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(angle:direction);
                    FailIf(_player.IsUsingShield || _rooms.TilePushCounter != 20 ||
                        _playerWorld.TilePushingDirection != row.Direction,
                        "Release consumes the prior suppressed signal before publishing renewed wall contact.");
                    Step(angle:direction);
                    FailIf(_rooms.TilePushCounter != 19,
                        "Tile contact resumes on the update after Shield releases.");
                }
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    for (int wait = 0; _rooms.TilePushCounter != 10 && wait < 24; wait++) Step(angle:direction);
                    int damageCues = sounds.Requests.Count(cue => cue == SoundId.SndDamageLink);
                    FailIf(_rooms.TilePushCounter != 10 ||
                        !_player.ApplyEnemyContactDamage(_player.Position + Vector2.Left * 16,1),
                        "Tile contact recoil must begin through the normal accepted-damage gate at countdown10.");
                    // Shared accepted-contact caller; native Link owns every
                    // following recoil update, including collision and recovery.
                    // The damage-caller cue lies outside applyDamageToLink.
                    FailIf(sounds.Requests.Count(cue => cue == SoundId.SndDamageLink) != damageCues + 1,
                        "One accepted collision must request its source SND_DAMAGE_LINK cue exactly once.");
                    rom[0xd02a] = 0x80; rom[0xd02b] = 0x22; rom[0xd02c] = 8; rom[0xd02d] = 0x0f;
                    rom.ApplyLinkDamage(0xfe); rom[0xd02a] = 0;
                    Step(4,direction);
                    _dialogue.ShowMessage("Tile contact recoil pause.",_player.Position.Y); rom[0xcba0] = 1;
                    Step(3,direction); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(11,direction);
                    FailIf(_player.KnockbackFrames != 0 || _rooms.TilePushCounter != 10,
                        "All15 recoil updates, including terminal recovery, bypass front contact.");
                    Step(angle:direction);
                    FailIf(_rooms.TilePushCounter != 9,
                        "The first eligible update after recoil resumes the retained countdown.");
                    Step(20);
                }
            }
            for (int wait = 0; _rooms.TilePushCounter != 1 && wait < 48; wait++) Step(angle:direction);
            FailIf(_rooms.TilePushCounter != 1 || _dialogue.IsOpen,
                $"Tile hint$4:${row.Room:x2} must approach and spend19 contact updates: counter={_rooms.TilePushCounter}, Link={_player.PrecisePosition}, push=${_playerWorld.TilePushingDirection:x2}, nativeXY={rom.Word(0xd00c) / 256f}/{rom.Word(0xd00a) / 256f}, walls=${rom[0xd033]:x2}, text={_dialogue.IsOpen}.");
            // Declared shared-byte boundaries while actual contact remains
            // published. No candidate clock may overwrite them on dispatch.
            _rooms.TilePushCounter = rom[0xcc6a] = 0;
            Step(angle:direction);
            FailIf(_rooms.TilePushCounter != 0xff || _dialogue.IsOpen,
                "Native decPushingAgainstTileCounter wraps$00->$ff without showing a hint.");
            _rooms.TilePushCounter = rom[0xcc6a] = 3;
            Step(2,direction);
            FailIf(_rooms.TilePushCounter != 1,"An inherited shared countdown must expire without restarting the candidate wait.");
            Step(angle:direction,pressed:1);
            FailIf(!_dialogue.IsOpen || _player.IsAttacking || _rooms.TilePushCounter != 20 ||
                _rooms.InformativeTextsShown != row.Mask || rom.TextGeneration != 1 ||
                rom[0xcba2] != (row.Text & 255) || rom[0xcba3] != 0x55,
                $"TX_{row.Text:x4} must show once, reset$cc6a and return before fresh Sword input.");
            Step(3,direction); _dialogue.Close(); rom[0xcba0] = 0;
            Step(); Step(24,direction);
            FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || _currentRoom.GetMetatile(center) != row.Tile,
                "Repeated contact must preserve the tile and suppress the same hint.");
            Step(); Step(angle:direction,pressed:1);
            FailIf(!_player.IsAttacking,"Sword input must resume after the hint's carry-return update.");
            _rooms.TilePushCounter = rom[0xcc6a] = 9;
            rom.ClearRoomVariables(scrolling:true);
            _rooms.SetLoadedRoom(4,_rooms.GetRoom(4,row.Room));
            FailIf(_rooms.TilePushCounter != 9 || _rooms.TilePushCounter != rom[0xcc6a] ||
                _rooms.InformativeTextsShown != 0 || _rooms.InformativeTextsShown != rom[0xccd7],
                "Native scroll activation retains$cc6a while clearing$ccd7.");
            rom.ClearRoomVariables(scrolling:false); LoadValidationRoom(4,row.Room);
            FailIf(_rooms.TilePushCounter != 0 || _rooms.TilePushCounter != rom[0xcc6a] ||
                _rooms.InformativeTextsShown != 0 || _rooms.InformativeTextsShown != rom[0xccd7],
                "Native ordinary reload clears$cc6a/$ccd7 without controller cancellation initializing them.");
        }
    }
}

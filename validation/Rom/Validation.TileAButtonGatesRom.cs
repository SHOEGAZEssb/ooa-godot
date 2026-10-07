using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareTileAButtonGatesRom()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x2a); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather,1);
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria,1);
            _inventory.GiveTreasure(TreasureId.Bracelet,1);
            _inventory.EquipA(0); _inventory.EquipB(TreasureId.Feather);
            _player.WarpTo(new(88,88)); _player.Face(Vector2I.Up);
            // Original room$0:$2a sign at$35 and signTextGroupTable TX_2e01.
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(88,56)) != 0xf2,
                "Sign input gate must approach original tile$35:$f2 through its real southern floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,88,88);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int pressed = 0,int held = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held | pressed,pressed,() =>
                {
                    string context = $"Sign$0:$2a/$35 batch={batch}, update={++update}";
                    FailIf(_dialogue.IsOpen != (rom[0xcba0] != 0) ||
                        _rooms.TilePushCounter != rom[0xcc6a] ||
                        _player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                        (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                        (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014) ||
                        _player.KnockbackFrames != rom[0xd02d] ||
                        _player.InvincibilityFrames != unchecked((sbyte)rom[0xd02b]) ||
                        _inventory.HealthQuarters != rom[0xc6aa],
                        context + ": tile modal/contact/air/recoil/health differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue is not (SoundId.SndText or SoundId.SndDamageLink))
                            .SequenceEqual(rom.Sounds.Where(cue => cue != SoundId.SndDamageLink)),
                        context + ": gameplay cues/shared RNG differ.");
                },contextPrefix:"Sign A-button eligibility");
            Step();
            for (int wait = 0; _playerWorld.TilePushingDirection != 0 && wait < 48; wait++) Step(angle:0);
            FailIf(_playerWorld.TilePushingDirection != 0 || _currentRoom.IsSolid(_player.Position),
                "Sign must be reached through the actual collision approach.");
            Step(pressed:2);
            FailIf(!_player.TopDownAirborne,"Equipped Feather must launch beside the reachable sign.");
            Step(pressed:1); Step(3); Step(pressed:1);
            FailIf(_dialogue.IsOpen || rom.TextGeneration != 0,
                "Fresh A cannot read a sign while Feather owns airborne Link.");
            for (int wait = 0; _player.TopDownAirborne && wait < 48; wait++) Step();
            FailIf(_player.TopDownAirborne,"Sign input probe must land before its ground retry.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(); Step(pressed:1);
                FailIf(!_dialogue.IsOpen || rom.TextGeneration != repeat + 1 ||
                    rom[0xcba2] != 1 || rom[0xcba3] != 0x32,
                    "Grounded fresh A must open source TX_2e01 on repeated sign contact.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            }
            Step();
            FailIf(!_player.ApplyEnemyContactDamage(_player.Position + Vector2.Down * 16,1),
                "Sign recoil probe must enter the normal accepted-contact boundary.");
            rom[0xd02a] = 0x80; rom[0xd02b] = 0x22; rom[0xd02c] = 0; rom[0xd02d] = 0x0f;
            rom.ApplyLinkDamage(0xfe); rom[0xd02a] = 0;
            Step(pressed:1); Step(14);
            FailIf(_player.KnockbackFrames != 0 || _dialogue.IsOpen || rom.TextGeneration != 2,
                "Fresh A during recoil must leave the sign closed through terminal recovery.");
            Step(pressed:1);
            FailIf(!_dialogue.IsOpen || rom.TextGeneration != 3,
                "The first eligible ground update after recoil must read the same reachable sign.");
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            // Cast into the original floor beside the sign and pick up the
            // real ITEM$18 through its collision boundary. No grab-state or
            // carried-object override substitutes for the shared item handoff.
            _player.Face(Vector2I.Right); rom[0xd008] = 1;
            EquipSomariaMotionItem(rom,TreasureId.CaneOfSomaria,false);
            Step(pressed:2); Step(24);
            FailIf(rom.Blocks.Length != 1 || rom[rom.Blocks[0] + 4] != 3,
                "Sign carry probe must create its real solid Somaria block on the adjacent source floor.");
            EquipSomariaMotionItem(rom,TreasureId.Bracelet,false);
            Step(12,angle:8); Step(pressed:2); Step(13,held:2);
            FailIf(!_player.IsCarryingObject || rom[0xcc5a] != 0x83,
                "Sign carry probe must finish the real weight-$00 lift before its approach.");
            for (int wait = 0; _player.Position.X > 88 && wait < 32; wait++) Step(angle:24);
            Step(48,angle:0);
            FailIf(_currentRoom.GetMetatile(_player.Position + Vector2.Up * 8) != 0xf2 ||
                _currentRoom.IsSolid(_player.Position),
                $"Carried sign probe must reach original tile$35 through actual floor collision, Link={_player.Position}.");
            Step(); Step(pressed:1);
            FailIf(_dialogue.IsOpen || rom.TextGeneration != 3 || _player.IsCarryingObject || rom[0xcc5a] != 0,
                "Fresh A beside a sign while carrying must release the block without reading the sign.");
            Step(45); Step(pressed:1);
            FailIf(!_dialogue.IsOpen || rom.TextGeneration != 4,
                "The same sign must accept fresh A after the carried child is released and retired.");
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
        }
    }
}

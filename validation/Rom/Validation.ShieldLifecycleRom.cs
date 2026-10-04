using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShieldLifecycleRom() => ValidateShieldLifecycleRom(false);
    private void ValidateShieldAirborneRom() => ValidateShieldLifecycleRom(true);

    private void ValidateShieldLifecycleRom(bool airborne)
    {
        static T Private<T>(object owner, string field) => (T)owner.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int jumpUpdates in airborne ? new[] { 0, 1, 15, 29 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x91);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Shield, level);
            int oppositeItem = airborne ? TreasureId.Feather : TreasureId.Sword;
            _inventory.GiveTreasure(oppositeItem, 1);
            _inventory.EquipA(primary ? TreasureId.Shield : oppositeItem);
            _inventory.EquipB(primary ? oppositeItem : TreasureId.Shield);
            _player.WarpTo(new(120, 128));
            StepGameplayUpdates(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) || _collision.Collides(_player.Position),
                "Shield fixture must walk through room $4:$91's actual entrance floor.");
            _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
            OracleRandomState seed = _random.CaptureState();
            // This shared item/Link boundary executes the native shield parent
            // and its callers as well as the Cane tested by its existing owner.
            var rom = new SomariaRom(_saveData, seed, _currentRoom, direction, 120, 112);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int shield = primary ? 1 : 2;
            int sword = primary ? 2 : 1;
            int update = 0;
            void Step(int count = 1, int held = 0, int pressed = 0, Vector2? movement = null, int angle = 0xff, int directions = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, movement ?? Vector2.Zero, MenuRomActions(held | directions), MenuRomActions(pressed), batched, () =>
                {
                    rom.UpdateGameplay(edge, held | directions, angle, _entities.FrameCounter);
                    edge = 0;
                    string context = $"Shield L{level} A={primary} direction={direction} airborne={airborne}/{jumpUpdates} update={++update}";
                    FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                        Private<int>(_player, "_shieldParentButton") != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                        Private<bool>(_player, "_shieldParentInitialized") != (rom[0xd504] != 0),
                        context + $": raised/parent initialized runtime={_player.IsUsingShield}/{Private<int>(_player, "_shieldParentButton")}/{Private<bool>(_player, "_shieldParentInitialized")}, ROM=${rom[0xcc6f]:x2}/${rom[0xd500]:x2}/${rom[0xd503]:x2}/${rom[0xd504]:x2}.");
                    FailIf(_player.Position != new Vector2(rom[0xd00d], rom[0xd00b]) ||
                        _player.FacingVector != (Vector2I)OracleObjectMath.StrictCardinalVector(rom[0xd008] * 8),
                        context + ": movement/facing differs from native raised-shield Link.");
                    if (airborne)
                    {
                        FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                            _player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": full Link XY/Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Shield sound order differs.");
                    }
                    FailIf(!sounds.Requests.Where(id => id == SoundId.SndShield).SequenceEqual(rom.Sounds.Where(id => id == SoundId.SndShield)),
                        context + ": shield sound requested at a different initialization boundary.");
                    OracleRandomState random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs during the item interruption.");
                });
            }
            if (airborne)
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    int shieldsBefore = sounds.Requests.Count(id => id == SoundId.SndShield);
                    if (jumpUpdates == 0)
                    {
                        Step(1, shield, shield);
                        Step(8, shield);
                        Step(1, shield | sword, sword); // Opposite Feather.
                    }
                    else
                    {
                        Step(jumpUpdates, sword, sword);
                        FailIf(!_player.TopDownAirborne, "Shield air fixture did not launch through equipped Feather.");
                        Step(1, shield, shield);
                    }
                    FailIf(!_player.TopDownAirborne || !_player.IsUsingShield ||
                        sounds.Requests.Count(id => id == SoundId.SndShield) != shieldsBefore + 1,
                        "Ordinary Feather air must permit Shield, retaining its one initialization sound across launch.");
                    _dialogue.ShowMessage("Airborne Shield pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6, shield);
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(4, shield, movement: Vector2.Up, angle: 0, directions: 0x40);
                    Step(4, shield, movement: Vector2.Left, angle: 24, directions: 0x20);
                    Step(4, shield, movement: Vector2.Down, angle: 16, directions: 0x80);
                    Step(4, shield, movement: Vector2.Right, angle: 8, directions: 0x10);
                    Step(35, shield);
                    FailIf(_player.TopDownAirborne || !_player.IsUsingShield ||
                        sounds.Requests.Count(id => id == SoundId.SndShield) != shieldsBefore + 1,
                        "Held Shield did not survive Feather landing without another initialization sound.");
                    Step(); Step(3);
                    FailIf(_player.IsUsingShield, "Released Shield retained its airborne parent after landing.");
                }
                continue;
            }
            Step(1, shield, shield);
            Step(8, shield);
            Step(4, shield, movement: Vector2.Up, angle: 0, directions: 0x40);
            Step(4, shield, movement: Vector2.Left, angle: 24, directions: 0x20);
            Step(4, shield, movement: Vector2.Down, angle: 16, directions: 0x80);
            Step(4, shield, movement: Vector2.Right, angle: 8, directions: 0x10);
            _dialogue.ShowMessage("Shield pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            Step(5, shield);
            _dialogue.Close();
            rom[0xcba0] = 0;
            Step(3, shield);
            Step(1, shield | sword, sword);
            Step(24, shield | sword);
            Step(60, shield);
            Step(1);
            Step(4);
            Step(1, shield, shield); // Repeated initialization after release.
            Step(4, shield);
            Step(1);
        }
        GD.Print($"Validated clean-US Shield A/B parents and three levels in four directions, Feather air handoffs={airborne}, sound initialization, movement/turning, dialogue retention, Sword suppression/resumption, release and repeat through split/batched actual gameplay updates.");
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuSwimEntryRom()
    {
        int fixture = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool mermaid in new[] { false, true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (mermaid) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
            _inventory.EquipA(primary ? TreasureId.Bombchus : 0); _inventory.EquipB(primary ? 0 : TreasureId.Bombchus);
            // Declared source water$fa/collision$10 basin with a walkable shore.
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool water = x is >= 4 and <= 7 && y is >= 2 and <= 5;
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfa : (byte)0xa0, water ? (byte)0x10 : (byte)0, 0);
            }
            _player.WarpTo(new(56, 64)); _player.Face(Vector2I.Right);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 56, 64); rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, previousDirection = 0;
            void Step(int count = 1, int held = 0, int pressed = 0, int angle = 0xff)
            {
                int direction = angle == 8 ? 0x10 : angle == 24 ? 0x20 : 0;
                int edge = pressed | (direction & ~previousDirection); previousDirection = direction;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle),
                    MenuRomActions(held | direction), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held | direction, angle, _entities.FrameCounter); edge = 0; update++;
                    string context = $"Bombchu shore A={primary}, Mermaid={mermaid}, update={update}";
                    CompareSomariaMotionRom(rom, context);
                    int parent = rom[0xd300] != 0 && rom[0xd301] == 0x0d ? 0xd300 :
                        rom[0xd400] != 0 && rom[0xd401] == 0x0d ? 0xd400 : 0;
                    int child = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .SingleOrDefault(slot => rom[slot] != 0 && rom[slot + 1] == 0x0d);
                    var runtime = _entities.Entities<BombchuItem>().SingleOrDefault();
                    FailIf(_entities.BombchuParent.Active != (parent != 0) || (runtime != null) != (child != 0) ||
                        _inventory.Bombchus != rom[0xc6b3] || _player.TopDownSwimmingState != (rom[0xcc5d] & 15),
                        context + ": parent/child, packed-BCD ammo or swimming differs.");
                    if (parent != 0)
                        FailIf(_entities.BombchuParent.Counter != rom[parent + 0x20] || _entities.BombchuParent.Parameter != rom[parent + 0x21],
                            context + ": fresh ground parent clock differs.");
                    if (runtime != null)
                        FailIf(runtime.ItemState != rom[child + 4] || runtime.Counter2 != rom[child + 7] ||
                            runtime.Position != new Vector2(rom[child + 0xd], rom[child + 0xb]) ||
                            (ushort)runtime.ZFixed != rom.Word(child + 0xe), context + ": initialized child state/fuse/position differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.SequenceEqual(rom.Sounds), context + ": ordered sound/RNG differs.");
                    FailIf(_currentRoom.IsSolid(_player.Position), context + ": shore approach entered solid geometry.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int began = update;
                while (!_player.TopDownSwimming && update - began < 60) Step(3, angle: 8);
                FailIf(!_player.TopDownSwimming, "Bombchu swim fixture must enter water through its shore.");
                Step(24); int ammo = _inventory.Bombchus;
                Step(1, button, button); Step(8, button);
                _dialogue.ShowMessage("Bombchu surface-water pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(3, button); _dialogue.Close(); rom[0xcba0] = 0; Step(3, button);
                FailIf(_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 0 || _inventory.Bombchus != ammo,
                    "Surface swimming must reject Bombchu without spending ammo.");
                began = update;
                while (_player.TopDownSwimming && update - began < 90) { Step(1, button); Step(6, button, angle: 24); }
                FailIf(_player.TopDownSwimming, "Bombchu swim fixture must return through its walkable shore.");
                Step(3, button);
                FailIf(_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 0,
                    "Holding a rejected Bombchu button through water exit must require a fresh edge.");
                Step(); Step(1, button, button);
                FailIf(!_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 1,
                    "Fresh shore input must initialize Bombchu again.");
                Step(3);
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Step();
            }
        }
        GD.Print("Compared Bombchu A/B Flippers/Mermaid surface-water approach, silent rejection, full fixed Link/parent/child, retained ammo, text, held exit/fresh use and cancellation/repeat through split/batched gameplay.");
    }
}

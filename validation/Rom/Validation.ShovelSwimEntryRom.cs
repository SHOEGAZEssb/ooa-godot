using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateShovelSwimEntryRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool mermaid in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (mermaid) _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Shovel, 0);
            _inventory.EquipA(primary ? TreasureId.Shovel : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Shovel);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool water = x is >= 4 and <= 7 && y is >= 2 and <= 5;
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    water ? (byte)0xfa : (byte)0xa0, water ? (byte)0x10 : (byte)0, 0);
            }
            _player.WarpTo(new(56, 64)); _player.Face(Vector2I.Right);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1, 56, 64);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0, precedingDirection = 0;
            void Step(int count = 1, int held = 0, int pressed = 0, int angle = 0xff)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int direction = angle == 8 ? 0x10 : angle == 24 ? 0x20 : 0;
                int edge = pressed | (direction & ~precedingDirection);
                precedingDirection = direction;
                StepGameplayUpdates(count, movement, MenuRomActions(held | direction), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held | direction, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Shovel swim A={primary} Mermaid={mermaid} update={++update}";
                    bool parent = Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x15);
                    int[] children = Enumerable.Range(0xd6, 10).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x15).ToArray();
                    FailIf(_player.IsUsingShovel != parent || _shovel.ChildActive != (children.Length == 1) ||
                        _player.TopDownSwimmingState != (rom[0xcc5d] & 0x0f) ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        context + $": parent/child/swimming/full fixed Link position differs: runtime={_player.IsUsingShovel}/{_shovel.ChildActive}/{_player.TopDownSwimmingState}/{_player.PrecisePosition}, native={parent}/{children.Length}/{rom[0xcc5d]:x2}/{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}, var2f=${rom[0xd02f]:x2}.");
                    if (_player.TopDownSwimming)
                    {
                        // SpecialObject.counter1 holds the entry delay in
                        // state 2 and the Flippers burst counter in state 3.
                        int counter = _player.TopDownSwimmingState == 2 || mermaid
                            ? _player.TopDownSwimmingEntryCounter : _player.TopDownSwimBurstCounter;
                        FailIf(_player.TopDownSwimAngle != rom[0xd009] || _player.TopDownSwimSpeedRaw != rom[0xd010] ||
                            _player.TopDownSwimTargetSpeedRaw != rom[0xd011] || counter != rom[0xd006],
                            context + $": swim angle/speed/target/counter runtime={_player.TopDownSwimAngle:x2}/{_player.TopDownSwimSpeedRaw}/{_player.TopDownSwimTargetSpeedRaw}/{counter}, native={rom[0xd009]:x2}/{rom[0xd010]}/{rom[0xd011]}/{rom[0xd006]}.");
                    }
                    if (children.Length == 1)
                    {
                        int child = children[0];
                        FailIf(_shovel.ChildCounter != rom[child + 6] || _shovel.ChildPosition !=
                            new Vector2(rom.Word(child + 0x0c) / 256f, rom.Word(child + 0x0a) / 256f),
                            context + ": independently owned shovel child counter/position differs.");
                    }
                    FailIf(!sounds.Requests.Where(id => id is SoundId.SndDig or SoundId.SndClink)
                        .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndDig or SoundId.SndClink)),
                        context + ": silent swim rejection or resumed digging sound differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                    FailIf(_currentRoom.IsSolid(_player.Position), context + ": basin approach entered solid room geometry.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(1, button, button); Step(24);
                FailIf(_player.IsUsingShovel || _shovel.ChildActive, "Shovel did not retire before entering water.");
                int began = update;
                while (!_player.TopDownSwimming && update - began < 90) Step(3, angle: 8);
                FailIf(!_player.TopDownSwimming, "Shovel swim fixture could not approach water through its shore.");
                Step(48);
                int clinks = sounds.Requests.Count(id => id == SoundId.SndClink);
                Step(1, button, button); Step(12, button);
                _dialogue.ShowMessage("Shovel swimming pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, button);
                _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, button); Step();
                FailIf(_player.IsUsingShovel || _shovel.ChildActive || sounds.Requests.Count(id => id == SoundId.SndClink) != clinks,
                    "Top-down swimming accepted a shovel parent or tile attempt.");
                began = update;
                while (_player.TopDownSwimming && update - began < 90)
                {
                    // Mermaid swimming accelerates only on direction edges;
                    // release/repress while retaining the equipped-item button.
                    Step(1, button);
                    Step(6, button, angle: 24);
                }
                FailIf(_player.TopDownSwimming, "Shovel swim fixture could not return to its walkable shore.");
                Step(3, button); Step();
                Step(1, button, button);
                FailIf(!_player.IsUsingShovel, "Fresh ground input failed to reinitialize shovel after swimming.");
                Step(24); Step();
                FailIf(sounds.Requests.Count(id => id == SoundId.SndClink) != clinks + 1,
                    "Post-swim ground shovel did not perform exactly one native tile attempt.");
            }
        }
        GD.Print("Validated clean-US Shovel A/B top-down Flippers/Mermaid basin approach, full fixed swimming, silent rejection/dialogue, held exit/release, fresh ground reinitialization and repeated traversal through split/batched gameplay.");
    }
}

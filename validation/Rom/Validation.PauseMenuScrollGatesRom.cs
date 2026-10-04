using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuScrollGatesRom() => RunPauseMenuScrollGatesRom(false);
    private void ValidateHudHeartBeepScrollRom() => RunPauseMenuScrollGatesRom(true);

    private void RunPauseMenuScrollGatesRom(bool lowHealthWarning)
    {
        var graphics = new ScreenTransitionGraphicsDatabase();
        int hostCase1 = 0;
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (var route in new[] { (Group: 0, Source: 0x11, Target: 0x12), (Group: 4, Source: 0x07, Target: 0x03) })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            if (lowHealthWarning)
            {
                _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
                _saveData.WriteWramByte(0xc622, 63);
            }
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(route.Group, route.Source); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            bool large = _currentRoom.Width > 160;
            int loadedUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            Vector2I vector = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left };
            _player.WarpTo(new(vector.X < 0 ? 6.75f : vector.X > 0 ? _currentRoom.Width - 5.25f : 80.75f,
                vector.Y < 0 ? 6.25f : vector.Y > 0 ? _currentRoom.Height - 6.75f : 64.25f));
            _player.Face(Vector2I.Down);
            _transitions.BeginScroll(_player, vector, route.Target);
            int total = _transitions.ScrollTotalFrames;
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(large, direction, (int)(_player.PrecisePosition.X * 256),
                (int)(_player.PrecisePosition.Y * 256), unique, loadedUnique);
            var menus = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    // Two bounded native owners: menu dispatch reads the
                    // preceding scroll mode before the native cutscene advances.
                    // This publication comes from ScrollRom, not runtime state.
                    menus[0xcd00] = scroll[0xcd00];
                    menus.Update(edge, held, _saveData.ReadWramByte(0xc622)); edge = 0;
                    string context = $"Scroll menu gates large={large} direction={direction} intro={introDone} keys=${keys:x2} batch={batched} update={++update}";
                    FailIf(_menuLifecycle.IsActive != (menus[0xcbcb] != 0),
                        context + ": native scroll-mask menu eligibility differs.");
                    FailIf(!sounds.Requests.Where(id => id is 0x54 or 0x55 or 0x5a || lowHealthWarning && id == 0x60).SequenceEqual(menus.Sounds),
                        context + ": scrolling/IntroDone/opening cue precedence differs.");
                    if (scroll[0xcd04] != 2)
                    {
                        scroll.Update();
                        Vector2 native = new(scroll.Word(0xd00c) / 256.0f, scroll.Word(0xd00a) / 256.0f);
                        Vector2 actual = new(((int)(_player.PrecisePosition.X * 256) & 0xffff) / 256.0f,
                            ((int)(_player.PrecisePosition.Y * 256) & 0xffff) / 256.0f);
                        FailIf(native != actual || _transitions.ScrollActive != (scroll[0xcd04] != 2),
                            context + ": menu input changed native scroll motion/completion.");
                    }
                });
            }
            // Fresh edges alternate with releases through setup, motion and
            // row cleanup; batched runs also retain each edge for several ticks.
            for (int remaining = total; remaining > 0;)
            {
                int count = System.Math.Min(3, remaining);
                Step(count, keys, keys); remaining -= count;
                if (remaining > 0) { Step(); remaining--; }
            }
            FailIf(_menuLifecycle.IsActive || scroll[0xcd00] != 1 || sounds.RequestsFor(SoundId.SndError) != 0,
                "Scroll mode $08 must reject menu edges before IntroDone; its finisher publishes allowed mode $01.");
            if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != 0,
                "Native scroll setup/motion/cleanup must suppress the global low-health warning.");
            Step(3, held: keys); Step();
            int warningsBefore = sounds.RequestsFor(0x60);
            if (lowHealthWarning) _saveData.WriteWramByte(0xc622, 127);
            Step(1, keys, keys);
            if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != warningsBefore + (introDone ? 1 : 0),
                "Post-scroll $80 warning must precede accepted input but follow the IntroDone error gate.");
            FailIf(_menuLifecycle.IsActive != introDone || sounds.RequestsFor(SoundId.SndError) != (introDone ? 0 : 1),
                "After scroll completion, an old held edge must remain consumed and a fresh edge must restore the native menu/IntroDone gate.");
            if (_menuLifecycle.IsActive)
            {
                if (keys == 4) _mapMenu.CloseImmediatelyForValidation();
                else _inventoryMenu.CloseImmediatelyForValidation();
            }
            if (lowHealthWarning)
            {
                menus[0xcbcb] = 0; // Explicit immediate cancellation, matching the runtime close above.
                _saveData.WriteWramByte(0xc622, 191);
                Step();
                FailIf(sounds.RequestsFor(0x60) != warningsBefore + (introDone ? 2 : 1),
                    "The next normal $c0 dispatch must resume warnings after scroll/menu cancellation, without an IntroDone requirement for idle input.");
            }
        }
        GD.Print("Validated clean-US small/large scroll menu gates in all directions: native $08/$01 mode publication, IntroDone cue precedence, fresh/held edges across all scroll phases, retained completion edges, post-scroll opening and split/batched gameplay.");
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusItemInterruptionsRom()
    {
        int fixture = 0;
        foreach (int item in new[] { TreasureId.Sword, TreasureId.Shield, TreasureId.CaneOfSomaria,
            TreasureId.Boomerang, TreasureId.Shovel, TreasureId.Bracelet, TreasureId.Bombs,
            TreasureId.Shooter, TreasureId.SwitchHook, TreasureId.Harp, TreasureId.Flute, TreasureId.BiggoronSword,
            TreasureId.Bombchus })
        foreach (int selection in Enumerable.Range(0, item is TreasureId.Harp or TreasureId.Flute ? 4 : 1))
        foreach (bool primary in selection == 0 ? new[] { false, true } : new[] { (selection & 1) != 0 })
        foreach (bool batched in RomHostSchedules(item == TreasureId.Harp && selection == 0 && !primary ? 0 : fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0x91); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20); _inventory.SelectSatchelSeeds(2);
            _inventory.GiveTreasure(item, item is TreasureId.Bombs or TreasureId.Bombchus ? 0x20 : item is TreasureId.Harp or TreasureId.Flute ? 0 : 1);
            if (item == TreasureId.Harp && selection != 0) { EnsureHarpAndSongs(); _inventory.SelectHarpSong(selection); }
            if (item == TreasureId.Flute) _saveData.WriteWramByte(0xc6b5, (byte)selection);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 0x20); _inventory.SelectShooterSeeds(0);
            _inventory.EquipA(primary ? TreasureId.SeedSatchel : item);
            _inventory.EquipB(primary ? item : TreasureId.SeedSatchel);
            _player.WarpTo(new(120, 128)); StepGameplayUpdates(16, Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) || _currentRoom.IsSolid(_player.Position),
                "Pegasus competing-item fixture must enter room $4:$91 through its actual floor.");
            _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 112);
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int satchel = primary ? 1 : 2, other = primary ? 2 : 1, previous = 0, update = 0;
            void Step(int count = 1, int held = 0, int angle = 0xff)
            {
                Vector2 move = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int buttons = held | (angle == 8 ? 0x10 : angle == 24 ? 0x20 : 0);
                int edge = buttons & ~previous; previous = buttons;
                StepGameplayUpdates(count, move, MenuRomActions(buttons), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, buttons, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Pegasus competing ITEM${item:x2} selection=${selection:x2} A={primary} batch={batched} update={++update}";
                    FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c) || _inventory.PegasusSeeds != rom[0xc6bb],
                        context + $": timer/ammo runtime=${_seedSatchel.Pegasus.RawCounter:x4}/${_inventory.PegasusSeeds:x2}, native=${rom.Word(0xcc6c):x4}/${rom[0xc6bb]:x2}, parents=${rom[0xd201]:x2}/${rom[0xd204]:x2},${rom[0xd301]:x2}/${rom[0xd304]:x2}.");
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _player.IsUsingShield != (rom[0xcc6f] != 0), context + ": Link movement/facing/Shield differs.");
                    ComparePegasusDustRom(rom, false, context);
                    if (item is TreasureId.Harp or TreasureId.Flute)
                        FailIf(_player.IsUsingHarp != (rom[0xd500] != 0) || _harp.PlayingInstrument != rom[0xcc8d],
                            context + ": instrument parent/publication differs.");
                    if (!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds))
                        FailIf(true, context + $": sounds runtime=[{string.Join(',', sounds.Requests.Where(cue => cue != SoundId.SndText).Select(cue => cue.ToString("x2")))}], native=[{string.Join(',', rom.Sounds.Select(cue => cue.ToString("x2")))}].");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + $": shared RNG runtime=${random.Rng1:x2}/${random.Rng2:x2}/{random.Calls - seed.Calls}, native=${rom[0xff94]:x2}/${rom[0xff95]:x2}/{rom.RandomCalls} differs.");
                });
            }
            void ReleaseParents()
            {
                Step();
                int remaining = 280;
                while (Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0) && remaining-- > 0) Step();
                FailIf(Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0),
                    "Released competing parents exceeded their bounded native lifetime.");
                if (_dialogue.IsOpen)
                {
                    // The instrument owns its empty-Flute/no-effect text.
                    // Text printing has separate ROM fixtures; declare its
                    // cancellation before testing the next item handoff.
                    _dialogue.Close(); rom[0xcba0] = 0; Step();
                }
            }
            // Selector 2 admits the Satchel beside existing lower parents;
            // the $22 branch clears without replacing their Link animation.
            Step(2, other); Step(3, other | satchel);
            _dialogue.ShowMessage("Pegasus item pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3, other | satchel); _dialogue.Close(); rom[0xcba0] = 0;
            Step(20, other | satchel); Step(1, other); Step(1, other | satchel);
            ReleaseParents(); Step(1, satchel);
            FailIf(!_seedSatchel.Pegasus.Active, $"ITEM${item:x2}: fresh Satchel input must activate after the competing parent releases.");
            // Once active, neither another parent nor another seed request
            // resets the counter. Dust continues while text freezes Link's timer.
            Step(2, satchel | other); Step(4, other);
            _dialogue.ShowMessage("Pegasus repeat pause.", _player.Position.Y); rom[0xcba0] = 1;
            Step(3, other); _dialogue.Close(); rom[0xcba0] = 0;
            Step(20, other); Step(1, satchel | other); ReleaseParents();
            Step(6, angle: 8); Step(6, angle: 24); Step();
        }
        ComparePegasusSatchelSwitchRom();
        ComparePegasusPunchRom();
        GD.Print("Validated native Pegasus competing Sword/Shield/Cane/Biggoron/Boomerang/Bombchu/Shovel/Bracelet/Bomb/Shooter/Switch Hook/Harp/Flute parents: A/B, retained/rejected edges, active timer/ammo, dialogue, dust and movement/sound/RNG through split/batched gameplay.");
    }
}

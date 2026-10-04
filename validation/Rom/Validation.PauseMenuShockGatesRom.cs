using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuShockGatesRom() => RunPauseMenuShockGatesRom(false);
    private void ValidateHudHeartBeepShockRom() => RunPauseMenuShockGatesRom(true);

    private void RunPauseMenuShockGatesRom(bool lowHealthWarning)
    {
        int hostCase1 = 0;
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (int elapsed in new[] { 0, 1, 44, 45 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x06); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.RingBox, 1);
            _inventory.GrantAppraisedRingForDebug((int)RingId.GreenHoly);
            _inventory.SetRingBoxSlotFromList(0, (int)RingId.GreenHoly); _inventory.EquipRingAt(0);
            if (lowHealthWarning)
            {
                _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
                _saveData.WriteWramByte(0xc622, unchecked((byte)(63 - elapsed)));
            }
            _inventory.EquipA(0); _inventory.EquipB(0);
            var landing = new TimeWarpLandingDatabase();
            Vector2 position = Enumerable.Range(1, 6).SelectMany(y => Enumerable.Range(1, 8)
                .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => !_collision.Collides(point) && landing.CanStandOnTile(_currentRoom, point, false) &&
                    !WarpDatabase.IsWarpTile(_currentRoom.ActiveCollisions, _currentRoom.GetMetatile(point)));
            _player.WarpTo(position);
            var seed = _random.CaptureState();
            var rom = new MenuRom(_saveData, _currentRoom);
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            var sounds = _sound.AttachPlayRequestAudit();
            _player.ApplyElectricShock(position - Vector2.Right * 32);
            // Declared collisionEffect36 outcome: Green Holy Ring preserves
            // health while still publishing the pending shock request.
            rom[0xccdb] = 1;
            int update = 0;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette(); rom.AdvanceElectricShock();
                    rom.Update(edge, held, _saveData.ReadWramByte(0xc622)); edge = 0;
                    if (rom[0xcbcb] == 0) rom.UpdateScreenShake();
                    string context = $"Shock menu gates intro={introDone} keys=${keys:x2} elapsed={elapsed} batch={batched} update={++update}";
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0) ||
                        _player.ElectricShockActive != (rom[0xccdb] != 0) || _player.ElectricShockCounter != rom[0xccdc],
                        context + $": menu/shock ownership or counter differs: runtime={_menuLifecycle.IsActive}/{_player.ElectricShockActive}/{_player.ElectricShockCounter}, native=${rom[0xcbcb]:x2}/${rom[0xccdb]:x2}/{rom[0xccdc]}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": shock/IntroDone/menu cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    FailIf(_player.HealthQuarters != rom[0xc6aa], context + ": shock/menu changed protected health.");
                    FailIf(_entities.ScreenShakeCounter != rom[0xcd18] ||
                        _entities.HorizontalScreenShakeCounter != rom[0xcd19],
                        context + ": shock/menu screen-shake counters differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": protected shock/menu RNG differs from native execution.");
                });
            }
            if (elapsed != 0) Step(elapsed);
            Step(1, keys, keys);
            bool ended = elapsed == 45;
            if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != (introDone && ended ? 1 : 0),
                "Shock must clear its restriction on its terminal update before the global $40 warning/menu edge.");
            FailIf(_menuLifecycle.IsActive != (introDone && ended) ||
                sounds.RequestsFor(SoundId.SndError) != (introDone ? 0 : 1),
                "Shock initialization and its exact $2d-counter zero update must precede the menu/IntroDone predicates.");
            if (!ended)
            {
                Step(46 - update, held: keys);
                FailIf(_menuLifecycle.IsActive || _player.ElectricShockActive,
                    "Shock completion must clear its gate without inventing another held menu edge.");
                Step(); Step(1, keys, keys);
                FailIf(_menuLifecycle.IsActive != introDone ||
                    sounds.RequestsFor(SoundId.SndError) != (introDone ? 0 : 2),
                    "A fresh post-shock edge did not restore native menu eligibility/IntroDone cues.");
            }
            if (_menuLifecycle.IsActive)
            {
                Step(22);
                int cancel = keys == 8 ? 8 : 2;
                Step(1, cancel, cancel); Step(22);
            }
        }
        GD.Print("Validated clean-US shock/menu initialization order, protected collision outcome, $2d-counter terminal update, pending/active/last/zero gates, IntroDone/sound precedence, held/repeated edges and full closing through split/batched gameplay.");
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuHarpGatesRom() => RunPauseMenuInstrumentGatesRom(false);
    private void ValidatePauseMenuFluteGatesRom() => RunPauseMenuInstrumentGatesRom(true);
    private void ValidatePauseMenuHarpCompletionRom() => RunPauseMenuInstrumentGatesRom(false, true);
    private void ValidatePauseMenuFluteTextCompletionRom() => RunPauseMenuInstrumentGatesRom(true,
        lowHealthWarning: true, naturalTextCompletion: true);
    private void ValidateHudHeartBeepInstrumentRom()
    {
        RunPauseMenuInstrumentGatesRom(false, lowHealthWarning: true);
        RunPauseMenuInstrumentGatesRom(true, lowHealthWarning: true);
    }

    private void RunPauseMenuInstrumentGatesRom(bool flute, bool completion = false, bool lowHealthWarning = false,
        bool naturalTextCompletion = false)
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool introDone in new[] { true, false })
        foreach (int song in naturalTextCompletion ? new[] { 0 } : Enumerable.Range(0, 4))
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            if (lowHealthWarning)
            {
                _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
                _saveData.WriteWramByte(0xc622, 55); // Menu edge lands on global $40.
            }
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            if (flute)
            {
                _inventory.GiveTreasure(TreasureId.Flute, 0);
                _saveData.WriteWramByte(0xc6b5, (byte)song); // Native wFluteIcon $00-$03.
            }
            else if (song == 0) _inventory.GiveTreasure(TreasureId.Harp, 0);
            else { EnsureHarpAndSongs(); _inventory.SelectHarpSong(song); }
            LoadValidationRoom(completion ? 4 : 0, completion ? 0x91 : 0x06); _entities.Clear();
            int item = flute ? TreasureId.Flute : TreasureId.Harp;
            _inventory.EquipA(primary ? item : 0);
            _inventory.EquipB(primary ? 0 : item);
            var landing = new TimeWarpLandingDatabase();
            Vector2 position = Enumerable.Range(1, 6).SelectMany(y => Enumerable.Range(1, 8)
                .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => !_collision.Collides(point) && landing.CanStandOnTile(_currentRoom, point, false) &&
                    !WarpDatabase.IsWarpTile(_currentRoom.ActiveCollisions, _currentRoom.GetMetatile(point)));
            _player.WarpTo(position, recordSafe: false); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState();
            var rom = new HarpRom(_saveData, seed, _currentRoom.Group, _currentRoom.Id,
                _currentRoom.TilesetFlags, (int)position.X, (int)position.Y);
            rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            var menus = rom.CreateMenuFixture();
            // Saved-game initialization has already synchronized the HUD.
            // Inventory graphics reload the native status bar while opening.
            rom[0xcbe4] = rom[0xc6aa];
            rom[0xcbe5] = rom[0xc6ad]; rom[0xcbe6] = rom[0xc6ae];
            rom[0xcbe9] = 0xff; rom[0xcc39] = 0xff;
            if (completion)
            {
                menus.LoadRoomTileset();
                FailIf(rom[0xcc39] != 4, "Harp completion room $4:$91 must load native dungeon $04.");
                menus.LoadDungeon(4);
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++) rom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(new(x * 16 + 8, y * 16 + 8));
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, button = primary ? 1 : 2;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    // mainThread updates palette/menus before the object pass.
                    // All native owners share one memory image; a menu freezes
                    // the existing Harp parent and note interactions beneath it.
                    menus.AdvancePalette();
                    menus.Update(edge, held, _saveData.ReadWramByte(0xc622));
                    if (rom[0xcbcb] == 0) rom.Update(edge, held, _entities.FrameCounter);
                    if (lowHealthWarning) menus.AdvanceText();
                    edge = 0;
                    string context = $"Instrument menu gates flute={flute} completion={completion} A={primary} intro={introDone} song/icon=${song:x2} keys=${keys:x2} batch={batched} update={++update}";
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0) ||
                        _player.IsUsingHarp != (rom[0xd500] != 0) || _harp.PlayingInstrument != rom[0xcc8d] ||
                        _player.Position != position || _harp.NoteSpawnCount != rom.NoteCount,
                        context + $": menu/parent/instrument/position/note ownership differs: runtime={_menuLifecycle.IsActive}/{_player.IsUsingHarp}/${_harp.PlayingInstrument:x2}, native=${rom[0xcbcb]:x2}/${rom[0xd500]:x2}/${rom[0xcc8d]:x2}.");
                    if (lowHealthWarning) FailIf(DialogueOpen != (rom[0xcba0] != 0),
                        context + ": instrument-owned text lifetime differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls, context + ": shared Harp/note RNG differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": Harp/menu/error sound order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                });
            }
            Step(1, button, button); Step(7, held: button);
            FailIf(!_player.IsUsingHarp, "Instrument menu gate fixture did not start the actual equipped Harp parent.");
            Step(1, keys, keys); Step(3, held: keys);
            bool permitted = !flute && introDone && song == 0;
            bool introError = !flute && !introDone && song == 0;
            if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != (permitted ? 1 : 0),
                "The empty-Harp byte permits the $40 warning before menu input; nonzero songs/Flute and IntroDone errors suppress it.");
            FailIf(_menuLifecycle.IsActive != permitted ||
                sounds.RequestsFor(SoundId.SndError) != (introError ? 1 : 0),
                "Native menu eligibility uses wLinkPlayingInstrument before IntroDone, including zero for the empty Harp.");
            if (permitted)
            {
                int parentCounter = rom[0xd520];
                int pose = _player.HarpPoseFrame;
                Step(22);
                FailIf(!_menuLifecycle.IsActive || rom[0xd520] != parentCounter || _player.HarpPoseFrame != pose,
                    "An opened menu advanced the existing empty-Harp parent animation.");
                int cancel = keys == 8 ? 8 : 2;
                Step(1, cancel, cancel); Step(22);
                FailIf(_menuLifecycle.IsActive || !_player.IsUsingHarp,
                    "Closing the menu failed to resume the retained empty-Harp parent.");
                Step(); Step(1, keys, keys); Step(22);
                Step(1, cancel, cancel); Step(22);
                FailIf(_menuLifecycle.IsActive || !_player.IsUsingHarp,
                    "Repeated menu opening/cancellation did not preserve the independently owned empty-Harp parent.");
            }
            else
            {
                Step(); Step(1, keys, keys);
                FailIf(_menuLifecycle.IsActive || sounds.RequestsFor(SoundId.SndError) != (introError ? 2 : 0),
                    "Repeated blocked Harp menu input changed instrument/IntroDone precedence.");
                Step(23, held: keys);
                FailIf(_harp.NoteSpawnCount != 1,
                    "Rejected menu edges must allow the native instrument's first floating-note/RNG update.");
            }
            if (lowHealthWarning)
            {
                Step(65);
                if (flute && song == 0)
                {
                    FailIf(!DialogueOpen || rom[0xcba0] == 0 || _harp.PlayingInstrument != 0xff,
                        "The empty Flute's native text must retain its completed $ff instrument signal.");
                    if (naturalTextCompletion)
                    {
                        // Native text keeps the completed instrument byte for
                        // printing, the exit press and its separate closing update.
                        while (rom[0xcba0] != 0 && update < 512)
                        {
                            bool lineDone = menus.Text(0xd0d0) >= 16 || menus.Text(0xd400 + menus.Text(0xd0d0)) == 0;
                            bool waiting = menus.TextState is 5 or 0x0f ||
                                (menus.Text(0xd0c1) & 2) != 0 && lineDone;
                            Step(waiting || menus.TextState == 0x10 ? 1 : 4,
                                waiting ? 1 : keys, waiting ? 1 : keys);
                        }
                        FailIf(DialogueOpen || rom[0xcba0] != 0 || _harp.PlayingInstrument != 0xff,
                            "Natural Flute text completion must release text while retaining its completed instrument byte.");
                    }
                    else { _dialogue.Close(); rom[0xcba0] = 0; } // Explicit cancellation boundary.
                    Step(1, keys, keys); // Menus see the retained byte before normal Link clears it.
                    FailIf(_menuLifecycle.IsActive || _harp.PlayingInstrument != 0,
                        "Canceling Flute text must defer fresh menu eligibility until normal Link clears its instrument byte.");
                    Step(); Step(1, keys, keys);
                    FailIf(_menuLifecycle.IsActive != introDone,
                        "A subsequent fresh edge did not restore the native Flute/menu handoff.");
                    if (_menuLifecycle.IsActive)
                    {
                        Step(22); int cancel = keys == 8 ? 8 : 2;
                        Step(1, cancel, cancel); Step(22);
                    }
                }
            }
            if (completion)
            {
                // Dungeon flags reject the tune effect after the native
                // animation completes; no portal/timewarp script is supplied.
                int updatesRemaining = 260 - (int)typeof(Player).GetField("_harpActionUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_player)!;
                Step(updatesRemaining);
                FailIf(_player.IsUsingHarp || rom[0xd500] != 0 || _harp.PlayingInstrument != song ||
                    _harp.NoteSpawnCount != 8 || rom[0xcc8a] != 0,
                    "Harp's exact 260-update completion must clear the parent/freeze mask and retain its final instrument byte.");
                int errorsBefore = sounds.RequestsFor(SoundId.SndError);
                Step(1, keys, keys);
                FailIf(_menuLifecycle.IsActive != (introDone && song == 0) ||
                    sounds.RequestsFor(SoundId.SndError) != errorsBefore + (!introDone && song == 0 ? 1 : 0),
                    "The update after parent completion must gate menus using the retained instrument byte before Link clears it.");
                if (_menuLifecycle.IsActive)
                {
                    Step(22);
                    int cancel = keys == 8 ? 8 : 2;
                    Step(1, cancel, cancel); Step(22);
                }
                else
                {
                    Step();
                    Step(1, keys, keys);
                    FailIf(_menuLifecycle.IsActive != introDone ||
                        sounds.RequestsFor(SoundId.SndError) != errorsBefore + (introDone ? 0 : song == 0 ? 2 : 1),
                        "Link's subsequent normal update must restore the zero-instrument menu/IntroDone gate for a fresh edge.");
                    if (_menuLifecycle.IsActive)
                    {
                        Step(22);
                        int cancel = keys == 8 ? 8 : 2;
                        Step(1, cancel, cancel); Step(22);
                    }
                }
            }
        }
        GD.Print($"Validated clean-US instrument menu eligibility flute={flute} completion={completion} low-health-warning={lowHealthWarning} for all four song/icon bytes, A/B, Start/Select/chord, IntroDone cue precedence, held/repeated edges, empty-Harp menu freeze/resume/cancellation, retained completion byte, note/sound/RNG ownership and split/batched gameplay.");
    }
}

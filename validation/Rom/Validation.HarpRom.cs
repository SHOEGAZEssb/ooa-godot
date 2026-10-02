using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHarpPlaybackRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int song in new[] { 0, 1, 2, 3 })
            RunHarpRom(batched, primary, song, 0, 0x06);
        GD.Print("Validated executed-US ITEM_HARP playback, all four selections, A/B input, animation/OAM, floating-note motion/lifetime, shared RNG, sound and completion through split/batched gameplay updates.");
    }

    private void ValidateHarpEffectsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            RunHarpRom(batched, true, 1, 0, 0xcd);
            foreach (int song in new[] { 1, 2, 3 })
            {
                RunHarpRom(batched, true, song, 1, 0x06);
                RunHarpRom(batched, true, song, 4, 0x91);
            }
        }
        GD.Print("Validated executed-US Harp present/past and indoor tune gates, TX_5110 and immediate CUTSCENE_TIMEWARP handoff.");
    }

    private void RunHarpRom(bool batched, bool primary, int song, int group, int room)
    {
        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
        _inventory.GiveTreasure(TreasureId.Sword, 1);
        if (song == 0) _inventory.GiveTreasure(TreasureId.Harp, 0);
        else EnsureHarpAndSongs();
        LoadValidationRoom(group, room);
        bool placedPortal = group == 0 && room == 0xcd;
        _saveData.SetRoomFlag(group, room, 0x08, false);
        if (!placedPortal) _entities.Clear();
        _inventory.EquipA(primary ? TreasureId.Harp : TreasureId.Sword);
        _inventory.EquipB(primary ? TreasureId.Sword : TreasureId.Harp);
        // Song $00 is the source's empty Harp state before a tune award.
        if (song != 0) _inventory.SelectHarpSong(song);
        var landing = new TimeWarpLandingDatabase();
        Vector2 position = Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
            .SelectMany(y => Enumerable.Range(1, _currentRoom.WidthInTiles - 2).Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
            .First(point => !_collision.Collides(point) &&
                landing.CanStandOnTile(_currentRoom, point, false) &&
                !WarpDatabase.IsWarpTile(_currentRoom.ActiveCollisions, _currentRoom.GetMetatile(point)) &&
                (group > 1 || landing.CanStandOnTile(_rooms.GetRoom(group ^ 1, room), point, false)));
        _player.WarpTo(position, recordSafe: false);
        _player.Face(Vector2I.Down);
        var seed = _random.CaptureState();
        var audit = _sound.AttachPlayRequestAudit();
        var rom = new HarpRom(_saveData, seed, group, room, _currentRoom.TilesetFlags,
            (int)position.X, (int)position.Y);
        for (int y = 0; y < _currentRoom.HeightInTiles; y++)
        for (int x = 0; x < _currentRoom.WidthInTiles; x++)
            rom[0xcf00 + y * 16 + x] = _currentRoom.GetMetatile(new(x * 16 + 8, y * 16 + 8));
        if (placedPortal) rom.InitializePortal();
        byte[] inventory = Enumerable.Range(0xc688, 0x38).Select(_saveData.ReadWramByte).ToArray();
        int update = 0;
        void Compare()
        {
            string context = $"Harp ${group:x1}:${room:x2} song=${song:x2} A={primary} batch={batched} update={update}";
            bool playing = rom[0xd500] != 0;
            FailIf(_player.IsUsingHarp != playing || _harp.IsPlaying != playing ||
                _harp.PlayingInstrument != rom[0xcc8d], $"{context}: parent/instrument runtime={_player.IsUsingHarp}/{_harp.IsPlaying}/${_harp.PlayingInstrument:x2}, ROM={playing}/${rom[0xcc8d]:x2}; grounded={_player.IsGroundedForFloorButton}, transition={IsTransitioning}, text={DialogueOpen}, event={_roomEvents.Active}, physics={_player.IsPhysicsProcessing()}, position={_player.Position}/{position}.");
            FailIf(_player.Position != position || _player.IsAttacking ||
                _inventoryMenu.IsActive || _mapMenu.IsActive, $"{context}: playback input escaped its owner.");
            if (playing)
            {
                var frames = (IntroSpriteFrame[])typeof(Player).GetField("_harpFrames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
                IntroSpriteFrame frame = frames[_player.HarpPoseFrame];
                FailIf(frame.SourceOffset != 0 || !frame.Parts.SequenceEqual(rom.LinkParts),
                    $"{context}: Link retained graphic/OAM differs (runtime frame={_player.HarpPoseFrame}).");
            }
            var rng = _random.CaptureState();
            FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                $"{context}: ordered global RNG differs.");
            FailIf(!audit.Requests.SequenceEqual(rom.Sounds), $"{context}: sound requests differ: {string.Join(',', audit.Requests)} / {string.Join(',', rom.Sounds)}.");
            int[] slots = rom.Notes;
            var notes = _entities.Entities<NpcCharacter>().Where(n => n.Active && n.Name.ToString().StartsWith("PlayableHarpMusicNote", StringComparison.Ordinal)).ToArray();
            FailIf(notes.Length != slots.Length || _harp.NoteSpawnCount != rom.NoteCount,
                $"{context}: note count differs runtime={notes.Length}/{_harp.NoteSpawnCount}, ROM={slots.Length}/{rom.NoteCount}.");
            foreach (int address in slots)
            {
                var note = notes.Single(n => n.Name == $"PlayableHarpMusicNote{rom.NoteSerial(address)}");
                Vector2 expected = new(rom.Word(address + 0x0c) / 256.0f, rom.Word(address + 0x0a) / 256.0f);
                FailIf(note.Position != expected, $"{context}: INTERAC_FLOATING_IMAGE ${address:x4} position runtime={note.Position}, ROM={expected}.");
            }
            FailIf(_interactions.DialogueOpen != (rom[0xcba0] != 0) || _transitions.TimeWarpActive != (rom[0xcc04] == 0x1b),
                $"{context}: tune effect text/timewarp differs, ROM text=${rom[0xcba0]:x2}, trigger=${rom[0xcc04]:x2}.");
            if (DialogueOpen)
                FailIf(rom.Word(0xcba2) != 0x5510 ||
                    _dialogue.CurrentMessage != "Your tune echoes\nin vain." ||
                    _harp.Database.Record.NoEffectText != "Your \\col(1)tune\\col(0) echoes\nin vain.",
                    $"{context}: TX_5110 content or native text index differs.");
            FailIf(!inventory.SequenceEqual(Enumerable.Range(0xc688, 0x38).Select(_saveData.ReadWramByte)),
                $"{context}: playback changed inventory bytes.");
            int roomFlagAddress = (group & 1) == 0 ? 0xc700 + room : 0xc800 + room;
            if (group == 4) roomFlagAddress = 0xc900 + room;
            FailIf(_saveData.GetRoomFlags(group, room) != rom[roomFlagAddress], $"{context}: room flags differ.");
            if (placedPortal)
            {
                var portal = _entities.Entities<TimePortal>().Single();
                FailIf(portal.Awakening != (rom[0xd044] == 2) || portal.Active != (rom[0xd044] == 3),
                    $"{context}: Echoes portal signal/activation differs.");
            }
        }
        void Step(int count, int pressed = 0, int held = 0, Vector2? movement = null)
        {
            string[] Actions(int mask) => new[] { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
                .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
            int edge = pressed;
            StepGameplayUpdates(count, movement ?? Vector2.Zero, Actions(held), Actions(pressed), batched, () =>
            {
                rom.Update(edge, held, _entities.FrameCounter); edge = 0; update++;
                Compare();
            });
        }
        int button = primary ? 1 : 2;
        _player.DisableInstruments(2); rom[0xcc6b] = 2;
        Step(1, button, button);
        Step(2, held: button);
        FailIf(_player.IsUsingHarp, "Rejected instrument input retried without a new button edge.");
        Step(1);
        Step(1, button, button);
        Step(1, 0xfc | (primary ? 2 : 1), button | 0x40, Vector2.Up);
        Step(257, held: primary ? 1 : 2);
        FailIf(!_player.IsUsingHarp, "Harp finished before its native animation terminal parameter.");
        Step(1);
        FailIf(_player.IsUsingHarp || rom[0xd500] != 0, "Harp retained its parent beyond native completion.");
        if (!DialogueOpen && !IsTransitioning)
        {
            Step(1, held: button); // Completed signal is cleared by the next Link update.
            Step(1);
            Step(1, button, button);
            Step(259);
            FailIf(_player.IsUsingHarp, "A fresh performance did not finish on its native boundary.");
        }
    }
}


namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedCheckpoints()
    {
        // bank1.s native Seasons @respawnBuffers, independently of generated
        // records. Preset $01 is Subrosia; preset $03 is Horon's shop exterior.
        byte[][] presets = [
            [0xfe, 0, 0xb6, 3, 2, 0x48, 0x78, 0],
            [0xfe, 2, 0x5d, 0, 2, 0x68, 0x50, 0],
            [0xdc, 0, 0x6f, 0xff, 2, 0x58, 0x78, 0xff],
            [0xdc, 0, 0xc5, 0xff, 2, 0x28, 0x48, 0xff] ];
        byte[] Capture(OracleSaveData save) => Enumerable.Range(0xc5b0, 0x550).Select(save.ReadWramByte).ToArray();
        OracleSaveData Prepare(int group, int room, int x)
        {
            var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
            save.SetDeathRespawnPoint(group, room, 0x82, 1, 0x58, x,
                new RememberedCompanion(0x0b, 1, 0x97, 0x38, 0x68), 0xd1);
            save.WriteWramByte(0xc635, 0x5a);
            return save;
        }
        for (int preset = 0; preset < presets.Length; preset++)
        {
            var save = Prepare(3, 0xaf, 0x28);
            byte[] expected = Capture(save);
            for (int field = 0; field < 7; field++)
                if ((presets[preset][0] & (0x80 >> field)) != 0)
                    expected[0xc62b - 0xc5b0 + field] = presets[preset][field + 1];
            DeathRespawnPresets.Load(save, preset);
            FailIf(!Capture(save).SequenceEqual(expected),
                $"Seasons checkpoint preset ${preset:x2} must apply its native partial-write mask without changing other saved bytes.");
        }
        foreach (int hardware in new[] { 1, 0x81 })
        foreach (var test in new[] { (3, 0xaf, 0x00, true), (3, 0xaf, 0xff, true),
            (0, 0xc5, 0x3f, false), (0, 0xc5, 0x40, true), (0, 0xc5, 0xff, true),
            (3, 0xfe, 0x48, false), (1, 0x58, 0x48, false) })
        {
            var save = Prepare(test.Item1, test.Item2, test.Item3);
            byte[] expected = Capture(save);
            if (hardware == 1 && test.Item4)
            {
                // Mask $dc writes group/room/facing/Y/X, preserving season,
                // remembered companion ID and the remaining companion buffer.
                expected[0x7b] = 0; expected[0x7c] = 0xc5;
                expected[0x7e] = 2; expected[0x7f] = 0x28; expected[0x80] = 0x48;
            }
            DeathRespawnInitialization.Apply(save, (byte)hardware);
            FailIf(!Capture(save).SequenceEqual(expected),
                $"Seasons initializeGame profile=${hardware:x2}, checkpoint=${test.Item1:x1}:${test.Item2:x2}, X=${test.Item3:x2} lost native shop repair gates or preserved bytes.");
        }
        var rejectedSave = Prepare(0, 0x97, 0x58);
        byte[] original = Capture(rejectedSave);
        bool rejected = false;
        try { DeathRespawnPresets.Load(rejectedSave, 4); }
        catch (NotSupportedException) { rejected = true; }
        FailIf(!rejected || !Capture(rejectedSave).SequenceEqual(original),
            "An unsupported checkpoint preset must fail before changing the live save image.");
        ValidateSharedCheckpointRecording();
    }

    private void ValidateSharedCheckpointRecording()
    {
        LoadValidationRoom(0, 0x45);
        _entities.Clear();
        Check(_rooms, _player, _deathRespawnPoints);
        var root = new FrontendValidationRoot();
        AddChild(root);
        try
        {
            var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
            save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
            save.SetDeathRespawnPoint(0, 0xb6, 3, 2, 0x48, 0x78);
            save.SetRoomFlag(0, 0xb6, 0x40);
            save.SetLinkName("SEAS");
            var selector = root.InitializeGameSelect(); root.SeedFileSlot(0,save);
            selector.RequestPointerSelection(OracleGame.Seasons,true); root.Step(1,false); root.CompleteBootLoading();
            root.Step(1,false); root.Step(1,false,"inventory"); root.Step(32,false);
            root.Step(1,false,"attack"); root.Step(1,false); root.Step(1,false,"attack"); root.Step(32,false);
            var scene = root._seasonsOpening ?? throw new InvalidOperationException("Seasons checkpoint entry omitted its engine session.");
            Check(scene.Rooms, scene.Link, scene.Checkpoints);
        }
        finally { root.Free(); }

        void Check(RoomSession rooms, Player player, DeathRespawnPointController recorder)
        {
            OracleRuntimeState memory = rooms.RuntimeState;
            OracleSaveData save = rooms.SaveData;
            Vector2 position = player.PrecisePosition;
            Vector2I facing = player.FacingVector;
            byte modifier = memory.ReadField("wRoomStateModifier"), owner = memory.ReadField("wLinkObjectIndex");
            RememberedCompanion remembered = CompanionRuntimeState.ReadRemembered(memory);
            byte[] original = Capture(save);
            try
            {
                memory.SetField("wRoomStateModifier", 0x83);
                CompanionRuntimeState.Remember(memory, 0x0c, 1, 0x97, new(0x68, 0x38));
                // A carried/standing companion can occupy $d1 while Link still owns $d0.
                CompanionRuntimeState.Begin(memory, 0x0c, new(0x68, 0x38), 1);
                memory.SetField("wLinkObjectIndex", 0xd0);
                player.WarpTo(new(-1.25f, 260.75f)); player.Face(Vector2I.Left);
                save.WriteWramByte(0xc635, 0x5a);
                byte[] expected = Capture(save);
                byte[] fields = [(byte)rooms.ActiveGroup, (byte)rooms.CurrentRoom.Id, 0x83, 3, 4, 0xfe,
                    0x0c, 1, 0x97, 0xd0, 0x5a, 0x38, 0x68];
                fields.CopyTo(expected, 0xc62b - 0xc5b0);
                recorder.RecordCurrentPoint();
                FailIf(!Capture(save).SequenceEqual(expected),
                    $"{save.Game}: setDeathRespawnPoint must copy the live modifier, byte-wrapped world XY, five remembered fields and real Link page, retaining $c635.");
                memory.SetField("wLinkObjectIndex", 0xd1);
                MinecartRuntimeState.ClearActive(memory);
                expected[0xc634 - 0xc5b0] = 0xd1;
                recorder.RecordCurrentPoint();
                FailIf(!Capture(save).SequenceEqual(expected), $"{save.Game}: a mounted checkpoint must retain the actual $d1 Link page.");
            }
            finally
            {
                CompanionRuntimeState.Clear(memory, 0x0c);
                CompanionRuntimeState.Remember(memory, remembered.Id, remembered.Group, remembered.Room, new(remembered.X, remembered.Y));
                memory.SetField("wRoomStateModifier", modifier); memory.SetField("wLinkObjectIndex", owner);
                player.WarpTo(position); player.Face(facing);
                for (int address = 0xc62b; address <= 0xc637; address++) save.WriteWramByte(address, original[address - 0xc5b0]);
            }
        }

        static byte[] Capture(OracleSaveData save) => Enumerable.Range(0xc5b0, 0x550).Select(save.ReadWramByte).ToArray();
    }
}

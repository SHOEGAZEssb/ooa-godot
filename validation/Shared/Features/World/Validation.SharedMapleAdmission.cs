using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedMapleInitialization(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var memory = rooms.RuntimeState; var save = rooms.SaveData; var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id, kills = save.MapleKillCounter;
        var remembered = CompanionRuntimeState.ReadRemembered(memory);
        byte load = save.Game == OracleGame.Ages ? memory.ReadField("wcc05") : (byte)0;
        bool nut = save.Game == OracleGame.Ages && save.HasGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
        void ClearSlot()
        {
            entities.Clear();
            for (int address = 0xd100; address < 0xd140; address++) memory.SetWramByte(address, 0);
            memory.SetField("wcc85", 0); memory.SetField("wDisabledObjects", 0);
        }
        try
        {
            ClearSlot();
            if (save.Game == OracleGame.Ages)
            {
                save.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
                CompanionRuntimeState.Remember(memory, 0x0b, 1, 2, new(72, 56));
                save.SetMapleKillCounter(30); memory.SetField("wcc05", 0x0f);
                var destination = rooms.Load(1, 2);
                var native = new SomariaRom(save, entities.Random.CaptureState(), destination, 2, 80, 80);
                native[0xcc2e] = 0; native[0xcc05] = 0x0f;
                for (int address = 0xcc24; address <= 0xcc28; address++) native[address] = memory.ReadWramByte(address);
                native.InitializeRoomInteractions();
                root.LoadDebugRoom(1, 2);
                var maple = entities.Entities<MapleEncounter>().Single();
                FailIf(entities.EntityAdapters<ScriptedCompanionRoomEntity>().Any() || memory.ReadWramByte(0xd101) != 0x0e ||
                    maple.Stage != MapleEncounterStage.Initializing || maple.Visible || save.MapleKillCounter != 0,
                    "Ages past Maple must replace the freshly remembered animal's presentation without clearing the native slot or running state0.");
                for (int address = 0xd100; address < 0xd140; address++)
                    FailIf(memory.ReadWramByte(address) != native[address], $"Ages past Maple native slot ${address:x4} differs before the first gameplay update.");
                player.WarpTo(new(80, 80)); player.BeginCutsceneControl();
                memory.SetField("wDisabledObjects", 0x20);
                app.Step(3, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.Initializing || maple.Visible || memory.ReadWramByte(0xd104) != 0,
                    "The replaced Maple body must retain state0 under the companion mask.");
                memory.SetField("wDisabledObjects", 0);
                app.Step(1, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.EntryDelay || memory.ReadField("wcc85") != 0,
                    "The replaced Maple must initialize on its next eligible shared object visit.");
                app.Step(3, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.Flying, "Past Maple must finish the native three-update entry delay after replacement.");
                // Declare a retained-slot re-admission independently of the ordinary
                // player exit lock; exercise the real owner and gameplay visits.
                save.SetMapleKillCounter(30);
                destination = rooms.Load(1, 0x12);
                var retained = new BraceletRom(destination, player.Position, 2, 0xff);
                retained.LoadSave(save);
                for (int address = 0xd100; address < 0xd140; address++) retained[address] = memory.ReadWramByte(address);
                retained[0xd100] = 2; retained[0xcc2d] = 1; retained[0xcc30] = 0x12;
                retained.CheckAndSpawnMaple();
                memory.SetField("wScrollMode", 8);
                entities.BeginScreenTransition(1, destination, new(0, 128), EnemyPlacementContext.Scrolling(Vector2I.Down, 0x55), player);
                FailIf(!ReferenceEquals(entities.Entities<MapleEncounter>().Single(), maple) ||
                    entities.OutgoingEntities<MapleEncounter>().Count != 0 || maple.Stage != MapleEncounterStage.Flying,
                    "Native enabled=$01 re-admission must transfer the retained Maple presentation to the incoming room without reconstructing state0.");
                for (int address = 0xd100; address < 0xd140; address++)
                    FailIf(memory.ReadWramByte(address) != retained[address], $"Retained past Maple byte ${address:x4} differs from native re-admission.");
                Vector2 birth = maple.Position;
                app.Step(3, Vector2.Zero, batched: batched);
                FailIf(maple.Stage != MapleEncounterStage.Flying || maple.Position != birth,
                    "A retained Maple must freeze under the source scroll-mode gate after re-admission.");
                entities.FinishScreenTransition(); memory.SetField("wScrollMode", 1);
                app.Step(3, Vector2.Zero, batched: batched);
                FailIf(!ReferenceEquals(entities.Entities<MapleEncounter>().Single(), maple) || maple.Finished,
                    "clearObjectsWithEnabled2 must retain the re-admitted enabled=$01 Maple body.");
                byte state = memory.ReadWramByte(0xd104);
                memory.SetWramByte(0xd104, 5);
                byte[] opaque = memory.CaptureState().Wram;
                bool diagnosed = false;
                try { SomariaPrivate<IRoomEntityFactory>(entities, "_factory").CreateMaple(rooms.CurrentRoom); }
                catch (NotSupportedException error) when (error.Message.Contains("checkAndSpawnMaple") && error.Message.Contains("state $05")) { diagnosed = true; }
                FailIf(!diagnosed || !memory.CaptureState().Wram.SequenceEqual(opaque),
                    "Unbound retained Maple state must diagnose its source before constructing a state0 body or rewriting bytes.");
                memory.SetWramByte(0xd104, state);
            }
            else
            {
                CompanionRuntimeState.ForgetRemembered(memory);
                save.SetMapleKillCounter(30);
                bool unsupported = false;
                try { root._seasonsOpening!.DebugTravel(0, 0x12); }
                catch (NotSupportedException error) when (error.Message.Contains("checkAndSpawnMaple") && error.Message.Contains("SPECIALOBJECT $0e")) { unsupported = true; }
                FailIf(!unsupported || save.MapleKillCounter != 0 || memory.ReadField("wIsMaplePresent") != 1 ||
                    memory.ReadField("wcc85") != 1 || memory.ReadWramByte(0xd100) != 1 || memory.ReadWramByte(0xd101) != 0x0e ||
                    memory.ReadWramByte(0xd10b) != 0x18 || memory.ReadWramByte(0xd10d) != 0xb8,
                    "Seasons must execute shared native Maple admission and diagnose the unbound encounter content, rather than silently skipping the mechanism.");
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); player.EndCutsceneControl(); ClearSlot();
            CompanionRuntimeState.Remember(memory, remembered.Id, remembered.Group, remembered.Room, new(remembered.X, remembered.Y));
            save.SetMapleKillCounter(0);
            if (save.Game == OracleGame.Ages)
            {
                memory.SetField("wcc05", load); save.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced, nut);
                root.LoadDebugRoom(originalGroup, originalRoom);
            }
            else root._seasonsOpening!.DebugTravel(originalGroup, originalRoom);
            save.SetMapleKillCounter(kills);
            entities.EnableLinkCollisionsAndMenu(); player.EndCutsceneControl();
        }
    }

    private void ValidateSharedMapleAdmissionRom()
    {
        int cases = 0;
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            string pack = OracleAssetPack.ForGame(game);
            var data = MapleAdmissionData.For(pack);
            var save = OracleSaveData.CreateStandardGame(game);
            var memory = OracleRuntimeState.CreateInitialized(game);
            var room = game == OracleGame.Ages ? _world.LoadRoom(0, 1) : new SeasonsWorldData().LoadRoom(0x97, 0, 0);
            var native = new BraceletRom(room, new(80, 80), 2, 0xff, game: game);
            bool Check(int group, int roomId, byte companion, byte enabled, byte gate, byte kills, byte ring, byte state)
            {
                save.WriteField("wAnimalCompanion", companion); save.WriteField("wActiveRing", ring); save.SetMapleKillCounter(kills);
                native.LoadSave(save);
                for (int offset = 0; offset < 64; offset++)
                {
                    byte value = (byte)(offset * 7 + 0x21);
                    native[0xd100 + offset] = value; memory.SetWramByte(0xd100 + offset, value);
                }
                native[0xd100] = enabled; memory.SetWramByte(0xd100, enabled);
                native[0xd101] = 0x0b; memory.SetWramByte(0xd101, 0x0b);
                native[0xd104] = state; memory.SetWramByte(0xd104, state);
                native[0xcc2d] = (byte)group; native[0xcc30] = (byte)roomId;
                native[0xcc85] = gate; memory.SetField("wcc85", gate);
                native[0xcdda] = 0x5a; memory.SetField("wIsMaplePresent", 0x5a);
                native.CheckAndSpawnMaple();
                bool admitted = data.TryAdmit(group, roomId, save, memory);
                string context = $"{game} Maple group${group:x1}/room${roomId:x2}/companion${companion:x2}/enabled${enabled:x2}/gate${gate:x2}/kills${kills:x2}/ring${ring:x2}/state${state:x2}";
                FailIf(admitted != (native[0xc641] == 0) || save.MapleKillCounter != native[0xc641] ||
                    memory.ReadField("wcc85") != native[0xcc85] || memory.ReadField("wIsMaplePresent") != native[0xcdda] ||
                    native.RandomCalls != 0,
                    context + ": admission side effects differ from original checkAndSpawnMaple.");
                for (int offset = 0; offset < 64; offset++)
                    FailIf(memory.ReadWramByte(0xd100 + offset) != native[0xd100 + offset],
                        context + $": SPECIALOBJECT byte ${0xd100 + offset:x4} differs; birth must retain state/fractions/other slot bytes.");
                cases++;
                return admitted;
            }
            foreach (byte companion in game == OracleGame.Ages ? new byte[] { 0, 0x0b, 0x0c, 0x0d } : new byte[] { 0x0b, 0x0c, 0x0d })
            for (int roomId = 0; roomId < 256; roomId++)
                Check(0, roomId, companion, 0, 0, 30, 0xff, 0);
            if (game == OracleGame.Ages)
                for (int roomId = 0; roomId < 256; roomId++) Check(1, roomId, 0xff, 3, 0, 30, 0xff, 5);
            foreach (int group in new[] { 0, 1, 2 })
            foreach (byte enabled in new byte[] { 0, 1, 3 })
            foreach (byte gate in new byte[] { 0, 1, 0x80 })
            foreach (byte kills in new byte[] { 14, 15, 29, 30 })
            foreach (byte ring in new byte[] { 0x0f, 0xff })
            foreach (byte state in new byte[] { 0, 5 })
                Check(group, group == 1 ? 2 : game == OracleGame.Ages ? 1 : 0x12, 0x0b, enabled, gate, kills, ring, state);
            FailIf(!Check(0, game == OracleGame.Ages ? 1 : 0x12, 0x0b, 0, 0, 30, 0xff, 0) ||
                !Check(0, game == OracleGame.Ages ? 1 : 0x12, 0x0b, 0, 0, 15, 0x0f, 0),
                $"{game}: literal source-eligible rooms must admit at the native ordinary/ring thresholds.");
            foreach (byte raw in new byte[] { 1, 2 })
            {
                bool rejected = false;
                try { data.IsEligibleLocation(0, 1, raw); }
                catch (NotSupportedException error) when (error.Message.Contains($"raw companion ${raw:x2}") && error.Message.Contains("native bitmap $")) { rejected = true; }
                FailIf(!rejected, $"{game}: legacy companion ${raw:x2} aliases must not silently select a named native bitmap.");
            }
            byte[] before = memory.CaptureState().Wram;
            bool mismatch = false;
            try { data.TryAdmit(0, 1, OracleSaveData.CreateStandardGame(game == OracleGame.Ages ? OracleGame.Seasons : OracleGame.Ages), memory); }
            catch (ArgumentException) { mismatch = true; }
            FailIf(!mismatch || !memory.CaptureState().Wram.SequenceEqual(before), "Cross-pack Maple admission must fail before writes.");
        }
        GD.Print($"Validated shared Maple admission against both original native routines: {cases} cases, all source map bitmaps, raw selectors, slot/group/gate/ring/threshold boundaries, preserved slot bytes and RNG.");
    }
}

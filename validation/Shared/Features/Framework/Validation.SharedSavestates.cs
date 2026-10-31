using Godot;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static DebugSavestateData CheckSharedSavestates(FrontendValidationRoot root, bool batched, DebugSavestateData? previousState)
    {
        OracleGame game = root._selectedGame;
        OracleGame other = game == OracleGame.Ages ? OracleGame.Seasons : OracleGame.Ages;
        var engine = root._gameplaySession!;
        var native = root._seasonsOpening;
        var rooms = engine.Rooms;
        var entities = native?.Entities ?? root._entities;
        var player = root._scene.Player;
        if (native is not null)
        {
            native.Memory.SetField("wRoomStateModifier",2);
            native.DebugTravel(0,0xb6);
            entities.Clear();
        }
        var save = rooms.SaveData;
        var memory = rooms.RuntimeState;
        memory.SetWramByte(0xc123,0xab);
        memory.SetField("wDisplayedRupees",0x57);
        memory.SetField("wDisplayedRupees",0x02,1);
        memory.SetField("wDisplayedHearts",0x07);
        memory.SetField("wDontUpdateStatusBar",0x77);
        player.WarpTo(new(0x58,0x48)); player.Face(Vector2I.Left);
        root._random.Next(); root._random.Next();
        var captured = root.CaptureDebugSavestate();
        byte[] expectedSave = captured.CreateSaveData().Serialize();
        var expectedRandom = root._random.CaptureState();
        var expectedEntities = entities.CaptureDebugState();
        byte[] expectedLayout = rooms.CurrentRoom.Layout.ToArray();
        byte[] encoded = captured.Serialize();
        if (previousState is not null && previousState.Game != game)
        {
            bool rejected = false;
            try { root.RestoreDebugSavestate(previousState); }
            catch (ArgumentException) { rejected = true; }
            FailIf(!rejected || !engine.Active || !ReferenceEquals(root._gameplaySession,engine) ||
                !save.Serialize().AsSpan().SequenceEqual(expectedSave) || root._random.Calls != expectedRandom.Calls,
                "A foreign game's valid debug state must fail before releasing or modifying the current session.");
        }
        bool valid = DebugSavestateData.TryDeserialize(encoded,game,out var decoded);
        FailIf(captured.Game != game || !valid ||
            DebugSavestateData.TryDeserialize(encoded,other,out _),
            "Shared debug states must round-trip only for their explicit native game.");
        static byte[] RetagVersion(byte[] source, int version)
        {
            byte[] result = (byte[])source.Clone();
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8, 4), version);
            SHA256.HashData(result.AsSpan(0, result.Length - 32)).CopyTo(result, result.Length - 32);
            return result;
        }
        FailIf(BinaryPrimitives.ReadInt32LittleEndian(encoded.AsSpan(8, 4)) != 5 ||
            DebugSavestateData.TryDeserialize(RetagVersion(encoded, 2), game, out _) ||
            DebugSavestateData.TryDeserialize(RetagVersion(encoded, 3), game, out _) ||
            DebugSavestateData.TryDeserialize(RetagVersion(encoded, 4), game, out _),
            "Native cart interaction/item snapshots require version5; prior formats lack reconstructible physical lifetimes.");
        var stableMemory = memory.CaptureState();
        try
        {
            foreach (int id in Enumerable.Range(0x0a, 10))
            foreach ((int State, int Substate, int Z) phase in id is >= 0x0b and <= 0x0d
                ? new[] { (0, 0, 0), (3, 0, 0), (0x0a, 0, 0), (0x0c, 0, 0), (5, 1, 0), (1, 0, 1) }
                : new[] { (0, 0, 0) })
            {
                CompanionRuntimeState.Begin(memory, id, new(104.75f, 56.25f), 1);
                memory.SetWramByte(0xd104, (byte)phase.State);
                memory.SetWramByte(0xd105, (byte)phase.Substate);
                memory.SetWramByte(0xd10e, (byte)phase.Z);
                memory.SetWramByte(0xd10f, 0);
                memory.SetField("wLinkObjectIndex", 0xd0);
                bool unboundRejected = false;
                try { root.CaptureDebugSavestate(); }
                catch (NotSupportedException) { unboundRejected = true; }
                FailIf(!unboundRejected, $"{game}: an unbound native slot${id:x2} must reject live capture.");
                // Exercise the wire format independently of live actor reconstruction.
                var wireState = (DebugSavestateData)typeof(DebugSavestateData)
                    .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                    .Invoke([captured.Group, captured.Room, captured.PlayerPosition, captured.PlayerFacing,
                        captured.AnimationTicks, expectedSave, memory.CaptureState(), expectedRandom, expectedEntities]);
                byte[] occupied = wireState.Serialize();
                FailIf(!DebugSavestateData.TryDeserialize(occupied, game, out _) ||
                    DebugSavestateData.TryDeserialize(RetagVersion(occupied, 2), game, out _) ||
                    DebugSavestateData.TryDeserialize(RetagVersion(occupied, 3), game, out _) ||
                    DebugSavestateData.TryDeserialize(RetagVersion(occupied, 4), game, out _),
                    $"{game}: occupied native slot${id:x2} requires the current native object format.");
                memory.SetWramByte(0xc123, 0x55);
                var beforeRejectedRestore = memory.CaptureState();
                foreach (Action restore in new Action[]
                {
                    () => root.RestoreDebugSavestate(wireState),
                    () => wireState.RestoreRoomParseState(entities, memory),
                    () => wireState.RestoreLiveState(save, memory, root._random, entities)
                })
                {
                    bool unsupportedRejected = false;
                    try { restore(); }
                    catch (ArgumentException error) when (error.Message.Contains($"${id:x2}", StringComparison.Ordinal))
                    { unsupportedRejected = true; }
                    FailIf(!unsupportedRejected || !engine.Active || !ReferenceEquals(root._gameplaySession, engine) ||
                        !memory.CaptureState().Wram.AsSpan().SequenceEqual(beforeRejectedRestore.Wram) ||
                        !save.Serialize().AsSpan().SequenceEqual(expectedSave) || root._random.Calls != expectedRandom.Calls,
                        $"{game}: unsupported native slot${id:x2} must fail before session disposal or live-state mutation.");
                }
            }
        }
        finally { memory.RestoreState(stableMemory); }
        var wrongSave = OracleSaveData.CreateStandardGame(other);
        byte[] wrongImage = wrongSave.Serialize();
        bool wrongOwnerRejected = false;
        try { decoded!.RestoreLiveState(wrongSave,memory,root._random,entities); }
        catch (ArgumentException) { wrongOwnerRejected = true; }
        FailIf(!wrongOwnerRejected || !wrongSave.Serialize().AsSpan().SequenceEqual(wrongImage) ||
            memory.ReadWramByte(0xc123) != 0xab || root._random.Calls != expectedRandom.Calls,
            "A mismatched live save owner must be rejected before any save, runtime or RNG mutation.");
        wrongOwnerRejected = false;
        try { decoded!.RestoreLiveState(captured.CreateSaveData(),memory,root._random,entities); }
        catch (ArgumentException) { wrongOwnerRejected = true; }
        FailIf(!wrongOwnerRejected,"A same-game save clone must not replace the session's authoritative save owner.");
        wrongOwnerRejected = false;
        try { decoded!.RestoreLiveState(save,memory,new OracleRandom(),entities); }
        catch (ArgumentException) { wrongOwnerRejected = true; }
        FailIf(!wrongOwnerRejected,"Savestate restore must reject a foreign RNG owner before restoring any live bytes.");
        if (game == OracleGame.Ages)
        {
            // Independent old wire layout: magic, version 1, then group; no game tag.
            byte[] legacyPayload = new byte[encoded.Length - 32 - sizeof(int)];
            encoded.AsSpan(0,12).CopyTo(legacyPayload);
            BinaryPrimitives.WriteInt32LittleEndian(legacyPayload.AsSpan(8,4),1);
            encoded.AsSpan(16,encoded.Length - 32 - 16).CopyTo(legacyPayload.AsSpan(12));
            byte[] legacy = new byte[legacyPayload.Length + 32];
            legacyPayload.CopyTo(legacy,0); SHA256.HashData(legacyPayload).CopyTo(legacy,legacyPayload.Length);
            FailIf(DebugSavestateData.TryDeserialize(legacy,game,out _) ||
                DebugSavestateData.TryDeserialize(legacy,other,out _),
                "Untagged version-1 snapshots cannot reconstruct current native object lifetimes.");
        }
        encoded[^1] ^= 0xff;
        FailIf(DebugSavestateData.TryDeserialize(encoded,game,out _),"Shared debug states must reject corrupt payload hashes.");
        string path = Path.Combine(Path.GetTempPath(),$"oracles-debug-{Guid.NewGuid():N}.state");
        try
        {
            FailIf(!DebugSavestateStore.Save(decoded!,path).Success || !DebugSavestateStore.Load(path,game).Success ||
                DebugSavestateStore.Load(path,other).Success || File.Exists(path + ".tmp"),
                "Atomic shared debug storage must validate its selected-game readback and remove the staged file.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        FailIf(DebugSavestateStore.PathForSlot(OracleGame.Ages,7) != "user://oracle_of_ages_debug_state_7.state" ||
            DebugSavestateStore.PathForSlot(OracleGame.Seasons,7) != "user://oracle_of_seasons_debug_state_7.state",
            "Native debug slot paths must preserve existing Ages files and isolate Seasons.");

        var app = new ApplicationValidationFixture(root);
        int retailWrites = root._saveWriteRequests, debugWrites = root.DebugStateWrites;
        root._Input(new InputEventKey { Pressed = true,PhysicalKeycode = Key.Key7,ShiftPressed = true });
        FailIf(root.DebugStateWrites != debugWrites + 1 || !root._scene.RoomDebug.Text.Contains("S7 SAVED",StringComparison.Ordinal),
            "Shift+number must capture through the same actual application input path in both games.");
        app.Step(1,Vector2.Zero,held:["debug_object_spawner"],pressed:["debug_object_spawner"],batched:batched);
        root._Input(new InputEventKey { Pressed = true,PhysicalKeycode = Key.Key7,ShiftPressed = true });
        FailIf(root.DebugStateWrites != debugWrites + 1 || !root._scene.RoomDebug.Text.Contains("S7 BUSY",StringComparison.Ordinal),
            "Savestate capture must reject a modal pause without replacing the saved generation.");
        save.SetGlobalFlag(GlobalFlag.IntroDone,false);
        memory.SetWramByte(0xc123,0x11);
        root._random.Next(); player.WarpTo(new(24,24)); player.Face(Vector2I.Up);
        // Loading may cancel a modal; it must retire the old lease without a callback.
        Action retiredGameOver=engine.Menus.BeginGameOver;
        root._Input(new InputEventKey { Pressed = true,PhysicalKeycode = Key.Key7 });
        var restoredEngine = root._gameplaySession!;
        FailIf(!ReferenceEquals(root._gameplayMenus,restoredEngine.Menus) ||
            !ReferenceEquals(root._gameplayPause,restoredEngine.Menus.Pause) ||
            !ReferenceEquals(root._menuLifecycle,restoredEngine.Menus.Lifecycle) ||
            !ReferenceEquals(root._mapMenu,restoredEngine.Menus.Map) ||
            !ReferenceEquals(root._inventoryMenu,restoredEngine.Menus.Inventory) ||
            !ReferenceEquals(root._debugFlagMenu,restoredEngine.Menus.Flags) ||
            !ReferenceEquals(root._debugCollision,restoredEngine.Menus.Collision),
            $"{game}: every application menu/development accessor must resolve the replacement session's owner.");
        native = root._seasonsOpening;
        rooms = restoredEngine.Rooms; entities = native?.Entities ?? root._entities; player = root._scene.Player;
        FailIf(restoredEngine.StatusBar.DisplayedRupees != 257 || root._scene.Hud.Rupees != 257,
            $"{game}: debug reconstruction must publish captured displayed-money BCD without advancing or snapping it to live inventory.");
        FailIf(restoredEngine.StatusBar.DisplayedHealth != 7 || root._scene.Hud.HealthQuarters != 7,
            $"{game}: debug reconstruction must publish native wDisplayedHearts without advancing recovery or snapping to live health.");
        FailIf(rooms.RuntimeState.ReadField("wDontUpdateStatusBar")!=0x77,
            $"{game}: debug reconstruction must preserve wDontUpdateStatusBar instead of running showStatusBar_body.");
        var actualRandom = root._random.CaptureState();
        var actualEntities = entities.CaptureDebugState();
        FailIf(engine.Active || engine.Menus.Pause.IsLeased || ReferenceEquals(engine,restoredEngine) || !restoredEngine.Active ||
            !root._scene.RoomDebug.Text.Contains("S7 LOADED",StringComparison.Ordinal) || root._saveWriteRequests != retailWrites ||
            rooms.ActiveGroup != captured.Group || rooms.CurrentRoom.Id != captured.Room ||
            player.PrecisePosition != captured.PlayerPosition || player.FacingVector != captured.PlayerFacing ||
            rooms.RuntimeState.ReadWramByte(0xc123) != 0xab || !rooms.SaveData.Serialize().AsSpan().SequenceEqual(expectedSave) ||
            !rooms.CurrentRoom.Layout.SequenceEqual(expectedLayout) ||
            actualRandom.Rng1 != expectedRandom.Rng1 || actualRandom.Rng2 != expectedRandom.Rng2 ||
            actualRandom.Calls != expectedRandom.Calls || actualRandom.PlacementIndex != expectedRandom.PlacementIndex ||
            actualRandom.PlacementBufferReady != expectedRandom.PlacementBufferReady || actualRandom.LastResult != expectedRandom.LastResult ||
            !actualRandom.PlacementBuffer.SequenceEqual(expectedRandom.PlacementBuffer) ||
            actualEntities.FrameCounter != expectedEntities.FrameCounter || actualEntities.FrameAccumulator != expectedEntities.FrameAccumulator ||
            actualEntities.ActiveTriggers != expectedEntities.ActiveTriggers ||
            actualEntities.RecentEnemyDefeats.Tail != expectedEntities.RecentEnemyDefeats.Tail ||
            actualEntities.RecentEnemyDefeats.ActiveRoom != expectedEntities.RecentEnemyDefeats.ActiveRoom ||
            !actualEntities.RecentEnemyDefeats.Rooms.SequenceEqual(expectedEntities.RecentEnemyDefeats.Rooms) ||
            !actualEntities.RecentEnemyDefeats.KilledEnemies.SequenceEqual(expectedEntities.RecentEnemyDefeats.KilledEnemies),
            "Shared savestate load must cancel modal ownership and reconstruct the selected native save, room, player, WRAM, RNG and slot-walk state without a retail commit.");
        if (native is not null)
            FailIf(native.Memory.ReadField("wRoomStateModifier") != 2 || root.CaptureDebugSavestate().AnimationTicks != captured.AnimationTicks,
                "Seasons restore must publish the captured season before loading room geometry and retain its animation counter.");
        byte[] replacementSave=rooms.SaveData.Serialize();
        CheckRetiredMenuCallbacks(engine,root._sound,root._sound.AttachPlayRequestAudit(),retiredGameOver);
        FailIf(!restoredEngine.Active || !replacementSave.SequenceEqual(rooms.SaveData.Serialize()),
            $"{game}: retired callbacks must leave the replacement session and save intact.");
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(restoredEngine.StatusBar.DisplayedHealth!=7 || restoredEngine.StatusBar.DisplayedRupees!=257,
            $"{game}: the restored native status gate must freeze health and money through actual updates.");
        rooms.RuntimeState.SetField("wDontUpdateStatusBar",0);
        app.Step(4,Vector2.Zero,batched:batched);
        FailIf(restoredEngine.StatusBar.DisplayedHealth==7,
            $"{game}: releasing the restored native status gate must resume health updates.");
        FailIf(!restoredEngine.Active || restoredEngine.Menus.Pause.IsLeased || root._saveWriteRequests != retailWrites,
            "The reconstructed native session must resume ordinary updates without a stale modal or disk write.");
        app.Step(1,Vector2.Zero,held:["debug_flags"],pressed:["debug_flags"],batched:batched);
        FailIf(!restoredEngine.Menus.Flags.IsActive || !root.DebugFlagMenuOpen || !restoredEngine.Menus.Pause.IsLeased,
            $"{game}: replacement menus must acquire their own live pause owner.");
        app.Step(1,Vector2.Zero,held:["debug_flags"],pressed:["debug_flags"],batched:batched);
        FailIf(restoredEngine.Menus.Flags.IsActive || restoredEngine.Menus.Pause.IsLeased,
            $"{game}: replacement menus must release their own live pause owner.");
        // Saving again must address the replacement session, not captured native closures.
        root._Input(new InputEventKey { Pressed = true,PhysicalKeycode = Key.Key7,ShiftPressed = true });
        FailIf(root.DebugStateWrites != debugWrites + 2,"Shared debug input must capture the replacement session again.");
        root.ClearDebugSavestateStatus();
        restoredEngine.StatusBar.SynchronizeHealth();
        return captured;
    }
}

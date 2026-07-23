using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOverworldKeyholeAndGraveyardGate()
    {
        const int group = 0;
        const int roomId = 0x5c;
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        OverworldKeyholeDatabase database = _keyholes.Database;
        GraveyardGateEvent gate = _roomEvents.GraveyardGate;
        if (database.Count != 6 || database.TileCount != 3 ||
            !database.TryGet(group, roomId, out OverworldKeyholeDatabaseRecord keyhole) ||
            keyhole is not
            {
                Treasure: TreasureDatabase.TreasureGraveyardKey,
                SubId: 0,
                TileBase: 0x0e,
                Palette: 5
            } || !database.IsKeyholeTile(0, 0xec) ||
            !database.IsKeyholeTile(1, 0xae) ||
            !database.IsKeyholeTile(4, 0xec))
        {
            throw new InvalidOperationException(
                "The imported six-room Ages keyhole table or collision-set tiles are incomplete.");
        }

        bool originalGateFlag = _saveData.HasRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(group, roomId);
        OracleRoomData room = _currentRoom;
        Vector2 keyholeCenter = new(0x48, 0x48);
        Vector2 linkBelow = new(0x48, 0x52);
        byte At(int packed) => room.GetMetatile(new Vector2(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8));
        void Push() => _keyholes.UpdatePushAttempt(
            linkBelow, Vector2I.Up, Vector2.Up);
        void StepGate() => _roomEvents.Update(update);

        if (_inventory.HasTreasure(TreasureDatabase.TreasureGraveyardKey) ||
            room.ActiveCollisions != 0 || At(0x44) != 0xec ||
            gate.Stage != GraveyardGateEventEventStage.WaitingForKeyhole ||
            gate.BlocksGameplay || _roomEvents.Active)
        {
            throw new InvalidOperationException(
                "Room 0:5c did not arm its nonblocking $dc:$01 controller around keyhole tile $ec.");
        }

        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 9; frame++)
            Push();
        if (_dialogue.IsOpen || _keyholes.RemainingPushFrames != 2 ||
            _saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80))
        {
            throw new InvalidOperationException(
                "nextToOverworldKeyhole activated before its doubled 20-to-zero push counter elapsed.");
        }
        Push();
        if (!_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "Huh? This has a\nkeyhole." ||
            !_keyholes.InformativeTextShown ||
            _saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 0)
        {
            throw new InvalidOperationException(
                "Room 0:5c did not show one-time TX_5109 when the Graveyard Key was absent.");
        }
        _dialogue.Close();
        for (int frame = 0; frame < 10; frame++)
            Push();
        if (_dialogue.IsOpen || !_keyholes.InformativeTextShown)
        {
            throw new InvalidOperationException(
                "wInformativeTextsShown did not suppress repeated TX_5109 in the same room.");
        }

        _inventory.GiveTreasure(
            _treasures.GetObject("TREASURE_OBJECT_GRAVEYARD_KEY_00"));
        for (int frame = 0; frame < 9; frame++)
            Push();
        if (gate.Stage != GraveyardGateEventEventStage.WaitingForKeyhole ||
            _keyholes.RemainingPushFrames != 2 ||
            _entities.Entities<OverworldKeyUseEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "The owned Graveyard Key triggered before the tenth continuous push update.");
        }

        int roomMusic = _sound.ActiveMusic;
        int randomCalls = _entities.RandomCalls;
        _sound.ClearPlayRequestAudit();
        Push();
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureGraveyardKey) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlag80) ||
            gate.Stage != GraveyardGateEventEventStage.Running ||
            !_roomEvents.Active || !_player.CutsceneControlled ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 0 ||
            _entities.Entities<OverworldKeyUseEffect>() is not
                [{ State: 0, Counter: 0, ZFixed: 0, SpeedZ: 0 }])
        {
            throw new InvalidOperationException(
                "The 0:5c keyhole did not retain key $42, set room flag $80, " +
                "create INTERAC_OVERWORLD_KEY_SPRITE, and disable control.");
        }

        OverworldKeyUseEffect keyEffect =
            _entities.Entities<OverworldKeyUseEffect>().Single();
        keyEffect.UpdateFrame();
        if (keyEffect.State != 1 || keyEffect.SpeedZ != -0x200 ||
            keyEffect.ZFixed != 0)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE state 0 did not install speedZ -$200 without moving.");
        }
        for (int frame = 0; frame < 12; frame++)
            keyEffect.UpdateFrame();
        if (keyEffect.State != 1 || keyEffect.SpeedZ != -0x20 ||
            keyEffect.ZFixed != -0xdb0)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE diverged before its speed apex.");
        }
        keyEffect.UpdateFrame();
        if (keyEffect.State != 2 || keyEffect.SpeedZ != 0x08 ||
            keyEffect.ZFixed != -0xdd0 || keyEffect.Counter != 60)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE did not enter its 60-update apex hold.");
        }
        for (int frame = 0; frame < 59; frame++)
            keyEffect.UpdateFrame();
        if (keyEffect.Finished || keyEffect.Counter != 1)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE ended before its full apex hold.");
        }
        keyEffect.UpdateFrame();
        if (!keyEffect.Finished)
        {
            throw new InvalidOperationException(
                "INTERAC_OVERWORLD_KEY_SPRITE did not delete after its 60-update hold.");
        }

        StepGate();
        if (_sound.ActiveMusic != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndCtrlStopMusic) != 1 ||
            gate.CurrentCommandIndex != 1 || gate.Counter != 0)
        {
            throw new InvalidOperationException(
                "interactiondcSubid01Script did not stop room music before wait 60.");
        }
        StepGate();
        for (int frame = 0; frame < 59; frame++)
            StepGate();
        if (gate.Counter != 1 || At(0x34) != 0x89 || At(0x44) != 0xec ||
            _entities.Entities<PuzzlePuffEffect>().Count != 0)
        {
            throw new InvalidOperationException(
                "The first graveyard-gate collapse phase ran before wait 60 reached zero.");
        }
        StepGate();
        if (At(0x34) != 0x3a || At(0x44) != 0x3a ||
            At(0x33) != 0x3a || At(0x35) != 0x3a ||
            At(0x43) != 0x98 || At(0x45) != 0x9a ||
            gate.Counter != 45 || gate.ShakeCounter != 9 ||
            _entities.RandomCalls != randomCalls + 2 ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 1 }, { ElapsedUpdates: 1 }] ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 2)
        {
            throw new InvalidOperationException(
                "Phase 1 did not apply the two ordinary/four interleaved tiles, " +
                "two puffs, and ten-update shake in source order.");
        }

        for (int frame = 0; frame < 44; frame++)
            StepGate();
        if (gate.Counter != 1 || At(0x43) != 0x98 || At(0x45) != 0x9a ||
            _entities.RandomCalls != randomCalls + 20)
        {
            throw new InvalidOperationException(
                "The second collapse phase ran before wait 45 or the first shake consumed the wrong RNG count.");
        }
        StepGate();
        if (At(0x33) != 0x3a || At(0x35) != 0x3a ||
            At(0x43) != 0x3a || At(0x45) != 0x3a ||
            gate.Counter != 60 || gate.ShakeCounter != 9 ||
            _entities.RandomCalls != randomCalls + 22 ||
            _entities.Entities<PuzzlePuffEffect>().Count != 4 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 4)
        {
            throw new InvalidOperationException(
                "Phase 2 did not finish the gate with four ordinary tiles, two puffs, and a fresh shake.");
        }

        for (int frame = 0; frame < 59; frame++)
            StepGate();
        if (gate.Counter != 1 || _entities.RandomCalls != randomCalls + 40 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "The final wait/shake boundary diverged before resetmusic.");
        }
        StepGate();
        if (_sound.ActiveMusic != roomMusic || gate.CurrentCommandIndex != 7)
        {
            throw new InvalidOperationException(
                "resetmusic did not restore room 0:5c's normal track after wait 60.");
        }
        StepGate();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            !_player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "The solve cue did not yield once before enabling Link input.");
        }
        StepGate();
        if (gate.Stage != GraveyardGateEventEventStage.Completed ||
            gate.HasState || _roomEvents.Active || _player.CutsceneControlled)
        {
            throw new InvalidOperationException(
                "interactiondcSubid01Script did not enable input and end after the solve cue.");
        }

        LoadValidationRoom(group, roomId);
        room = _currentRoom;
        if (room.GetMetatile(new Vector2(0x48, 0x38)) != 0x3a ||
            room.GetMetatile(new Vector2(0x38, 0x48)) != 0x3a ||
            room.GetMetatile(new Vector2(0x48, 0x48)) != 0x3a ||
            room.GetMetatile(new Vector2(0x58, 0x48)) != 0x3a ||
            gate.Stage != GraveyardGateEventEventStage.Inactive)
        {
            throw new InvalidOperationException(
                "Room 0:5c did not apply its persistent flag-$80 gate substitutions " +
                $"on re-entry: $34=${room.GetMetatile(new Vector2(0x48, 0x38)):x2}, " +
                $"$43=${room.GetMetatile(new Vector2(0x38, 0x48)):x2}, " +
                $"$44=${room.GetMetatile(new Vector2(0x48, 0x48)):x2}, " +
                $"$45=${room.GetMetatile(new Vector2(0x58, 0x48)):x2}, " +
                $"flag=${_saveData.GetRoomFlags(group, roomId):x2}, stage={gate.Stage}.");
        }

        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlag80, originalGateFlag);
        LoadValidationRoom(0, 0x11);
        GD.Print("Validated room 0:5c's six-room imported keyhole table, retained " +
            "Graveyard Key, one-time TX_5109, doubled push counter, $18 key apex, " +
            "typed 60/45/60 gate script, interleaved collapse tiles, puffs, RNG shake, " +
            "music/solve/input order, and persistent re-entry state.");
    }
}

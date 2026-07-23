using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDarkRoomInteractions()
    {
        const int group = 5;
        const int roomId = 0xed;
        var database = new DarkRoomDatabase();
        IReadOnlyList<DarkRoomDatabaseRecord> records =
            database.GetRoomRecords(group, roomId);
        if (database.RecordCount != 3 || records.Count != 2 ||
            records[0] is not
            {
                Order: 0, Kind: DarkRoomDatabaseObjectKind.Reward,
                Id: 0xdc, SubId: 0x00, Y: 0x48, X: 0x78,
                RequiredCount: 2,
                TreasureObject: "TREASURE_OBJECT_GRAVEYARD_KEY_00"
            } ||
            records[1] is not
            {
                Order: 1, Kind: DarkRoomDatabaseObjectKind.Handler,
                Id: 0x08, SubId: 0x00, Parameter: 0x50
            } ||
            database.GetRoomRecords(5, 0xa8) is not
            [{ Kind: DarkRoomDatabaseObjectKind.Handler, Order: 0 }])
        {
            throw new InvalidOperationException(
                "Rooms 5:a8/5:ed did not retain their imported dark-room object order.");
        }

        TreasureObjectRecord keyObject =
            _treasures.GetObject("TREASURE_OBJECT_GRAVEYARD_KEY_00");
        TreasureObjectVisualRecord keyVisual =
            _treasures.GetObjectVisual(keyObject.Graphic);
        if (keyObject is not
            {
                TreasureId: TreasureDatabase.TreasureGraveyardKey,
                SubId: 0x00, Parameter: 0x00, TextId: 0x23, Graphic: 0x44
            } ||
            keyVisual is not
            {
                Sprite: "spr_map_compass_keys_bookofseals",
                TileBase: 0x0e, Palette: 0x05, DefaultAnimation: 0x00
            } ||
            string.IsNullOrEmpty(keyObject.Message))
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_GRAVEYARD_KEY_00 no longer matches $42/$00/TX_0023/graphic $44.");
        }

        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlagItem, value: false);
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40);
        _sound.ClearPlayRequestAudit();
        Func<Vector2, Vector2> priorWorldToScreen = _entities.WorldToScreen;
        _entities.WorldToScreen = static position => position - new Vector2(0, 32);
        LoadValidationRoom(group, roomId);
        _player.WarpTo(new Vector2(24, 152));

        OracleRoomData room = _currentRoom;
        DarkRoomHandlerRoomEntity handler =
            _entities.Entities<DarkRoomHandlerRoomEntity>().Single();
        if (_entities.Entities<DarkRoomRewardRoomEntity>().Count != 1 ||
            _entities.Entities<LightableTorchRoomEntity>().Count != 0 ||
            room.Layout.Length != 176 || room.Layout[0x33] != 0x08 ||
            room.Layout[0x3b] != 0x08 ||
            room.TemporaryBackgroundPaletteOffset != -16 ||
            handler.State.Parameter != 0xf0 || handler.State.RenderedOffset != -16)
        {
            throw new InvalidOperationException(
                "Room 5:ed did not enter fully dark with two unlit source metatiles and ordered controllers.");
        }

        // The handler's state-0 scan runs in the ordinary entity update and
        // creates both child parts in packed-address order.
        _entities.Update(1.0 / 60.0, _player);
        List<LightableTorchRoomEntity> torches =
            _entities.Entities<LightableTorchRoomEntity>();
        if (!handler.Initialized || handler.State.TotalTorches != 2 ||
            torches.Select(torch => torch.PackedPosition).ToArray() is not [0x33, 0x3b])
        {
            throw new InvalidOperationException(
                "PART_DARK_ROOM_HANDLER $08 did not scan all 176 layout bytes into torches $33/$3b.");
        }

        var noSpawns = new List<RoomEntitySpawn>();
        LightableTorchRoomEntity left = torches[0];
        SeedRecord emberRecord =
            new SeedSatchelDatabase().Ember;
        _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            left.Position - emberRecord.RightOffset,
            Vector2I.Right, emberRecord, group));
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<EmberSeedEffect>().Count != 0 ||
            !left.HitPending || handler.State.LitCount != 0 ||
            room.Layout[0x33] != 0x08)
        {
            throw new InvalidOperationException(
                "The first 5:ed torch did not consume its Ember Seed without a fire animation " +
                "and retain the original hit-to-state-2 update delay.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (handler.State.LitCount != 1 || room.Layout[0x33] != 0x09 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch) != 1 ||
            handler.State.FadeActive)
        {
            throw new InvalidOperationException(
                "The first 5:ed torch did not light on its following object update with SND_LIGHTTORCH.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (!handler.State.FadeActive || handler.State.Parameter != 0xf7 ||
            handler.State.RenderedOffset != -16)
        {
            throw new InvalidOperationException(
                "The first lit torch did not begin the $f0->$f7 partial brighten on the following handler update.");
        }
        for (int update = 0; update < 6; update++)
            _entities.Update(1.0 / 60.0, _player);
        if (!handler.State.FadeActive || handler.State.RenderedOffset != -10 ||
            room.TemporaryBackgroundPaletteOffset != -10)
        {
            throw new InvalidOperationException(
                "The partial dark-room palette did not render offsets $f1-$f6 at one component step per update.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (handler.State.FadeActive || handler.State.RenderedOffset != -10)
        {
            throw new InvalidOperationException(
                "The partial brighten did not stop before rendering target offset $f7.");
        }

        LightableTorchRoomEntity right =
            _entities.Entities<LightableTorchRoomEntity>().Single();
        if (right.PackedPosition != 0x3b || right.ApplySeedHit(
                right.CollisionBounds, right.Position, noSpawns) !=
                SeedHitResult.Consume)
        {
            throw new InvalidOperationException("The second 5:ed torch rejected an Ember Seed collision.");
        }
        _entities.Update(1.0 / 60.0, _player);
        if (handler.State.LitCount != 2 || room.Layout[0x3b] != 0x09 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0)
        {
            throw new InvalidOperationException(
                "The second torch did not light one update before the ordered $dc:$00 reward check.");
        }
        _entities.Update(1.0 / 60.0, _player);
        GroundTreasurePickup key =
            _entities.Entities<GroundTreasurePickup>().Single();
        if (_entities.Entities<DarkRoomRewardRoomEntity>().Count != 0 ||
            key.Record is not
            {
                TreasureObject: "TREASURE_OBJECT_GRAVEYARD_KEY_00",
                SpawnMode: 2, GrabMode: 1, SpawnDelayFrames: 40,
                BounceCount: 2, Gravity: 0x10, BounceSpeed: -0xaa,
                SpawnSound: OracleSoundEngine.SndSolvePuzzle,
                LandingSound: OracleSoundEngine.SndDropEssence,
                InitialZAboveScreen: true
            } ||
            key.Position != new Vector2(0x78, 0x48) || key.Visible ||
            !handler.State.FadeActive || handler.State.Parameter != 0)
        {
            throw new InvalidOperationException(
                "Exactly two torches did not create the falling one-hand Graveyard Key before full brightening.");
        }

        _entities.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (key.State != PickupState.Spawning ||
            key.SpawnCounter != 40 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1)
        {
            throw new InvalidOperationException(
                "The Graveyard Key did not begin its 40-update SND_SOLVEPUZZLE delay.");
        }
        for (int update = 0; update < 39; update++)
            _entities.Update(1.0 / 60.0, _player);
        if (key.SpawnCounter != 1 || key.Visible)
            throw new InvalidOperationException("The falling key appeared before delay update 40.");
        _entities.Update(1.0 / 60.0, _player);
        if (key.ZFixed != -48 << 8 || !key.Visible)
        {
            throw new InvalidOperationException(
                $"objectGetZAboveScreen for 5:ed expected -48, got {key.ZFixed >> 8}.");
        }

        for (int update = 0;
             update < 240 && key.State != PickupState.Waiting;
             update++)
        {
            _entities.Update(1.0 / 60.0, _player);
        }
        if (key.State != PickupState.Waiting ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence) != 2 ||
            handler.State.FadeActive || handler.State.RenderedOffset != -1 ||
            room.TemporaryBackgroundPaletteOffset != -1)
        {
            throw new InvalidOperationException(
                "The Graveyard Key did not bounce twice while the room finished at the original retained $ff offset.");
        }

        _sound.ClearPlayRequestAudit();
        _player.WarpTo(key.Position);
        _entities.Update(1.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureGraveyardKey) ||
            !_saveData.HasRoomFlag(group, roomId, OracleSaveData.RoomFlagItem) ||
            key.State != PickupState.Collected ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1 ||
            !_dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Collecting the Graveyard Key did not grant treasure $42, set only ROOMFLAG_ITEM, and open TX_0023.");
        }
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        if (!key.Held || !_player.IsHoldingItemOneHand ||
            key.Position != _player.Position + new Vector2(-4, -14) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2)
        {
            throw new InvalidOperationException(
                "The collected Graveyard Key did not use its one-hand held pose and second SND_GETITEM.");
        }
        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);

        // ROOMFLAG_ITEM suppresses only the reward spawner. The dark-room
        // handler and its two permanent torch children still initialize on
        // re-entry; unrelated room flag $40 never suppressed the reward.
        LoadValidationRoom(group, roomId);
        _entities.Update(1.0 / 60.0, _player);
        if (_entities.Entities<DarkRoomRewardRoomEntity>().Count != 0 ||
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.Entities<LightableTorchRoomEntity>().Count != 2 ||
            room.Layout[0x33] != 0x08 || room.Layout[0x3b] != 0x08 ||
            room.TemporaryBackgroundPaletteOffset != -16)
        {
            throw new InvalidOperationException(
                "Room 5:ed re-entry did not reset its torches/darkness while ROOMFLAG_ITEM suppressed only the key.");
        }

        LoadValidationRoom(5, 0xa8);
        _entities.Update(1.0 / 60.0, _player);
        DarkRoomHandlerRoomEntity roomA8 =
            _entities.Entities<DarkRoomHandlerRoomEntity>().Single();
        if (_entities.Entities<DarkRoomRewardRoomEntity>().Count != 0 ||
            roomA8.State.TotalTorches != 2 ||
            _entities.Entities<LightableTorchRoomEntity>()
                .Select(torch => torch.PackedPosition).ToArray() is not [0x56, 0x58] ||
            _currentRoom.TemporaryBackgroundPaletteOffset != -16)
        {
            throw new InvalidOperationException(
                "Room 5:a8 did not reuse the same source-ordered dark-room torch handler.");
        }

        _entities.WorldToScreen = priorWorldToScreen;
        _saveData.SetRoomFlag(group, roomId, OracleSaveData.RoomFlag40, value: false);
        GD.Print("Validated rooms 5:a8/5:ed PART_DARK_ROOM_HANDLER scanning, permanent " +
            "Ember torches, exact palette-thread offsets, $dc:$00 item predicate/order, " +
            "falling Graveyard Key timing/sounds/one-hand collection, and re-entry.");
    }
}

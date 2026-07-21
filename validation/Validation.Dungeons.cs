using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePushBlocks()
    {
        _sound.ClearPlayRequestAudit();
        OracleRoomData directionalRoom = _rooms.Load(4, 0x09);
        _roomView.SetRoom(directionalRoom.Texture);
        _entities.LoadRoom(4, directionalRoom);

        // The left dungeon wall at this position sets the original $0c
        // adjacent-wall mask. Link should use the pushing walk variant only
        // while the held direction matches the side he is facing.
        _player.WarpTo(new Vector2(20, 24));
        _player.Face(Vector2I.Left);
        _player.UpdatePushingState(Vector2.Left);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation against the ordinary wall in 4:09.");
        }
        _player.UpdatePushingState(Vector2.Right);
        if (_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link retained his pushing animation without holding toward the wall.");
        }

        Vector2 rightOnlyBlock = new(0x0b * 16 + 8, 0x01 * 16 + 8);
        Vector2 linkAbove = rightOnlyBlock + new Vector2(0, -10);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkAbove, Vector2I.Down, Vector2.Down);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames ||
            directionalRoom.GetMetatile(rightOnlyBlock) != 0x19 ||
            directionalRoom.GetCollision(
                directionalRoom.GetMetatile(rightOnlyBlock + Vector2.Down * 16)) != 0)
        {
            throw new InvalidOperationException(
                "Right-only block $19 accepted a downward push toward clear floor in 4:09.");
        }

        OracleRoomData room = _rooms.Load(4, 0x08);
        _roomView.SetRoom(room.Texture);
        _entities.LoadRoom(4, room);

        // Dungeon 0 room $08 has an all-direction tile $1c at packed
        // position $4b. Its right neighbor is the solid one-way block $19,
        // while the tile above is clear floor, so both rejection and a full
        // successful push can be checked against unmodified original data.
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        Vector2 linkLeft = blockCenter + new Vector2(-10, 0);
        Vector2 linkRight = blockCenter + new Vector2(10, 0);
        if (room.ActiveCollisions != 2 || room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                $"Expected dungeon collision mode 2 and block $1c at 4:08/$4b, got " +
                $"mode {room.ActiveCollisions} / tile ${room.GetMetatile(blockCenter):x2}.");
        }

        _player.WarpTo(linkBelow);
        _player.Face(Vector2I.Up);
        _player.UpdatePushingState(Vector2.Up);
        if (!_player.IsPushing)
        {
            throw new InvalidOperationException(
                "Link did not enter his pushing animation while pressing block $1c in 4:08.");
        }

        Vector2 cornerApproach = new(blockCenter.X - 6, linkBelow.Y);
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                cornerApproach, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames !=
            PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Block $1c accepted a push while Link occupied a metatile corner.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkRight, Vector2I.Left, Vector2.Left);
        }
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 0)
        {
            throw new InvalidOperationException(
                "An accepted push did not request SND_MOVEBLOCK exactly once at movement start.");
        }
        for (int frame = 0; frame < PushBlockController.MoveFrames; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 holeCenter = blockCenter + Vector2.Left * 16;
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(holeCenter) != 0xf5 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 1 ||
            _entities.Entities<FallingDownHoleEffect>() is not
                [{ ElapsedUpdates: 0, AnimationFrame: 0 }])
        {
            throw new InvalidOperationException(
                "Block $1c did not become INTERAC_FALLDOWNHOLE over destination " +
                "hole $f5 with SND_FALLINHOLE.");
        }
        FallingDownHoleEffect fallingBlock =
            _entities.Entities<FallingDownHoleEffect>()[0];
        if (!fallingBlock.PrecisePosition.IsEqualApprox(
            holeCenter + new Vector2(0, -2)))
        {
            throw new InvalidOperationException(
                "The falling block interaction did not inherit the pushed block's Y-2 center.");
        }
        for (int frame = 0; frame < 7; frame++)
            fallingBlock.UpdateFrame();
        if (fallingBlock.AnimationFrame != 0)
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE left its first frame before eight updates.");
        fallingBlock.UpdateFrame();
        if (fallingBlock.AnimationFrame != 1 ||
            OracleObjectMath.ToPixelPosition(fallingBlock.PrecisePosition) != holeCenter)
        {
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE did not center at SPEED_60 or advance after eight updates.");
        }
        for (int frame = 0; frame < 12; frame++)
            fallingBlock.UpdateFrame();
        if (fallingBlock.AnimationFrame != 2)
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE did not retain its second frame for 12 updates.");
        for (int frame = 0; frame < 12; frame++)
            fallingBlock.UpdateFrame();
        if ((fallingBlock.CurrentParameter & 0x80) == 0 || fallingBlock.Finished)
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE did not reach its terminal animation parameter after 8/12/12 updates.");
        fallingBlock.UpdateFrame();
        if (!fallingBlock.Finished)
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE did not delete one update after its terminal parameter.");
        if (!room.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks))
            throw new InvalidOperationException("Could not restore 4:08/$4b after the hole test.");

        _pushBlocks.UpdatePushAttempt(linkLeft, Vector2I.Right, Vector2.Right);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames ||
            _pushBlocks.Active)
        {
            throw new InvalidOperationException(
                "Block $1c started moving right even though destination tile $19 is solid.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames - 1; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (_pushBlocks.Active || _pushBlocks.RemainingPushFrames != 1 ||
            room.GetMetatile(blockCenter) != 0x1c)
        {
            throw new InvalidOperationException(
                "Block $1c moved before Link completed the original 20-update push delay.");
        }

        _pushBlocks.UpdatePushAttempt(linkBelow, Vector2I.Up, Vector2.Zero);
        if (_pushBlocks.RemainingPushFrames != PushBlockController.PushDelayFrames)
        {
            throw new InvalidOperationException(
                "Releasing the direction did not reset wPushingAgainstTileCounter to 20.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
        {
            _pushBlocks.UpdatePushAttempt(
                linkBelow, Vector2I.Up, Vector2.Up);
        }
        if (!_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            !_collision.Collides(blockCenter + new Vector2(0, -2)) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 2)
        {
            throw new InvalidOperationException(
                "Block $1c did not become a Link-blocking object over source floor $a0.");
        }

        for (int frame = 0; frame < PushBlockController.MoveFrames - 1; frame++)
        {
            _pushBlocks.Advance(1.0 / 60.0);
        }
        Vector2 expectedTopLeft = new(blockCenter.X - 8, blockCenter.Y - 8 - 15.5f);
        if (!_pushBlocks.Active ||
            !_pushBlocks.BlockTopLeft.IsEqualApprox(expectedTopLeft) ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0xa0)
        {
            throw new InvalidOperationException(
                "Block $1c did not move at SPEED_80 for the first 31 updates.");
        }

        _pushBlocks.Advance(1.0 / 60.0);
        if (_pushBlocks.Active || room.GetMetatile(blockCenter) != 0xa0 ||
            room.GetMetatile(blockCenter + Vector2.Up * 16) != 0x1d ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndFallInHole) != 1)
        {
            throw new InvalidOperationException(
                "Block $1c did not finish after 32 updates as destination tile $1d.");
        }

        GD.Print("Validated Link's wall/block pushing animation, directional/corner " +
            "restrictions, SND_MOVEBLOCK, hole SND_FALLINHOLE, imported " +
            "INTERAC_FALLDOWNHOLE timing/motion, hazard disposal, " +
            "4:08/$4b push delay reset, blocked " +
            "destination, source $a0 replacement, SPEED_80 movement, moving " +
            "collision, and destination tile $1d.");
    }

    private void ValidateDungeonKeyDoors()
    {
        const int group = 4;
        const int roomId = 0x0a;
        const int neighborRoomId = 0x09;
        const byte roomDoorFlag = 0x08;
        const byte neighborDoorFlag = 0x02;
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var database = new DungeonKeyDoorDatabase();
        if (database.Count != 4 ||
            !database.TryGet(0x73, out DungeonKeyDoorDatabase.Record leftDoor) ||
            leftDoor.Direction != Vector2I.Left || leftDoor.OpenTile != 0xa0 ||
            leftDoor.PushCounter != 20 || leftDoor.DoorFrameWait != 6 ||
            leftDoor.NoKeyTextId != 0x5100)
        {
            throw new InvalidOperationException(
                "The imported $70-$73 small-key door table is incomplete.");
        }

        byte originalRoomFlags = _saveData.GetRoomFlags(group, roomId);
        byte originalNeighborFlags = _saveData.GetRoomFlags(group, neighborRoomId);
        _saveData.SetRoomFlag(group, roomId, roomDoorFlag, value: false);
        _saveData.SetRoomFlag(group, neighborRoomId, neighborDoorFlag, value: false);
        LoadValidationRoom(group, roomId);
        OracleRoomData room = _currentRoom;
        Vector2 doorCenter = new(0x08, 0x58);
        Vector2 linkRightOfDoor = new(0x12, 0x58);
        int dungeon = _rooms.CurrentDungeonIndex;
        if (dungeon != 0x0d || room.GetMetatile(doorCenter) != 0x73 ||
            !room.IsSolid(doorCenter) || !_hud.DungeonKeyDisplayActive ||
            _hud.DungeonIndex != dungeon)
        {
            throw new InvalidOperationException(
                "Room 4:0a did not load its solid left small-key door $73 or dungeon-$0d HUD mode.");
        }

        TreasureDatabase.TreasureObjectRecord smallKey =
            _treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03");
        int originalKeys = _inventory.GetDungeonSmallKeys(dungeon);
        while (_inventory.TryUseDungeonSmallKey(dungeon))
        {
        }
        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 10; frame++)
        {
            _keyDoors.UpdatePushAttempt(
                linkRightOfDoor, Vector2I.Left, Vector2.Left);
        }
        if (!_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You need a key\nfor this door!" ||
            room.GetMetatile(doorCenter) != 0x73 ||
            _saveData.HasRoomFlag(group, roomId, roomDoorFlag) ||
            _saveData.HasRoomFlag(group, neighborRoomId, neighborDoorFlag) ||
            _entities.Entities<DungeonKeyUseEffect>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:0a did not show TX_5100 without consuming a key or opening its door.");
        }
        _dialogue.Close();
        for (int index = 0; index < originalKeys; index++)
            _inventory.GiveTreasure(smallKey);
        if (originalKeys == 0)
            _inventory.GiveTreasure(smallKey);

        int keysBeforeOpen = _inventory.GetDungeonSmallKeys(dungeon);
        _sound.ClearPlayRequestAudit();
        for (int frame = 0; frame < 9; frame++)
        {
            _keyDoors.UpdatePushAttempt(
                linkRightOfDoor, Vector2I.Left, Vector2.Left);
        }
        if (_keyDoors.Opening || _keyDoors.RemainingPushFrames != 2 ||
            _inventory.GetDungeonSmallKeys(dungeon) != keysBeforeOpen ||
            room.GetMetatile(doorCenter) != 0x73)
        {
            throw new InvalidOperationException(
                "Small-key door $73 opened before nextToKeyDoor's doubled 20-to-zero counter elapsed.");
        }

        _keyDoors.UpdatePushAttempt(
            linkRightOfDoor, Vector2I.Left, Vector2.Left);
        if (!_keyDoors.Opening || _keyDoors.OpeningCounter != 6 ||
            _inventory.GetDungeonSmallKeys(dungeon) != keysBeforeOpen - 1 ||
            room.GetMetatile(doorCenter) != 0xa0 || !room.IsSolid(doorCenter) ||
            !_saveData.HasRoomFlag(group, roomId, roomDoorFlag) ||
            !_saveData.HasRoomFlag(group, neighborRoomId, neighborDoorFlag) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1 ||
            _entities.Entities<DungeonKeyUseEffect>() is not
                [{ Phase: 0, Counter: 8, Z: -4 }])
        {
            throw new InvalidOperationException(
                "Room 4:0a did not consume one dungeon key, set both directional flags, " +
                "and begin its still-solid interleaved/key-sprite frame.");
        }
        if (_hud.StatusMapTileForValidation(0x0a) != 0x04 ||
            _hud.StatusMapTileForValidation(0x0b) != 0x1b ||
            _hud.StatusMapTileForValidation(0x0c) !=
                0x10 + _inventory.GetDungeonSmallKeys(dungeon) ||
            _hud.StatusMapTileForValidation(0x2a) !=
                0x10 + Mathf.Clamp(_hud.Rupees, 0, 999) / 100 ||
            _hud.StatusMapTileForValidation(0x2b) !=
                0x10 + Mathf.Clamp(_hud.Rupees, 0, 999) / 10 % 10 ||
            _hud.StatusMapTileForValidation(0x2c) !=
                0x10 + Mathf.Clamp(_hud.Rupees, 0, 999) % 10)
        {
            throw new InvalidOperationException(
                "The dungeon HUD did not draw gfx_key/X/key count together with the rupee digits.");
        }

        DungeonKeyUseEffect keyEffect = _entities.Entities<DungeonKeyUseEffect>()[0];
        for (int frame = 0; frame < 7; frame++)
            keyEffect.UpdateFrame();
        if (keyEffect.Phase != 0 || keyEffect.Counter != 1 || keyEffect.Z != -4)
            throw new InvalidOperationException(
                "INTERAC_DUNGEON_KEY_SPRITE left its first Z=$fc phase before eight updates.");
        keyEffect.UpdateFrame();
        if (keyEffect.Phase != 1 || keyEffect.Counter != 20 || keyEffect.Z != -8)
            throw new InvalidOperationException(
                "INTERAC_DUNGEON_KEY_SPRITE did not enter its Z=$f8 20-update phase.");
        for (int frame = 0; frame < 20; frame++)
            keyEffect.UpdateFrame();
        if (!keyEffect.Finished)
            throw new InvalidOperationException(
                "INTERAC_DUNGEON_KEY_SPRITE did not delete after 8+20 updates.");

        for (int frame = 0; frame < leftDoor.DoorFrameWait - 1; frame++)
            _keyDoors.Advance(update);
        if (!room.IsSolid(doorCenter) || _keyDoors.OpeningCounter != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Small-key door $73 finalized before six interleaved updates elapsed.");
        }
        _keyDoors.Advance(update);
        if (_keyDoors.Opening || room.IsSolid(doorCenter) ||
            room.GetMetatile(doorCenter) != 0xa0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Small-key door $73 did not finalize open tile $a0 and SND_DOORCLOSE on update 6.");
        }

        LoadValidationRoom(group, roomId);
        if (_currentRoom.GetMetatile(doorCenter) != 0xa0 ||
            _currentRoom.IsSolid(doorCenter))
        {
            throw new InvalidOperationException(
                "Room 4:0a did not substitute its persisted left-door flag to open tile $a0 on re-entry.");
        }

        while (_inventory.GetDungeonSmallKeys(dungeon) < originalKeys)
            _inventory.GiveTreasure(smallKey);
        _saveData.SetRoomFlag(group, roomId, roomDoorFlag, value: false);
        _saveData.SetRoomFlag(group, neighborRoomId, neighborDoorFlag, value: false);
        if ((originalRoomFlags & roomDoorFlag) != 0)
            _saveData.SetRoomFlag(group, roomId, roomDoorFlag);
        if ((originalNeighborFlags & neighborDoorFlag) != 0)
            _saveData.SetRoomFlag(group, neighborRoomId, neighborDoorFlag);
        LoadValidationRoom(group, roomId);

        GD.Print("Validated imported small-key doors, TX_5100 no-key handling, " +
            "10-update push activation, per-dungeon key consumption and HUD " +
            "key/rupee coexistence, " +
            "paired dungeon-layout flags, INTERAC_DUNGEON_KEY_SPRITE 8+20 timing, " +
            "six-update interleaved opening, and re-entry substitution.");
    }

    private void ValidateDungeonMechanics()
    {
        const double update = 1.0 / OracleSoundEngine.UpdatesPerSecond;
        var database = new DungeonMechanicDatabase();
        int buttonRecordCount = 0;
        int triggerDoorRecordCount = 0;
        int permanentTriggerChestCount = 0;
        int retractableTriggerChestCount = 0;
        for (int group = 0; group < 8; group++)
        for (int roomId = 0; roomId < 0x100; roomId++)
        {
            foreach (DungeonMechanicDatabase.Record record in
                database.GetRoomRecords(group, roomId))
            {
                buttonRecordCount += record.Id == 0x09 ? 1 : 0;
                triggerDoorRecordCount += record is
                    { Id: 0x1e, SubId: >= 0x04 and <= 0x07 } ? 1 : 0;
                permanentTriggerChestCount += record is
                    { Id: 0x20, SubId: 0x00 } ? 1 : 0;
                retractableTriggerChestCount += record is
                    { Id: 0x21, SubId: 0x17 } ? 1 : 0;
            }
        }
        if (database.RecordCount != 155 || buttonRecordCount != 49 ||
            triggerDoorRecordCount != 20 ||
            permanentTriggerChestCount != 7 ||
            retractableTriggerChestCount != 6 ||
            database.GetRoomRecords(4, 0x0c).Select(record => record.Order)
                .ToArray() is not [0, 1])
        {
            throw new InvalidOperationException(
                "Expected 49 buttons, 20 trigger shutters, seven delayed and " +
                "six retractable trigger chests, and 73 ordered " +
                "$13:$01/enemy-shutter dungeon placements.");
        }

        void Step() => _entities.Update(update, _player);
        _entities.WorldToScreen = static position => position;

        // Room 4:08 uses the dungeon-$0d table's $20:$00 script: exact
        // wActiveTriggers == $01, solve cue plus INTERAC_PUFF, wait 15, then
        // TILEINDEX_CHEST at packed $57. The script object precedes the button,
        // so it observes the press on the following update.
        _saveData.SetRoomFlag(
            4, 0x08, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x08);
        OracleRoomData room = _currentRoom;
        Vector2 room408Button = new(0x78, 0x18);
        Vector2 room408Chest = new(0x78, 0x58);
        _player.EndNewGameSlowFall();
        _player.WarpTo(new Vector2(0x48, 0x78));
        _sound.ClearPlayRequestAudit();
        if (_entities.Entities<TriggerChestRoomEntity>() is not
            [{
                Id: 0x20,
                SubId: 0x00,
                PackedPosition: 0x57,
                TriggerParameter: 0x01,
                Predicate: DungeonMechanicDatabase.TriggerPredicate.Exact
            }] ||
            _entities.Entities<GroundButtonRoomEntity>() is not
            [{ SubId: 0x00, PackedPosition: 0x17, Reusable: false }] ||
            room.GetMetatile(room408Button) != 0x0c ||
            room.GetMetatile(room408Chest) != 0xa3)
        {
            throw new InvalidOperationException(
                "Room 4:08 did not instantiate its ordered exact-trigger chest script and button.");
        }
        Step();
        _player.WarpTo(room408Button);
        Step();
        if (_entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(room408Button) != 0x0d ||
            room.GetMetatile(room408Chest) != 0xa3 ||
            _entities.Entities<GroundButtonRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:08's button did not latch before its earlier $20:$00 script slot reacted.");
        }
        Step();
        if (_entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 15 }] ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 1, AnimationFrame: 0 }] ||
            room.GetMetatile(room408Chest) != 0xa3 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:08 did not request solve/poof and begin the exact 15-update chest wait.");
        }
        for (int frame = 0; frame < database.ChestWait - 1; frame++)
            Step();
        if (room.GetMetatile(room408Chest) != 0xa3 ||
            _entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 1 }] ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 15, AnimationFrame: 2 }])
        {
            throw new InvalidOperationException(
                "Room 4:08 installed its chest before wait 15 reached zero.");
        }
        Step();
        if (room.GetMetatile(room408Chest) != 0xf1 ||
            _entities.Entities<TriggerChestRoomEntity>().Count != 0 ||
            _entities.Entities<PuzzlePuffEffect>() is not
                [{ ElapsedUpdates: 16, AnimationFrame: 2 }])
        {
            throw new InvalidOperationException(
                "Room 4:08 did not install chest tile $f1 on the wait-15 zero update.");
        }
        Step();
        Step();
        Step();
        if (_entities.Entities<PuzzlePuffEffect>() is not
            [{ ElapsedUpdates: 19, CurrentParameter: 0xff }])
        {
            throw new InvalidOperationException(
                "INTERAC_PUFF did not reach its imported terminal parameter after 6/8/4 updates.");
        }
        Step();
        if (_entities.Entities<PuzzlePuffEffect>().Count != 0)
            throw new InvalidOperationException(
                "INTERAC_PUFF did not delete one update after terminal parameter $ff.");

        TreasureDatabase.TreasureObjectVisualRecord smallKeyVisual =
            _treasures.GetObjectVisual(0x42);
        if (smallKeyVisual.Sprite != "spr_map_compass_keys_bookofseals" ||
            smallKeyVisual.TileBase != 0x0c || smallKeyVisual.Palette != 5 ||
            smallKeyVisual.DefaultAnimation != 0)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_SMALL_KEY_03 did not import INTERAC_TREASURE graphic $42 exactly.");
        }
        int dungeon = _rooms.CurrentDungeonIndex;
        int keysBefore = _inventory.GetDungeonSmallKeys(dungeon);
        _player.WarpTo(room408Chest + Vector2.Down * 12.0f);
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _interactions.ChestReward is not { VisualGraphic: 0x42 } keyReward ||
            room.GetMetatile(room408Chest) != 0xf0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:08's revealed chest did not open as graphic $42 with SND_OPENCHEST.");
        }
        var rupeeReward = new ChestTreasureEffect();
        rupeeReward.Initialize(Vector2.Zero, _treasures.GetObjectVisual(0x2b));
        if (OracleGraphicsCache.PixelHash(keyReward.RewardTexture.GetImage()) ==
            OracleGraphicsCache.PixelHash(rupeeReward.RewardTexture.GetImage()))
        {
            throw new InvalidOperationException(
                "Room 4:08's small-key reward still rendered as the rupee graphic $2b.");
        }
        rupeeReward.Free();
        _interactions.Update(31.0 / 60.0, _player);
        if (_inventory.GetDungeonSmallKeys(dungeon) != keysBefore ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:08 granted its key or SND_GETITEM before the 32-frame rise ended.");
        }
        _interactions.Update(1.0 / 60.0, _player);
        if (_inventory.GetDungeonSmallKeys(dungeon) != keysBefore + 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetSeed) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1 ||
            !_dialogue.IsOpen)
        {
            throw new InvalidOperationException(
                "Room 4:08 did not grant its key with SND_GETSEED then SND_GETITEM after 32 frames.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        LoadValidationRoom(4, 0x08);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x48, 0x78));
        Step();
        if (_entities.Entities<TriggerChestRoomEntity>().Count != 0 ||
            room.GetMetatile(room408Chest) != 0xf0)
        {
            throw new InvalidOperationException(
                "Room 4:08 did not retire $20:$00 and load its opened chest " +
                $"for ROOMFLAG_ITEM: entities={_entities.Entities<TriggerChestRoomEntity>().Count}, " +
                $"tile=${room.GetMetatile(room408Chest):x2}, " +
                $"flag={_saveData.HasRoomFlag(4, 0x08, OracleSaveData.RoomFlagItem)}.");
        }
        _saveData.SetRoomFlag(
            4, 0x08, OracleSaveData.RoomFlagItem, value: false);

        // $21:$17 is the reusable companion consumer: exact trigger equality
        // creates its chest immediately and pressure release restores the
        // original room-buffer tile with another puff but no solve cue.
        _saveData.SetRoomFlag(
            4, 0x7a, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(4, 0x7a);
        room = _currentRoom;
        Vector2 retractableChest = new(0x98, 0x38);
        Vector2 retractableButton = new(0x48, 0x18);
        byte retractableOriginal = room.GetOriginalMetatile(retractableChest);
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        Step();
        _player.WarpTo(retractableButton);
        Step();
        Step();
        if (room.GetMetatile(retractableChest) != 0xf1 ||
            _entities.Entities<TriggerChestRoomEntity>() is not
            [{ Id: 0x21, Predicate: DungeonMechanicDatabase.TriggerPredicate.Exact }] ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:7a's $21:$17 did not create its exact-$01 reusable chest.");
        }
        _player.WarpTo(new Vector2(0x78, 0x78));
        Step();
        Step();
        if (_entities.ActiveTriggers != 0 ||
            room.GetMetatile(retractableChest) != retractableOriginal ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndPoof) != 2 ||
            _entities.Entities<TriggerChestRoomEntity>().Count != 1)
        {
            throw new InvalidOperationException(
                "Room 4:7a's $21:$17 did not retract to the source tile without another solve cue.");
        }

        _sound.ClearPlayRequestAudit();
        LoadValidationRoom(4, 0x0c);
        room = _currentRoom;
        Vector2 block = new(0x78, 0x48);
        Vector2 door = new(0x78, 0x08);
        if (_entities.Entities<PushBlockTriggerRoomEntity>() is not [{ PackedPosition: 0x47 }] ||
            _entities.Entities<DungeonDoorRoomEntity>() is not
                [{ SubId: 0x08, PackedPosition: 0x07 }] ||
            room.GetMetatile(block) != 0x18 || room.GetMetatile(door) != 0x78)
        {
            throw new InvalidOperationException(
                "Room 4:0c did not instantiate ordered trigger $13:$01 then up shutter $1e:$08.");
        }

        Step();
        if (room.GetMetatile(block) != 0x1d || room.GetMetatile(door) != 0x78)
            throw new InvalidOperationException(
                "Room 4:0c update 1 did not install trigger tile $1d while retaining shutter $78.");
        Step();
        if (room.GetMetatile(block) != 0x18 || room.GetMetatile(door) != 0x78)
            throw new InvalidOperationException(
                "Room 4:0c update 2 did not restore the source push block before arming it.");

        Vector2 linkBelow = block + Vector2.Down * 10.0f;
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _pushBlocks.UpdatePushAttempt(linkBelow, Vector2I.Up, Vector2.Up);
        if (!_pushBlocks.Active || room.GetMetatile(block) != 0xa0)
            throw new InvalidOperationException(
                "Room 4:0c's up-only trigger block did not start its common push movement.");

        // State 2 observes the source-layout write, then state 3 installs and
        // decrements its own $1e counter on the following 30 updates.
        Step();
        for (int frame = 0; frame < database.PushDelay - 1; frame++)
            Step();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
            room.GetMetatile(door) != 0x78)
        {
            throw new InvalidOperationException(
                "Room 4:0c released its synthetic enemy before the 30-update trigger delay.");
        }
        Step();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 0 ||
            room.GetMetatile(door) != 0x78)
        {
            throw new InvalidOperationException(
                "Room 4:0c did not clear wNumEnemies and request SND_SOLVEPUZZLE on update 30.");
        }

        for (int frame = 0; frame < database.SolveWait - 1; frame++)
            Step();
        if (room.GetMetatile(door) != 0x78)
            throw new InvalidOperationException(
                "Room 4:0c began opening before the exact eight-update solve wait.");
        Step();
        if (room.GetMetatile(door) != 0x78)
            throw new InvalidOperationException(
                "Room 4:0c opened in the same update that wait 8 reached zero.");

        // OracleWorldData caches mutable room instances; use an isolated world
        // so preparing the open mapping cannot alter the active door state.
        OracleRoomData reference = new OracleWorldData().LoadRoom(4, 0x0c);
        Vector2 topLeftSample = door + new Vector2(-4, -4);
        Vector2 bottomLeftSample = door + new Vector2(-4, 4);
        Color expectedClosedBottom = reference.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));
        if (!reference.ReplaceMetatile(door, 0x78, 0xa0, (long)_animationTicks))
            throw new InvalidOperationException("Could not prepare the 4:0c open-door reference tile.");
        Color expectedOpenBottom = reference.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));

        Step();
        Color actualInterleavedTop = room.GetRenderedPixelForValidation(
            new Vector2I((int)topLeftSample.X, (int)topLeftSample.Y));
        Color actualInterleavedBottom = room.GetRenderedPixelForValidation(
            new Vector2I((int)bottomLeftSample.X, (int)bottomLeftSample.Y));
        if (room.GetMetatile(door) != 0xa0 || !room.IsSolid(door) ||
            !actualInterleavedTop.IsEqualApprox(expectedClosedBottom) ||
            !actualInterleavedBottom.IsEqualApprox(expectedOpenBottom) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:0c did not install the type-0 mapping-interleaved, still-solid " +
                $"door frame: tile=${room.GetMetatile(door):x2}, solid={room.IsSolid(door)}, " +
                $"top={actualInterleavedTop}/{expectedClosedBottom}, " +
                $"bottom={actualInterleavedBottom}/{expectedOpenBottom}, " +
                $"SND_DOORCLOSE={_sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose)}.");
        }

        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        if (!room.IsSolid(door) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 1)
        {
            throw new InvalidOperationException(
                "Room 4:0c finalized the shutter before six interleaved updates elapsed.");
        }
        Step();
        if (room.GetMetatile(door) != 0xa0 || room.IsSolid(door) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:0c did not finalize open tile $a0 and SND_DOORCLOSE on update 6.");
        }

        // replaceShutterForLinkEntering temporarily opens only the shutter at
        // Link's incoming packed position. Destination objects remain frozen
        // throughout the scroll; afterward $1e:$0b waits until Link no longer
        // overlaps its $08/$0a radii before beginning the six-update close.
        _player.WarpTo(new Vector2(0xe8, 0x58));
        _sound.ClearPlayRequestAudit();
        _transitions.BeginScroll(_player, Vector2I.Right, 0x0b);
        OracleRoomData scrollingRoom40b = _world.LoadRoom(4, 0x0b);
        Vector2 scrollingUpDoor = new(0x78, 0x08);
        Vector2 scrollingLeftDoor = new(0x08, 0x58);
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            scrollingRoom40b.GetMetatile(scrollingUpDoor) != 0x78 ||
            !scrollingRoom40b.IsSolid(scrollingUpDoor))
        {
            throw new InvalidOperationException(
                "Room 4:0b scrolling preload did not open only Link's left-entry shutter.");
        }
        Step();
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:0b's incoming left shutter advanced while destination entities were frozen.");
        }
        _transitions.UpdateScroll(1.0);
        if (scrollingRoom40b.GetPackedPosition(_player.Position) != 0x50)
        {
            throw new InvalidOperationException(
                $"Room 4:0b's left scroll ended at packed position " +
                $"${scrollingRoom40b.GetPackedPosition(_player.Position):x2} instead of $50.");
        }

        Step();
        Step();
        _player.WarpTo(new Vector2(0x17, 0x58), recordSafe: false);
        Step();
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:0b closed its left shutter while Link still overlapped the strict 16-pixel boundary.");
        }
        _player.WarpTo(new Vector2(0x18, 0x58), recordSafe: false);
        Step();
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0 ||
            _player.LocalRespawnPosition != new Vector2(0x18, 0x58))
        {
            throw new InvalidOperationException(
                "Room 4:0b did not move Link's local respawn inward while deferring the close animation.");
        }
        Step();
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0xa0 ||
            scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not begin its non-solid interleaved close after Link cleared the left doorway.");
        }
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        if (scrollingRoom40b.IsSolid(scrollingLeftDoor))
        {
            throw new InvalidOperationException(
                "Room 4:0b made the incoming shutter solid before six close updates elapsed.");
        }
        Step();
        if (scrollingRoom40b.GetMetatile(scrollingLeftDoor) != 0x7b ||
            !scrollingRoom40b.IsSolid(scrollingLeftDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not finalize closed left tile $7b and collision on update 6.");
        }

        // Room 4:0b proves the same controller handles multiple orientations
        // and ordinary live combat enemies rather than depending on room 4:0c.
        IReadOnlyList<EnemyDatabase.RoomObjectRecord> room40bObjects =
            new EnemyDatabase().GetRoomObjects(4, 0x0b);
        if (room40bObjects is not
            [{
                Kind: EnemyDatabase.RoomObjectKind.RandomEnemy,
                Id: 0x43,
                SubId: 0x00,
                Flags: 0x60,
                Count: 3,
                ConditionMask: 0xff
            }])
        {
            throw new InvalidOperationException(
                "Room 4:0b's Gel stream or always-active predicate diverged from obj_RandomEnemy $60 $43 $00.");
        }
        LoadValidationRoom(4, 0x0b);
        room = _currentRoom;
        _sound.ClearPlayRequestAudit();
        if (_entities.Entities<DungeonDoorRoomEntity>().Select(value => value.SubId)
                .ToArray() is not [0x08, 0x0b] ||
            _entities.Entities<GelCharacter>().Count != 3)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not reuse the up/left enemy shutters with its three Gels.");
        }
        Step();
        if (room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b)
        {
            throw new InvalidOperationException(
                "Room 4:0b shutters opened while live Gel enemies remained.");
        }
        GelCharacter[] room40bGels = _entities.Entities<GelCharacter>().ToArray();
        for (int index = 0; index < room40bGels.Length; index++)
        {
            GelCharacter gel = room40bGels[index];
            if (!_entities.ApplySwordHit(gel.CollisionBounds, gel.Position) ||
                _entities.Entities<GelCharacter>().Count != room40bGels.Length - index - 1)
            {
                throw new InvalidOperationException(
                    $"Room 4:0b Gel {index + 1} did not die through the shared sword/combat path.");
            }
            Step();
            if (index < room40bGels.Length - 1 &&
                (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
                 room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
                 room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b))
            {
                throw new InvalidOperationException(
                    "Room 4:0b shutters released before all three counted Gels were defeated.");
            }
        }
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 2)
            throw new InvalidOperationException(
                "Room 4:0b's two shutters did not observe the shared live enemy count.");
        for (int frame = 0; frame < database.SolveWait; frame++)
            Step();
        Step();
        if (room.GetMetatile(new Vector2(0x78, 0x08)) != 0xa0 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0xa0 ||
            !room.IsSolid(new Vector2(0x78, 0x08)) ||
            !room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not begin both directional interleaved door frames together.");
        }
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        if (room.IsSolid(new Vector2(0x78, 0x08)) ||
            room.IsSolid(new Vector2(0x08, 0x58)) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 4)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not finish both reusable enemy shutters after six updates.");
        }

        // wEnemiesKilledList retains each source object's one-based index for
        // the last eight visited room IDs. A short re-entry therefore omits
        // all three direct Gels; the source layout is reloaded closed, then
        // both zero-count shutters open without SND_SOLVEPUZZLE.
        _entities.LoadRoom(4, _world.LoadRoom(4, 0x0c));
        _sound.ClearPlayRequestAudit();
        _entities.LoadRoom(4, room);
        if (_entities.Entities<GelCharacter>().Count != 0 ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 2 ||
            room.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            room.GetMetatile(new Vector2(0x08, 0x58)) != 0x7b)
        {
            throw new InvalidOperationException(
                "Room 4:0b short re-entry did not suppress its defeated Gel indices and restore both source shutters.");
        }
        Step();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
            throw new InvalidOperationException(
                "Room 4:0b replayed SND_SOLVEPUZZLE for a zero-count re-entry.");
        Step();
        if (!room.IsSolid(new Vector2(0x78, 0x08)) ||
            !room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not begin its immediate re-entry door animation on update 2.");
        }
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        if (room.IsSolid(new Vector2(0x78, 0x08)) ||
            room.IsSolid(new Vector2(0x08, 0x58)) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 4)
        {
            throw new InvalidOperationException(
                "Room 4:0b did not finish its no-solve-cue re-entry shutters after six updates.");
        }

        var recentDefeats = new RecentEnemyDefeats();
        recentDefeats.BeginRoom(0x0b);
        recentDefeats.MarkKilled(1);
        for (int roomId = 0x20; roomId < 0x27; roomId++)
            recentDefeats.BeginRoom(roomId);
        recentDefeats.BeginRoom(0x0b);
        if (!recentDefeats.WasKilled(1))
            throw new InvalidOperationException(
                "wEnemiesKilledList did not retain room 4:0b across seven other visited room IDs.");
        recentDefeats.BeginRoom(0x27);
        recentDefeats.BeginRoom(0x0b);
        if (recentDefeats.WasKilled(1))
            throw new InvalidOperationException(
                "wEnemiesKilledList did not evict room 4:0b at its original eight-entry ring boundary.");

        // The six room 5:93 Keese carry enemy-object flag $02. The parser
        // immediately subtracts them from wNumEnemies, so they must not hold
        // its right shutter closed even while their combat entities are live.
        LoadValidationRoom(5, 0x93);
        room = _currentRoom;
        _sound.ClearPlayRequestAudit();
        Vector2 countExemptDoor = new(0xe8, 0x88);
        if (_entities.Entities<KeeseCharacter>().Count != 6 ||
            _entities.Entities<DungeonDoorRoomEntity>() is not [{ SubId: 0x09 }] ||
            room.GetMetatile(countExemptDoor) != 0x79)
        {
            throw new InvalidOperationException(
                "Room 5:93 did not load six count-exempt Keese and right shutter $1e:$09.");
        }
        Step();
        Step();
        if (room.GetMetatile(countExemptDoor) != 0xa0 ||
            !room.IsSolid(countExemptDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "Enemy flag $02 incorrectly held room 5:93's shutter in wNumEnemies.");
        }

        // Room 4:06 combines two ordinary Stalfos with a $13:$01 push trigger.
        // Entering upward through its down shutter preloads only the crossed
        // door as floor, freezes all destination objects during the scroll,
        // and closes after Link reaches the strict 16-pixel clear boundary.
        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x09);
        _player.WarpTo(new Vector2(0x78, 0x08));
        _sound.ClearPlayRequestAudit();
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;
        _transitions.BeginScroll(_player, Vector2I.Up, 0x06);
        OracleRoomData scrollingRoom406 = _world.LoadRoom(4, 0x06);
        Vector2 room406DownDoor = new(0x78, 0xa8);
        Vector2 room406RightDoor = new(0xe8, 0x88);
        if (scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0x79 ||
            !scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>() is not
            [
                {
                    SubId: 0x0a,
                    EnteredThroughThisDoor: true,
                    EnemyCompletionSupported: true
                },
                {
                    SubId: 0x09,
                    EnteredThroughThisDoor: false,
                    EnemyCompletionSupported: true
                }
            ] ||
            _entities.Entities<PushBlockTriggerRoomEntity>() is not
                [{ PackedPosition: 0x7a }] ||
            _entities.Entities<StalfosCharacter>().Count != 2)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not preload only its crossed down shutter with two Stalfos and its push trigger active.");
        }
        int frozenStalfosRandomCalls = _entities.RandomCalls;
        Step();
        if (scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0 ||
            _entities.RandomCalls != frozenStalfosRandomCalls ||
            _entities.Entities<StalfosCharacter>().Any(enemy =>
                enemy.State != StalfosCharacter.StalfosState.Uninitialized))
        {
            throw new InvalidOperationException(
                "Room 4:06 advanced its incoming door or Stalfos during destination preload.");
        }
        _transitions.UpdateScroll(1.0);
        if (scrollingRoom406.GetPackedPosition(_player.Position) != 0xa7)
        {
            throw new InvalidOperationException(
                $"Room 4:06's upward scroll ended at packed position " +
                $"${scrollingRoom406.GetPackedPosition(_player.Position):x2} instead of $a7.");
        }
        Step();
        Step();
        _player.WarpTo(new Vector2(0x78, 0x99), recordSafe: false);
        Step();
        if (scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 closed its down shutter before Link stepped fully inside.");
        }
        _player.WarpTo(new Vector2(0x78, 0x98), recordSafe: false);
        Step();
        if (scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0 ||
            _player.LocalRespawnPosition != new Vector2(0x78, 0x98))
        {
            throw new InvalidOperationException(
                "Room 4:06 did not accept Link at the strict 16-pixel clear boundary.");
        }
        Step();
        if (scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not begin its non-solid interleaved down-door close after Link cleared it.");
        }
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        if (scrollingRoom406.IsSolid(room406DownDoor))
            throw new InvalidOperationException(
                "Room 4:06 made its incoming down shutter solid before six close updates elapsed.");
        Step();
        if (!scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not finish the delayed down-shutter close after six updates.");
        }

        // Both Stalfos die to one level-1 sword hit. Their deaths leave the
        // synthetic push-trigger enemy alive, so neither shutter can open
        // until the all-direction source block is moved and its 30-update delay ends.
        _sound.ClearPlayRequestAudit();
        StalfosCharacter[] room406Stalfos =
            _entities.Entities<StalfosCharacter>().ToArray();
        for (int index = 0; index < room406Stalfos.Length; index++)
        {
            StalfosCharacter enemy = room406Stalfos[index];
            if (!_entities.ApplySwordHit(
                    enemy.CollisionBounds.Grow(1.0f),
                    enemy.Position + Vector2.Down * 16.0f) ||
                _entities.Entities<StalfosCharacter>().Count !=
                    room406Stalfos.Length - index - 1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != index + 1)
            {
                throw new InvalidOperationException(
                    $"Room 4:06 Stalfos {index + 1} did not die through the shared sword/death-puff path.");
            }
            Step();
        }
        Vector2 room406Block = new(0xa8, 0x78);
        if (_entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            scrollingRoom406.GetMetatile(room406Block) != 0x1c ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            !scrollingRoom406.IsSolid(room406RightDoor))
        {
            throw new InvalidOperationException(
                "Room 4:06 opened before its remaining push-block trigger was completed: " +
                $"triggers={_entities.Entities<PushBlockTriggerRoomEntity>().Count}, " +
                $"Stalfos={_entities.Entities<StalfosCharacter>().Count}, " +
                $"block=${scrollingRoom406.GetMetatile(room406Block):x2}, " +
                $"solve={_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)}, " +
                $"downSolid={scrollingRoom406.IsSolid(room406DownDoor)}, " +
                $"rightSolid={scrollingRoom406.IsSolid(room406RightDoor)}.");
        }

        var pushableTiles = new PushableTileDatabase();
        if (!pushableTiles.TryGet(
                scrollingRoom406.ActiveCollisions,
                scrollingRoom406.GetMetatile(room406Block),
                out PushableTileDatabase.PushableTileRecord room406BlockRecord) ||
            !room406BlockRecord.AllowsEveryDirection)
        {
            throw new InvalidOperationException(
                "Room 4:06's restored source block was not the original all-direction pushable tile `$1c: " +
                $"mode={scrollingRoom406.ActiveCollisions}, " +
                $"tile=${scrollingRoom406.GetMetatile(room406Block):x2}, " +
                $"parameter=${room406BlockRecord.InteractionParameter:x2}, " +
                $"all={room406BlockRecord.AllowsEveryDirection}, " +
                $"direction={room406BlockRecord.RequiredDirection}.");
        }
        Vector2 linkBelowBlock = room406Block + Vector2.Down * 10.0f;
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _pushBlocks.UpdatePushAttempt(linkBelowBlock, Vector2I.Up, Vector2.Up);
        if (!_pushBlocks.Active ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:06's upward test push did not start the shared block movement.");
        }

        Step();
        for (int frame = 0; frame < database.PushDelay - 1; frame++)
            Step();
        if (_entities.Entities<PushBlockTriggerRoomEntity>().Count != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 released its synthetic enemy before the 30-update trigger delay.");
        }
        Step();
        if (_entities.Entities<PushBlockTriggerRoomEntity>().Count != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06's source-ordered doors observed the push trigger before it finished update 30.");
        }
        Step();
        if (_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:06's two shutters did not observe the completed Stalfos/block enemy count.");
        }
        for (int frame = 0; frame < database.SolveWait; frame++)
            Step();
        Step();
        if (scrollingRoom406.GetMetatile(room406DownDoor) != 0xa0 ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0xa0 ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            !scrollingRoom406.IsSolid(room406RightDoor))
        {
            throw new InvalidOperationException(
                "Room 4:06 did not begin both interleaved openings after its eight-update solve wait.");
        }
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        if (scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not finish opening both solved shutters after six updates.");
        }

        // Exercise the route from 4:07 independently: a leftward scroll must
        // substitute and then close 4:06's right shutter, never the down one.
        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x07);
        _player.WarpTo(new Vector2(0x08, 0x88));
        _sound.ClearPlayRequestAudit();
        _transitions.BeginScroll(_player, Vector2I.Left, 0x06);
        scrollingRoom406 = _world.LoadRoom(4, 0x06);
        if (scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0xa0 ||
            scrollingRoom406.IsSolid(room406RightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>() is not
            [
                {
                    SubId: 0x0a,
                    EnteredThroughThisDoor: false,
                    EnemyCompletionSupported: true
                },
                {
                    SubId: 0x09,
                    EnteredThroughThisDoor: true,
                    EnemyCompletionSupported: true
                }
            ] ||
            _entities.Entities<StalfosCharacter>().Count != 2 ||
            _entities.Entities<PushBlockTriggerRoomEntity>().Count != 1)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not preload only its crossed right shutter from room 4:07.");
        }
        Step();
        _transitions.UpdateScroll(1.0);
        if (scrollingRoom406.GetPackedPosition(_player.Position) != 0x8e)
        {
            throw new InvalidOperationException(
                $"Room 4:06's leftward scroll ended at packed position " +
                $"${scrollingRoom406.GetPackedPosition(_player.Position):x2} instead of $8e.");
        }
        Step();
        Step();
        _player.WarpTo(new Vector2(0xd9, 0x88), recordSafe: false);
        Step();
        if (scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 closed its right shutter while Link still overlapped it.");
        }
        _player.WarpTo(new Vector2(0xd8, 0x88), recordSafe: false);
        Step();
        if (scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not defer its right-entry close by one update at the strict boundary.");
        }
        Step();
        if (scrollingRoom406.IsSolid(room406RightDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not begin its non-solid interleaved right-door close.");
        }
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        if (scrollingRoom406.IsSolid(room406RightDoor))
            throw new InvalidOperationException(
                "Room 4:06 made its incoming right shutter solid before six close updates elapsed.");
        Step();
        if (!scrollingRoom406.IsSolid(room406RightDoor) ||
            scrollingRoom406.GetMetatile(room406RightDoor) != 0x79 ||
            scrollingRoom406.GetMetatile(room406DownDoor) != 0x7a ||
            !scrollingRoom406.IsSolid(room406DownDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:06 did not finish the delayed right-shutter close from room 4:07.");
        }
        _entities.ClearRecentEnemyDefeats();
        _entities.WorldToScreen = static position => position;

        LoadValidationRoom(4, 0x13);
        if (_entities.Entities<DungeonDoorRoomEntity>() is not
            [
                { EnemyCompletionSupported: false },
                { EnemyCompletionSupported: false }
            ] ||
            _currentRoom.GetMetatile(new Vector2(0x78, 0x08)) != 0x78 ||
            _currentRoom.GetMetatile(new Vector2(0x08, 0x78)) != 0x7b)
        {
            throw new InvalidOperationException(
                "Room 4:13 did not retain inert closed shutters for its unresolved before-event enemy.");
        }
        // Room 4:09 is the canonical one-shot button: PART_BUTTON $09:$00 at
        // $14 sets trigger bit 0 after its state-0 initialization update. Both
        // $1e:$04/$05 doors observe the bit on the following update, request
        // the solve cue independently, and begin their six-update opening one
        // update later. The released Link does not clear a one-shot trigger.
        LoadValidationRoom(4, 0x09);
        room = _currentRoom;
        Vector2 oneShotButton = new(0x48, 0x18);
        Vector2 triggerUpDoor = new(0x78, 0x08);
        Vector2 triggerRightDoor = new(0xe8, 0x58);
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        if (_entities.Entities<DungeonDoorRoomEntity>().Select(value => value.SubId)
                .ToArray() is not [0x04, 0x05] ||
            _entities.Entities<PushBlockTriggerRoomEntity>() is not
                [{ PackedPosition: 0x2a }] ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ SubId: 0x00, PackedPosition: 0x14, TriggerBit: 0, Reusable: false }] ||
            room.GetMetatile(oneShotButton) != 0x0c ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79)
        {
            throw new InvalidOperationException(
                "Room 4:09 did not instantiate its ordered bit-0 button, push trigger, and up/right shutters.");
        }
        Step();
        _player.WarpTo(oneShotButton);
        Step();
        if (_entities.ActiveTriggers != 0x01 ||
            _entities.Entities<GroundButtonRoomEntity>().Count != 0 ||
            room.GetMetatile(oneShotButton) != 0x0d ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0 ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79)
        {
            throw new InvalidOperationException(
                "Room 4:09's one-shot button did not latch tile $0d/trigger bit 0 before its doors updated.");
        }
        _player.WarpTo(new Vector2(0x78, 0x78));
        Step();
        if (_entities.ActiveTriggers != 0x01 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 2 ||
            room.GetMetatile(triggerUpDoor) != 0x78 ||
            room.GetMetatile(triggerRightDoor) != 0x79)
        {
            throw new InvalidOperationException(
                "Room 4:09's two trigger doors did not request their solve cues one update after button pressure.");
        }
        Step();
        if (room.GetMetatile(triggerUpDoor) != 0xa0 ||
            room.GetMetatile(triggerRightDoor) != 0xa0 ||
            !room.IsSolid(triggerUpDoor) || !room.IsSolid(triggerRightDoor) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:09 did not begin both interleaved openings while retaining collision.");
        }
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        if (room.IsSolid(triggerUpDoor) || room.IsSolid(triggerRightDoor) ||
            _entities.Entities<DungeonDoorRoomEntity>().Count != 2 ||
            _entities.ActiveTriggers != 0x01 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:09 did not retain its latched trigger and reusable door controllers after opening.");
        }

        // Room 4:22 uses reusable button $80. State 0 returns even when Link
        // already overlaps it; airborne Link remains ignored. Ground contact
        // presses on the next update, strict combined radius 8 releases at
        // exactly +8 pixels, and the right shutter closes again.
        LoadValidationRoom(4, 0x22);
        room = _currentRoom;
        Vector2 reusableButton = new(0xb8, 0x58);
        Vector2 reusableDoor = new(0xe8, 0x58);
        _sound.ClearPlayRequestAudit();
        _player.WarpTo(reusableButton);
        _player.BeginNewGameSlowFall(1);
        Step();
        Step();
        if (_entities.ActiveTriggers != 0 ||
            room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 0)
        {
            throw new InvalidOperationException(
                "Room 4:22's reusable button accepted state-0 or airborne Link pressure.");
        }
        _player.EndNewGameSlowFall();
        Step();
        if (_entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(reusableButton) != 0x0d ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ SubId: 0x80, TriggerBit: 0, Reusable: true, Pressed: true }] ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:22's grounded Link did not press reusable bit-0 button $80.");
        }
        Step();
        Step();
        for (int frame = 0; frame < database.DoorFrameWait; frame++)
            Step();
        if (room.IsSolid(reusableDoor))
            throw new InvalidOperationException("Room 4:22's reusable button did not open its right shutter.");
        _player.WarpTo(reusableButton + Vector2.Right * 7.0f);
        Step();
        if (_entities.ActiveTriggers != 0x01 || room.GetMetatile(reusableButton) != 0x0d)
            throw new InvalidOperationException(
                "Room 4:22 released its button inside the strict eight-pixel contact radius.");
        _player.WarpTo(reusableButton + Vector2.Right * 8.0f);
        Step();
        if (_entities.ActiveTriggers != 0 || room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:22 did not release at the strict eight-pixel boundary with SND_SPLASH.");
        }
        Step();
        if (room.IsSolid(reusableDoor))
            throw new InvalidOperationException(
                "Room 4:22 closed its shutter in the update that observed trigger release.");
        Step();
        int healthBeforeDoorRespawn = _player.HealthQuarters;
        Vector2 expectedDoorRespawn = _player.LocalRespawnPosition;
        _player.WarpTo(reusableDoor, recordSafe: false);
        for (int frame = 0; frame < database.DoorFrameWait - 1; frame++)
            Step();
        if (room.IsSolid(reusableDoor))
            throw new InvalidOperationException(
                "Room 4:22 applied closed collision before six interleaved updates.");
        Step();
        if (room.GetMetatile(reusableDoor) != 0x79 || !room.IsSolid(reusableDoor) ||
            !_player.IsFloorDoorRespawning || _player.FloorDoorRespawnCounter != 2 ||
            _player.Visible || _player.Position != expectedDoorRespawn ||
            _player.HealthQuarters != healthBeforeDoorRespawn)
        {
            throw new InvalidOperationException(
                "Room 4:22 did not finish tile $79 and begin the parameter-2 local respawn when it closed on Link.");
        }
        _player._PhysicsProcess(update);
        _player._PhysicsProcess(update);
        if (!_player.Visible || _player.HealthQuarters != healthBeforeDoorRespawn - 4)
        {
            throw new InvalidOperationException(
                "Floor-door respawn did not reappear after two updates with the original one-heart damage.");
        }
        _player.Heal(4);
        _player.WarpTo(new Vector2(0x78, 0x78));

        // An object above reusable button $80 starts the original $1c release
        // counter. The pressed tile is revealed when that object leaves and
        // remains active through 27 clear updates, releasing on update 28.
        LoadValidationRoom(4, 0x22);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x78, 0x78));
        Step();
        _sound.ClearPlayRequestAudit();
        Vector2 pressureBlock = reusableButton + Vector2.Left * 16.0f;
        room.SetPositionTileAndCollision(
            pressureBlock, 0x1c, null, (long)_animationTicks);
        Vector2 pushFromLeft = pressureBlock + Vector2.Left * 10.0f;
        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _pushBlocks.UpdatePushAttempt(pushFromLeft, Vector2I.Right, Vector2.Right);
        for (int frame = 0; frame < PushBlockController.MoveFrames; frame++)
            _pushBlocks.Advance(update);
        if (_pushBlocks.Active || room.GetMetatile(reusableButton) != 0x1d)
        {
            throw new InvalidOperationException(
                "The shared push-block controller did not place tile $1d over room 4:22's button.");
        }
        Step();
        if (_entities.ActiveTriggers != 0x01 ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ Pressed: true, ReleaseCounter: 0x1c }] ||
            room.GetMetatile(reusableButton) != 0x1d ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1)
        {
            throw new InvalidOperationException(
                "Room 4:22 did not preserve an object above its newly pressed reusable button.");
        }
        // Destination tile $1d is intentionally no longer pushable. Restore
        // the underlying tile here to model a removable pot/Somaria block
        // leaving while preserving the real push-controller pressure path.
        room.SetPositionTileAndCollision(
            reusableButton, 0x0c, null, (long)_animationTicks);
        for (int frame = 0; frame < database.ButtonObjectReleaseDelay - 1; frame++)
            Step();
        if (_entities.ActiveTriggers != 0x01 ||
            room.GetMetatile(reusableButton) != 0x0d ||
            _entities.Entities<GroundButtonRoomEntity>() is not
                [{ Pressed: true, ReleaseCounter: 1 }] ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 1)
        {
            throw new InvalidOperationException(
                "Reusable object pressure did not retain tile $0d through 27 release updates.");
        }
        Step();
        if (_entities.ActiveTriggers != 0 || room.GetMetatile(reusableButton) != 0x0c ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSplash) != 2)
        {
            throw new InvalidOperationException(
                "Reusable object pressure did not release on exact update $1c.");
        }

        // Bits 0-2, not bit 7, choose wActiveTriggers. Room 4:16's second
        // one-shot button therefore sets only bit 1.
        LoadValidationRoom(4, 0x16);
        room = _currentRoom;
        _player.WarpTo(new Vector2(0x78, 0x78));
        _sound.ClearPlayRequestAudit();
        Step();
        GroundButtonRoomEntity bit1Button = _entities.Entities<GroundButtonRoomEntity>()
            .Single(value => value.SubId == 0x01);
        GroundButtonRoomEntity bit0Button = _entities.Entities<GroundButtonRoomEntity>()
            .Single(value => value.SubId == 0x00);
        TriggerChestRoomEntity bit0Chest = _entities.Entities<TriggerChestRoomEntity>().Single();
        byte bit0ChestOriginal = room.GetOriginalMetatile(bit0Chest.Position);
        _player.WarpTo(bit1Button.Position);
        Step();
        if (_entities.ActiveTriggers != 0x02 ||
            room.GetMetatile(bit1Button.Position) != 0x0d ||
            room.GetMetatile(bit0Chest.Position) != bit0ChestOriginal ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 0)
        {
            throw new InvalidOperationException(
                "PART_BUTTON $09:$01 did not select only wActiveTriggers bit 1 or incorrectly activated a bit-0 chest.");
        }
        _player.WarpTo(bit0Button.Position);
        Step();
        Step();
        if (_entities.ActiveTriggers != 0x03 ||
            _entities.Entities<TriggerChestRoomEntity>() is not [{ Counter: 15 }] ||
            room.GetMetatile(bit0Chest.Position) != bit0ChestOriginal ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 2)
        {
            throw new InvalidOperationException(
                "Room 4:16's bit-0 predicate did not accept trigger state $03 " +
                $"while bit 1 remained set: triggers=${_entities.ActiveTriggers:x2}, " +
                $"controllers={_entities.Entities<TriggerChestRoomEntity>().Count}, " +
                $"counter={_entities.Entities<TriggerChestRoomEntity>().FirstOrDefault()?.Counter}, " +
                $"tile=${room.GetMetatile(bit0Chest.Position):x2}, " +
                $"solve={_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)}.");
        }
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;

        GD.Print("Validated all 155 imported button/trigger-chest/$13:$01/$1e:$04-$0b " +
            "placements, 49 buttons, seven delayed and six retractable trigger chests, " +
            "20 trigger-door records, room 4:08's exact-$01 solve/puff/15-update chest, " +
            "room 4:7a's reusable chest retraction, room 4:09's one-shot bit-0 " +
            "button and dual shutters, room 4:22's reusable strict-radius/airborne/" +
            "28-update object release, reopening/closing, and local door respawn, " +
            "exact/bit predicates, bit selection, room 4:0c's " +
            "ordered push-trigger enemy count and 30/8/6-update boundaries, mapping-level " +
            "half-door animation/collision, sounds, room 4:0b's delayed left-entry close, " +
            "shared up/left Gel shutters, sword path, predicates, transient eight-room " +
            "defeat/re-entry behavior, room 5:93's " +
            "enemy flag-$02 count exemption, and room 4:06's two delayed entry " +
            "closures plus its two-Stalfos/all-direction-block full solve.");
    }

    private void ValidateChests()
    {
        WarpToChestTest();
        _sound.ClearPlayRequestAudit();
        const int chestPosition = 0x51;
        Vector2 chestPoint = new(24, 88);
        if (_activeGroup != 0 || _currentRoom.Id != 0x49 ||
            _currentRoom.GetPackedPosition(chestPoint) != chestPosition ||
            _currentRoom.GetMetatile(chestPoint) != 0xf1)
        {
            throw new InvalidOperationException(
                "The canonical 0:49/$51 30-rupee chest was not available for testing.");
        }

        Image roomImage = _currentRoom.Texture.GetImage();
        int redChestPixels = 0;
        for (int y = 80; y < 96; y++)
        for (int x = 16; x < 32; x++)
        {
            Color pixel = roomImage.GetPixel(x, y);
            if (pixel.R > 0.5f && pixel.G < 0.2f && pixel.B < 0.25f)
                redChestPixels++;
        }
        if (redChestPixels == 0)
        {
            throw new InvalidOperationException(
                "Chest $51 did not render with PALH_0f background palette 0.");
        }

        _player.WarpTo(new Vector2(24, 74));
        _player.Face(Vector2I.Down);
        if (!TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "It won't open\nfrom this side!" ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 0)
        {
            throw new InvalidOperationException("Chest $51 did not use TX_510d from the wrong side.");
        }
        _dialogue.Close();

        _player.WarpTo(new Vector2(24, 100));
        _player.Face(Vector2I.Up);
        int rupeesBefore = _player.Rupees;
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _interactions.ChestReward is not { VisualGraphic: 0x2b } ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 0)
        {
            throw new InvalidOperationException("Chest $51 did not open from below into tile $f0.");
        }

        _interactions.Update(31.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 0)
            throw new InvalidOperationException("The chest reward completed before its 32-frame rise.");
        _interactions.Update(1.0 / 60.0, _player);
        if (!_interactions.ChestRewardActive || _player.Rupees != rupeesBefore + 30 ||
            !_dialogue.IsOpen || _dialogue.CurrentMessage != "You got\n30 Rupees!\nThat's nice." ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_RUPEES_04 did not remain visible while showing TX_0005.");
        }

        _dialogue.Close();
        if (!_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward disappeared before its textbox closed.");
        _interactions.Update(0.0, _player);
        if (_interactions.ChestRewardActive)
            throw new InvalidOperationException("The chest reward remained after its textbox closed.");

        _currentRoom = _world.LoadRoom(0, 0x49);
        _roomView.SetRoom(_currentRoom.Texture);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf0 || TryInteract(_player))
            throw new InvalidOperationException("The opened chest room flag did not persist for the session.");

        GD.Print("Validated 0:49/$51 red chest palette, direction, 32-frame reward rise, " +
            "source graphic $2b, SND_OPENCHEST/SND_GETITEM, reward visibility through " +
            "TX_0005, 30 rupees, and opened state.");
    }

    private void ValidateBraceletChestAndPushGate()
    {
        var chests = new ChestDatabase();
        if (!chests.TryGet(5, 0xa6, 0x37, out ChestDatabase.ChestRecord braceletChest) ||
            braceletChest.TreasureObject != "TREASURE_OBJECT_BRACELET_02" ||
            braceletChest.TreasureId != TreasureDatabase.TreasureBracelet ||
            braceletChest.Parameter != 2)
        {
            throw new InvalidOperationException(
                "The original 5:a6/$37 chest did not resolve to TREASURE_OBJECT_BRACELET_02.");
        }

        var pushables = new PushableTileDatabase();
        if (!pushables.TryGet(2, 0x10, out PushableTileDatabase.PushableTileRecord braceletBlock) ||
            !braceletBlock.RequiresBracelet ||
            !braceletBlock.AllowsEveryDirection ||
            braceletBlock.SourceReplacement != 0xa0 ||
            braceletBlock.DestinationTile != 0x10)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not retain interactable parameter $c0 and pushblock data.");
        }

        var breakables = new BreakableTileDatabase();
        if (!breakables.TryGet(2, 0x10, out BreakableTileDatabase.BreakableTileRecord liftablePot) ||
            !liftablePot.AllowsSource(BreakableTileDatabase.SourceBracelet) ||
            liftablePot.Replacement != 0xa0)
        {
            throw new InvalidOperationException(
                "Collision mode 2 tile $10 did not import as a bracelet-breakable tile with replacement $a0.");
        }

        LoadValidationRoom(4, 0x08);
        Vector2 blockCenter = new(0x0b * 16 + 8, 0x04 * 16 + 8);
        Vector2 linkBelow = blockCenter + new Vector2(0, 10);
        if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
            !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
        {
            throw new InvalidOperationException("Could not prepare 4:08/$4b as bracelet-required tile $10.");
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 moved before TREASURE_BRACELET was obtained.");
        }

        LoadValidationRoom(4, 0xce);
        _interactions.ResetChestForTesting(4, 0xce, 0x67, "TREASURE_OBJECT_BRACELET_00");
        Vector2 debugBraceletChest = new(7 * OracleRoomData.MetatileSize + 8, 6 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(debugBraceletChest) != 0xf1)
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest was not closed.");

        _player.WarpTo(new Vector2(debugBraceletChest.X, debugBraceletChest.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(debugBraceletChest) != 0xf0)
        {
            throw new InvalidOperationException("The debug 4:ce/$67 Power Bracelet chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 1 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Bracelet!\nHold the button\nand press \\item(0x00)\nto lift heavy\nobjects!")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_00 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_0026.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        Vector2 liftPoint = new(7 * OracleRoomData.MetatileSize + 8, 2 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(liftPoint) != 0x10)
            throw new InvalidOperationException("The 4:ce bracelet-use test tile was not dungeon tile $10.");
        _player.WarpTo(new Vector2(liftPoint.X, liftPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!_playerWorld.TryUseBracelet(_player) || _currentRoom.GetMetatile(liftPoint) != 0xa0)
        {
            throw new InvalidOperationException(
                "Equipped bracelet use did not break/lift the tile in front using BREAKABLETILESOURCE_BRACELET.");
        }

        LoadValidationRoom(5, 0xa6);
        _interactions.ResetChestForTesting(5, 0xa6, 0x37);
        Vector2 chestPoint = new(7 * OracleRoomData.MetatileSize + 8, 3 * OracleRoomData.MetatileSize + 8);
        if (_currentRoom.GetMetatile(chestPoint) != 0xf1)
            throw new InvalidOperationException("The original 5:a6/$37 Power Glove chest was not closed.");

        _player.WarpTo(new Vector2(chestPoint.X, chestPoint.Y + 12));
        _player.Face(Vector2I.Up);
        if (!TryInteract(_player) || !_interactions.ChestRewardActive ||
            _currentRoom.GetMetatile(chestPoint) != 0xf0)
        {
            throw new InvalidOperationException("The 5:a6/$37 Power Glove chest did not open from below.");
        }

        _interactions.Update(32.0 / 60.0, _player);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureBracelet) ||
            _inventory.BraceletLevel != 2 ||
            _inventory.EquippedB != InventoryState.ItemBracelet ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != "You got the\nPower Glove!\nYou can now lift\nheavy objects.")
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_BRACELET_02 did not set obtained flags, wBraceletLevel, wInventoryB, and TX_002f.");
        }
        _dialogue.Close();
        _interactions.Update(0.0, _player);

        LoadValidationRoom(4, 0x08);
        if (_currentRoom.GetMetatile(blockCenter) != 0x10)
        {
            if (_currentRoom.GetMetatile(blockCenter) != 0x1c ||
                !_currentRoom.ReplaceMetatile(blockCenter, 0x1c, 0x10, (long)_animationTicks))
            {
                throw new InvalidOperationException("Could not restore bracelet push validation tile $10.");
            }
        }

        for (int frame = 0; frame < PushBlockController.PushDelayFrames; frame++)
            _playerWorld.UpdatePushableBlocks(linkBelow, Vector2I.Up, Vector2.Up);
        if (!_pushBlocks.Active || _currentRoom.GetMetatile(blockCenter) != 0xa0)
        {
            throw new InvalidOperationException(
                "Bracelet-required tile $10 did not start moving after TREASURE_BRACELET was obtained.");
        }
        _pushBlocks.Cancel();
        _currentRoom.ReplaceMetatile(blockCenter, 0xa0, 0x1c, (long)_animationTicks);

        GD.Print("Validated debug Power Bracelet chest TREASURE_OBJECT_BRACELET_00, " +
            "bracelet tile use via BREAKABLETILESOURCE_BRACELET, original Power Glove upgrade, " +
            "and bracelet-required pushblock tile $10.");
    }
}

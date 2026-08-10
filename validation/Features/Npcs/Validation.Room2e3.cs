using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom2e3Interactions()
    {
        const double frame = 1.0 / 60.0;
        var database = new Room2e3Database();
        Room2e3InteractionRecord record = database.Record;
        EnemyHandlerDescriptor cuccoHandler =
            new EnemyDatabase().EnemyHandlers.ResolveHandler(
                0x33, 0x00, "validation:room2e3");
        FailIf(
            record.Group != 2 ||
            record.Room != 0xe3 ||
            record.Order != 0 ||
            record.Id != 0x6b ||
            record.SubId != 0x0a ||
            record.Y != 0x28 ||
            record.X != 0x28 ||
            record.Var03 != 0 ||
            record.ItemRoomFlag != OracleSaveData.RoomFlagItem ||
            record.TreasureId != TreasureDatabase.TreasureBombs ||
            record.TreasureSubId != 0x04 ||
            record.TreasureParameter != 0 ||
            record.PostGrantWait != 30 ||
            record.PickupDistance != 0x0e ||
            record.AnimationIndex != 0x01 ||
            cuccoHandler.Classification !=
                EnemyHandlerClassification.OrderedImplemented ||
            cuccoHandler.Handler != EnemyHandlerKind.BabyCucco ||
            cuccoHandler.CollisionMode != 0 ||
            cuccoHandler.SupportsCombatSource,
            "Room 2:e3 lost its imported Bombs interaction or non-combat " +
            "ENEMY_BABY_CUCCO $33:$00 handler contract.");

        _dialogue.Close();
        _inventory.GiveTreasure(TreasureDatabase.TreasureBombs, 0x10);
        while (_inventory.Bombs != 0x02)
        {
            FailIf(!_inventory.TryConsumeBomb(),
                "Could not arrange a partial Bomb count for room 2:e3.");
        }
        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag),
            value: false);
        LoadValidationRoom(record.Group, record.Room);

        GroundTreasurePickup pickup =
            _entities.Entities<GroundTreasurePickup>().Single();
        BabyCuccoCharacter[] cuccos =
            _entities.Entities<BabyCuccoCharacter>().ToArray();
        FailIf(
            pickup.Name.ToString() != "Room2e3Bombs" ||
            pickup.Position != new Vector2(0x28, 0x28) ||
            pickup.Record.TreasureObject != "TREASURE_OBJECT_BOMBS_04" ||
            pickup.Record.Sprite != "spr_common_items" ||
            pickup.Record.TileBase != 0x10 ||
            pickup.Record.Palette != 0x04 ||
            pickup.Record.Animation != record.Animation ||
            pickup.PixelHash == 0 ||
            cuccos.Length != 3 ||
            _entities.RoomEnemyCount != 3 ||
            !cuccos.Select(cucco => cucco.Position).SequenceEqual(
                [
                    new Vector2(0x28, 0x48),
                    new Vector2(0x38, 0x48),
                    new Vector2(0x58, 0x48)
                ]) ||
            cuccos.Any(cucco =>
                cucco.Record is not
                    {
                        Id: 0x33,
                        SubId: 0x00,
                        RadiusY: 6,
                        RadiusX: 6,
                        Health: 127,
                        DamageQuarters: 128,
                        Animations.Length: 2
                    }),
            "Room 2:e3 did not create its source-positioned Bombs pickup " +
            "followed by the three ordered Baby Cuccos.");

        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag));
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.Entities<BabyCuccoCharacter>().Count != 3,
            "ROOMFLAG_ITEM did not suppress only room 2:e3's Bombs pickup.");

        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag),
            value: false);
        LoadValidationRoom(record.Group, record.Room);
        pickup = _entities.Entities<GroundTreasurePickup>().Single();
        _player.WarpTo(new Vector2(0x36, 0x28), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Waiting ||
            _inventory.Bombs != 0x02,
            "Room 2:e3's Bombs collected at the excluded Manhattan-$0e boundary.");
        _player.WarpTo(new Vector2(0x35, 0x28), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Collected ||
            _inventory.Bombs != _inventory.MaxBombs ||
            !_saveData.HasRoomFlag(
                record.Group,
                record.Room,
                checked((byte)record.ItemRoomFlag)) ||
            !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.Contains(
                "Bombs back", StringComparison.Ordinal) ||
            !_entities.PlayerMovementDisabled ||
            !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerMenusDisabled,
            "Room 2:e3 did not refill wNumBombs before the $03:$04 grant, " +
            "set ROOMFLAG_ITEM, show TX_0076, and retain the input lease.");

        _dialogue.Close();
        _interactions.Update(frame, _player);
        _entities.Update(frame, _player);
        for (int update = 1;
            update < record.PostGrantWait;
            update++)
        {
            _entities.Update(frame, _player);
        }
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.PlayerMovementDisabled ||
            _entities.PlayerItemUsageDisabled ||
            _entities.PlayerMenusDisabled,
            "Room 2:e3's $6b:$0a did not release input and delete on its " +
            "30-update post-grant boundary.");

        // Keep the pickup suppressed while exercising the ordered enemy
        // stream. The first update is state 0 and deliberately consumes no
        // RNG; each near state-8 Cucco then consumes one shared value in
        // source order.
        LoadValidationRoom(record.Group, record.Room);
        cuccos = _entities.Entities<BabyCuccoCharacter>().ToArray();
        _player.WarpTo(new Vector2(0x90, 0x20), recordSafe: false);
        int callsBeforeInitialization = _random.Calls;
        _entities.Update(frame, _player);
        FailIf(
            _random.Calls != callsBeforeInitialization ||
            cuccos.Any(cucco =>
                cucco.State != BabyCuccoState.Following ||
                !cucco.Visible ||
                cucco.CurrentAnimationPixelHash == 0),
            "Baby Cucco state 0 changed RNG or failed to enter visible state 8.");

        _player.WarpTo(new Vector2(0x50, 0x48), recordSafe: false);
        foreach (BabyCuccoCharacter cucco in cuccos)
            cucco.Position = _player.Position;
        OracleRandomState randomState = _random.CaptureState();
        _random.RestoreState(randomState with { Rng1 = 0x40, Rng2 = 0x00 });
        int callsBeforeHops = _random.Calls;
        _entities.Update(frame, _player);
        FailIf(
            _random.Calls != callsBeforeHops + 3 ||
            cuccos.Any(cucco =>
                cucco.State != BabyCuccoState.Hopping ||
                cucco.ZFixed != 0 ||
                cucco.SpeedZ != -0xc0),
            "The three near Baby Cuccos did not consume one shared RNG value " +
            "each and enter the 1-in-64 hop in source order.");

        for (int update = 0; update < 22; update++)
            _entities.Update(frame, _player);
        FailIf(cuccos.Any(cucco =>
                cucco.State != BabyCuccoState.Hopping ||
                cucco.ZFixed != -66 ||
                cucco.SpeedZ != 204),
            "Baby Cucco hopping lost the $ff40 speedZ / $12 gravity boundary.");
        _entities.Update(frame, _player);
        FailIf(cuccos.Any(cucco =>
                cucco.State != BabyCuccoState.Following ||
                cucco.ZFixed != 0),
            "Baby Cucco hopping did not land on its 23rd airborne update.");

        BabyCuccoCharacter carried = cuccos[0];
        carried.Position = new Vector2(0x50, 0x48);
        cuccos[1].Position = new Vector2(0x20, 0x20);
        cuccos[2].Position = new Vector2(0x30, 0x20);
        _player.WarpTo(new Vector2(0x4a, 0x48), recordSafe: false);
        _player.Face(Vector2I.Right);
        FailIf(!_entities.TryUseBracelet(_player, Vector2I.Zero) ||
            carried.State != BabyCuccoState.Held ||
            carried.CurrentAnimationFrame != 0 ||
            !_player.IsCarryingObject,
            "The first source-order Baby Cucco was not grabbable from state 8.");
        for (int update = 0; update < 3; update++)
            _entities.Update(frame, _player);
        FailIf(
            carried.CurrentAnimationFrame != 0,
            "Held Baby Cucco advanced before its source four-update " +
            "animation boundary.");
        _entities.Update(frame, _player);
        FailIf(
            carried.CurrentAnimationFrame != 1,
            "Held Baby Cucco did not run enemyAnimate on its source " +
            "four-update frame boundary.");
        FailIf(!_entities.TryUseBracelet(_player, Vector2I.Right) ||
            carried.State != BabyCuccoState.Thrown ||
            carried.ThrowDirection != Vector2I.Right ||
            carried.SpeedZ != -0xf0 ||
            carried.ThrowSpeedRaw != 0x3c ||
            _player.IsCarryingObject,
            "Baby Cucco release did not start the shared weight-0 throw path.");

        int landingSounds =
            _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand);
        for (int update = 0;
            update < 180 && carried.State == BabyCuccoState.Thrown;
            update++)
        {
            _entities.Update(frame, _player);
        }
        FailIf(
            carried.State != BabyCuccoState.Following ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndBombLand) <=
                landingSounds ||
            _entities.RoomEnemyCount != 3,
            "Thrown Baby Cucco did not bounce with SND_BOMB_LAND and return " +
            "to counted, grabbable state 8.");

        GD.Print(
            "Validated room 2:e3 $6b:$0a and $33:$00: imported Bombs " +
            "placement/visual/refill/item flag/input lease plus three ordered " +
            "Baby Cuccos with shared-RNG chase/hop, held animation, and " +
            "bracelet throw/bounce.");
    }
}

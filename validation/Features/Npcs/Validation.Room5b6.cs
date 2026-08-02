using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5b6Interactions()
    {
        const double frame = 1.0 / 60.0;
        var database = new Room5b6Database();
        Room5b6InteractionRecord record = database.Record;
        FailIf(
            record.Group != 5 ||
            record.Room != 0xb6 ||
            record.Order != 0 ||
            record.Id != 0x6b ||
            record.SubId != 0x0b ||
            record.Var03 != 0x01 ||
            record.ItemRoomFlag != OracleSaveData.RoomFlagItem ||
            record.TreasureId != 0x52 ||
            record.TreasureSubId != 0 ||
            record.TreasureParameter != 0 ||
            record.PostGrantWait != 30 ||
            record.CollisionRadiusY != 0x02 ||
            record.CollisionRadiusX != 0x02 ||
            record.PickupDistance != 0x0e ||
            record.RememberedIdValue != 0 ||
            record.AnimationIndex != 0x02,
            "Room 5:b6 lost its imported $6b:$0b Cheval Rope placement, " +
            "var03 branch, item flag, collision radius, or script constants.");

        _dialogue.Close();
        // Give/Lose preserves the behaviour variable, letting the test prove
        // TREASURE_CHEVAL_ROPE parameter $00 clears the death-respawn copy.
        _inventory.GiveTreasure(
            record.TreasureId, CompanionRuntimeState.MooshId);
        FailIf(!_inventory.LoseTreasure(record.TreasureId),
            "Could not arrange an unowned Cheval Rope for room 5:b6.");
        CompanionRuntimeState.Remember(
            _entities.RuntimeState,
            CompanionRuntimeState.MooshId,
            group: 0,
            room: 0x80,
            new Vector2(0x58, 0x68));
        RememberedCompanion remembered =
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag),
            value: false);
        LoadValidationRoom(record.Group, record.Room);

        GroundTreasurePickup pickup =
            _entities.Entities<GroundTreasurePickup>().Single();
        FailIf(
            pickup.Name.ToString() != "Room5b6ChevalRope" ||
            pickup.Position != new Vector2(0x28, 0x48) ||
            pickup.Record.TreasureObject != record.TreasureObject ||
            pickup.Record.Sprite != "spr_quest_items_2" ||
            pickup.Record.TileBase != 0x10 ||
            pickup.Record.Palette != 0x03 ||
            pickup.Record.Animation != record.Animation ||
            pickup.Record.Source != "mainData.s:group5Mapb6ObjectData" ||
            pickup.PixelHash == 0,
            "Room 5:b6 did not create the source-positioned and source-drawn " +
            "$6b:$0b Cheval Rope interaction.");

        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag));
        LoadValidationRoom(record.Group, record.Room);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0,
            "ROOMFLAG_ITEM did not suppress room 5:b6's $6b:$0b interaction " +
            "on re-entry.");

        _saveData.SetRoomFlag(
            record.Group,
            record.Room,
            checked((byte)record.ItemRoomFlag),
            value: false);
        LoadValidationRoom(record.Group, record.Room);
        pickup = _entities.Entities<GroundTreasurePickup>().Single();
        _player.WarpTo(new Vector2(0x36, 0x48), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Waiting ||
            _inventory.HasTreasure(record.TreasureId),
            "Room 5:b6's $6b:$0b collected at objectCheckLinkWithinDistance's " +
            "excluded Manhattan-distance $0e boundary.");
        _player.WarpTo(new Vector2(0x2f, 0x4f), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Waiting ||
            _inventory.HasTreasure(record.TreasureId),
            "Room 5:b6's $6b:$0b lost objectCheckLinkWithinDistance's " +
            "Manhattan geometry at the $07+$07 diagonal boundary.");

        _player.WarpTo(new Vector2(0x35, 0x48), recordSafe: false);
        _entities.Update(frame, _player);
        RememberedCompanion duringHeldItem =
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        FailIf(
            pickup.State != PickupState.Collected ||
            !pickup.Held ||
            !_inventory.HasTreasure(record.TreasureId) ||
            !_saveData.HasRoomFlag(
                record.Group,
                record.Room,
                checked((byte)record.ItemRoomFlag)) ||
            _inventory.RememberedCompanionId != 0 ||
            duringHeldItem.Id != CompanionRuntimeState.MooshId ||
            !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.Contains(
                "Cheval", StringComparison.Ordinal) ||
            !_dialogue.CurrentMessage.Contains(
                "Rope", StringComparison.Ordinal) ||
            !_entities.PlayerMovementDisabled ||
            !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerMenusDisabled ||
            !_entities.ScreenTransitionsDisabled,
            "Touching room 5:b6's $6b:$0b did not grant and display " +
            "TREASURE_CHEVAL_ROPE $52:$00, set ROOMFLAG_ITEM, clear the " +
            "death-respawn companion ID, and retain the source input lease.");

        _dialogue.Close();
        _interactions.Update(frame, _player);
        FailIf(
            !pickup.Finished ||
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState).Id !=
                CompanionRuntimeState.MooshId,
            "The room 5:b6 Rope did not finish its held-item command before " +
            "the following wRememberedCompanionId write.");

        _entities.Update(frame, _player);
        RememberedCompanion afterScriptWrite =
            CompanionRuntimeState.ReadRemembered(_entities.RuntimeState);
        FailIf(
            afterScriptWrite.Id != 0 ||
            afterScriptWrite.Group != remembered.Group ||
            afterScriptWrite.Room != remembered.Room ||
            afterScriptWrite.Y != remembered.Y ||
            afterScriptWrite.X != remembered.X ||
            _entities.Entities<GroundTreasurePickup>().Count != 1,
            "Room 5:b6 did not write only wRememberedCompanionId after " +
            "giveitem returned and before wait 30 began.");
        for (int update = 1;
            update < record.PostGrantWait - 1;
            update++)
        {
            _entities.Update(frame, _player);
        }
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 1 ||
            !_entities.PlayerMovementDisabled,
            "Room 5:b6's $6b:$0b released input before wait 30 completed.");
        _entities.Update(frame, _player);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.PlayerMovementDisabled ||
            _entities.PlayerItemUsageDisabled ||
            _entities.PlayerMenusDisabled,
            "Room 5:b6's $6b:$0b did not delete and release input on the " +
            "30th post-dialogue update.");

        GD.Print(
            "Validated room 5:b6 $6b:$0b: imported Cheval Rope placement/" +
            "visual, strict Manhattan-$0e touch boundary, $52:$00 grant and item flag, " +
            "held-item ordering, both companion-ID writes, input lease, and " +
            "30-update deletion boundary.");
    }
}

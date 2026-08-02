using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5bfInteractions()
    {
        const int group = 5;
        const int roomId = 0xbf;
        const double frame = 1.0 / 60.0;
        var database = new Room5bfDatabase();
        FailIf(
            database.Records.Count != 5 ||
            !database.Records.Select(record => record.Kind).SequenceEqual(
                new[]
                {
                    Room5bfInteractionKind.Flippers,
                    Room5bfInteractionKind.SlidingBlock,
                    Room5bfInteractionKind.SlidingBlock,
                    Room5bfInteractionKind.Lever,
                    Room5bfInteractionKind.LeverConnection
                }) ||
            database.Constants.Group != group ||
            database.Constants.Room != roomId ||
            database.Constants.LeverLength != 0x40 ||
            database.Constants.PullSpeed != 0x0a ||
            database.Constants.PostGrantWait != 30 ||
            database.Constants.CollisionRadiusY != 0x02 ||
            database.Constants.CollisionRadiusX != 0x02 ||
            database.Constants.PickupDistance != 0x0e ||
            database.BlockPalette.Length != 4,
            "Room 5:bf lost its imported flippers/block/block/lever order, " +
            "lever constants, or PALH_a3 palette.");

        _dialogue.Close();
        _inventory.LoseTreasure(TreasureDatabase.TreasureFlippers);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(group, roomId);

        GroundTreasurePickup pickup =
            _entities.Entities<GroundTreasurePickup>().Single();
        Room5bfSlidingBlock[] blocks = _entities
            .Entities<Room5bfSlidingBlock>()
            .OrderBy(block => block.BaseRecord.X)
            .ToArray();
        Room5bfLever lever = _entities.Entities<Room5bfLever>().Single();
        Room5bfLeverConnection connection =
            _entities.Entities<Room5bfLeverConnection>().Single();
        FailIf(
            pickup.Record.TreasureObject != "TREASURE_OBJECT_FLIPPERS_00" ||
            pickup.Record.Source != "mainData.s:group5MapbfObjectData" ||
            pickup.Position != new Vector2(0xb8, 0x1c) ||
            blocks.Length != 2 ||
            blocks[0].Position != new Vector2(0xb0, 0x38) ||
            blocks[1].Position != new Vector2(0xc0, 0x38) ||
            lever.Position != new Vector2(0x78, 0x10) ||
            connection.Position != new Vector2(0x78, 0x10) ||
            !blocks[0].CurrentAnimationUsesColor(database.BlockPalette[1]),
            "Room 5:bf did not create the source-positioned flippers, two " +
            "PALH_a3 blocks, lever, and source-created connection " +
            $"(pickup={pickup.Position}, blocks={blocks.Length}/" +
            $"{string.Join(',', blocks.Select(block => block.Position))}, " +
            $"lever={lever.Position}, connection={connection.Position}, " +
            $"opaque={blocks[0].CurrentAnimationOpaquePixels}, " +
            $"hash={blocks[0].CurrentAnimationPixelHash:x16}, " +
            $"palette1={blocks[0].CurrentAnimationUsesColor(database.BlockPalette[1])}, " +
            $"palette2={blocks[0].CurrentAnimationUsesColor(database.BlockPalette[2])}, " +
            $"palette3={blocks[0].CurrentAnimationUsesColor(database.BlockPalette[3])}).");

        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem);
        LoadValidationRoom(group, roomId);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.Entities<Room5bfSlidingBlock>().Count != 2 ||
            _entities.Entities<Room5bfLever>().Count != 1 ||
            _entities.Entities<Room5bfLeverConnection>().Count != 1,
            "ROOMFLAG_ITEM did not suppress only room 5:bf's $6b:$0c " +
            "flippers interaction on re-entry.");

        // The $6b script falls through from state 0 to its collection check,
        // disables input, grants the item, and retains that lease for 30
        // updates after the get-item textbox closes.
        _inventory.LoseTreasure(TreasureDatabase.TreasureFlippers);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(group, roomId);
        pickup = _entities.Entities<GroundTreasurePickup>().Single();
        _player.WarpTo(new Vector2(0xbf, 0x23), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Waiting ||
            _inventory.HasTreasure(TreasureDatabase.TreasureFlippers),
            "Room 5:bf's $6b:$0c collected at objectCheckLinkWithinDistance's " +
            "excluded Manhattan-distance $0e boundary.");
        _player.WarpTo(new Vector2(0xbf, 0x22), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            pickup.State != PickupState.Collected ||
            !pickup.Held ||
            !_inventory.HasTreasure(TreasureDatabase.TreasureFlippers) ||
            !_saveData.HasRoomFlag(
                group, roomId, OracleSaveData.RoomFlagItem) ||
            !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.Contains(
                "Flippers", StringComparison.Ordinal) ||
            !_entities.PlayerMovementDisabled ||
            !_entities.PlayerItemUsageDisabled ||
            !_entities.PlayerMenusDisabled ||
            !_entities.ScreenTransitionsDisabled,
            "Touching room 5:bf's $6b:$0c did not immediately grant and " +
            "display TREASURE_FLIPPERS $2e:$00 under the source input lease.");

        _dialogue.Close();
        _interactions.Update(frame, _player);
        FailIf(
            !pickup.Finished ||
            _entities.Entities<GroundTreasurePickup>().Count != 1,
            "Closing the room 5:bf Flippers textbox did not end the held " +
            "treasure while retaining its source 30-update script wait.");
        for (int update = 0; update < database.Constants.PostGrantWait - 1; update++)
            _entities.Update(frame, _player);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 1 ||
            !_entities.PlayerMovementDisabled,
            "Room 5:bf's $6b:$0c released input before wait 30 completed.");
        _entities.Update(frame, _player);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Count != 0 ||
            _entities.PlayerMovementDisabled ||
            _entities.PlayerItemUsageDisabled ||
            _entities.PlayerMenusDisabled,
            "Room 5:bf's $6b:$0c did not delete and release input on the " +
            "30th post-dialogue update.");

        _inventory.LoseTreasure(TreasureDatabase.TreasureFlippers);
        _saveData.SetRoomFlag(
            group, roomId, OracleSaveData.RoomFlagItem, value: false);
        LoadValidationRoom(group, roomId);
        blocks = _entities.Entities<Room5bfSlidingBlock>()
            .OrderBy(block => block.BaseRecord.X)
            .ToArray();
        lever = _entities.Entities<Room5bfLever>().Single();
        connection = _entities.Entities<Room5bfLeverConnection>().Single();
        _player.WarpTo(new Vector2(0x78, 0x1c), recordSafe: false);
        _player.Face(Vector2I.Up);
        _sound.ClearPlayRequestAudit();
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: false) ||
            _bracelet.State != BraceletState.PullingInteraction ||
            !lever.Grabbed ||
            _player.Position != new Vector2(0x78, 0x1c),
            "ITEM_BRACELET did not grab room 5:bf's downward $61:$30 lever " +
            "and snap Link to its source +$0c Y offset.");

        void PullLever(Vector2 input)
        {
            FailIf(
                !_playerWorld.UpdateBracelet(
                    _player,
                    input,
                    primaryHeld: false,
                    secondaryHeld: true,
                    itemButtonJustPressed: false),
                "ITEM_BRACELET released room 5:bf's lever while its assigned " +
                "button remained held.");
            _entities.Update(frame, _player);
            _sound.Tick();
        }

        PullLever(Vector2.Zero);
        FailIf(
            lever.PullDistance != 0 || lever.Position.Y != 0x10,
            "Holding room 5:bf's lever without moving down changed its pull distance.");
        for (int update = 0; update < 3; update++)
            PullLever(Vector2.Down);
        FailIf(
            lever.PullDistance != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 0,
            "Room 5:bf's SPEED_40 lever advanced its high byte or move sound " +
            "before four quarter-pixel updates.");
        PullLever(Vector2.Down);
        FailIf(
            lever.PullDistance != 1 || lever.Position.Y != 0x11 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1,
            "Room 5:bf's SPEED_40 lever lost its first four-update pixel/sound boundary.");
        for (int update = 4; update < 256; update++)
            PullLever(Vector2.Down);
        FailIf(
            lever.PullDistance != 0xc0 ||
            lever.Position != new Vector2(0x78, 0x50) ||
            _player.Position != new Vector2(0x78, 0x5c) ||
            connection.Phase != 4 ||
            connection.Position != new Vector2(0x78, 0x30) ||
            blocks[0].PullOffset != 15 ||
            blocks[1].PullOffset != 15 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndMoveBlock) != 1 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1,
            "Room 5:bf's lever did not reach flagged distance $c0 after 256 " +
            "SPEED_40 updates with one move sound, one open sound, and the " +
            "same-pass five-phase connection update.");

        PullLever(Vector2.Zero);
        FailIf(
            blocks[0].Position.X != 0xa0 ||
            blocks[1].Position.X != 0xd0 ||
            blocks[0].PullOffset != 16 ||
            blocks[1].PullOffset != 16,
            "Room 5:bf's blocks did not consume flagged pull distance $c0 " +
            "one source-ordered update after the lever reached full extension.");

        FailIf(
            _playerWorld.UpdateBracelet(
                _player,
                Vector2.Zero,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: false) ||
            _bracelet.State != BraceletState.Idle,
            "Releasing ITEM_BRACELET did not detach room 5:bf's lever.");
        _entities.Update(frame, _player);
        FailIf(
            lever.PullDistance != 0xc0 || lever.Position.Y != 0x50,
            "Room 5:bf's lever retracted during its source release substate update.");
        _entities.Update(frame, _player);
        FailIf(
            lever.PullDistance != 0x3f || lever.Position.Y != 0x4f,
            "Room 5:bf's lever did not begin SPEED_40 self-retraction on the " +
            "update after release.");

        // State 3 falls through to state 1, so the lever is grabbable again
        // while retracting.
        FailIf(
            !_playerWorld.TryUseBracelet(_player, primaryButton: false) ||
            _bracelet.State != BraceletState.PullingInteraction ||
            !lever.Grabbed ||
            _player.Position != new Vector2(0x78, 0x5b),
            "Room 5:bf's lever was not re-grabbable during self-retraction.");
        PullLever(Vector2.Zero);
        _playerWorld.UpdateBracelet(
            _player,
            Vector2.Zero,
            primaryHeld: false,
            secondaryHeld: false,
            itemButtonJustPressed: false);
        _entities.Update(frame, _player);

        int retractUpdates = 0;
        while (lever.PullDistance > 8 && retractUpdates++ < 260)
            _entities.Update(frame, _player);
        FailIf(
            lever.PullDistance != 8,
            "Room 5:bf's lever did not retain byte-exact pull distance 8 " +
            "during SPEED_40 self-retraction.");
        _player.WarpTo(new Vector2(0xb8, 0x38), recordSafe: false);
        _entities.Update(frame, _player);
        FailIf(
            !_player.SideScrollSquished ||
            blocks[0].PullOffset != 2 ||
            blocks[0].Position.X != 0xae,
            "Room 5:bf's left block did not apply the source $fe/$ff " +
            "horizontal squish window around $38,$b8.");
        while (lever.PullDistance != 0 && retractUpdates++ < 300)
            _entities.Update(frame, _player);
        _entities.Update(frame, _player);
        FailIf(
            lever.PullDistance != 0 || lever.Position.Y != 0x10 ||
            blocks[0].Position.X != 0xb0 ||
            blocks[1].Position.X != 0xc0 ||
            connection.Phase != 0 ||
            connection.Position.Y != 0x10 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndOpenChest) != 1,
            "Room 5:bf's lever, blocks, and connection did not return to " +
            "their source base state without replaying SND_OPENCHEST.");

        GD.Print(
            "Validated room 5:bf: ordered $6b:$0c Flippers grant/flag/input " +
            "wait, PALH_a3 $6b:$0d blocks and squish window, retained " +
            "Bracelet $61:$30 pull/re-grab/retract, flagged $c0 distance, " +
            "source-order block lag, five connection phases, and $71/$6c sounds.");
    }
}

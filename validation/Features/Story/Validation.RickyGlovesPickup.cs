using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom098RickyGlovesPickup()
    {
        const int group = 0;
        const int room = 0x98;
        const int rickyStateAddress = 0xc646;
        const int talkedMask = 0x01;
        const int completeMask = 0x20;
        const int glovesTreasure = 0x48;
        var database = new GroundTreasureDatabase();
        GroundTreasureDatabaseRecord record =
            database.GetRoomRecords(group, room).Single();
        FailIf(
            record is not
            {
                Order: 0, Y: 0x28, X: 0x48,
                TreasureObject: "TREASURE_OBJECT_RICKY_GLOVES_00",
                Sprite: "spr_quest_items_2", TileBase: 0x1c, Palette: 0x05,
                SpawnMode: 5, GrabMode: 1,
                RoomFlagTiming: GroundTreasureRoomFlagTiming.Never,
                StateAddress: rickyStateAddress,
                StateMask: talkedMask | completeMask,
                StateValue: talkedMask,
                RequireTreasureClear: glovesTreasure,
                BuriedInitialSpeedZ: -0x100,
                Gravity: 0x10,
                BuriedMoveSpeed: 0x14
            } ||
            record.CompletionTextId != 0 ||
            !string.IsNullOrEmpty(record.CompletionMessage),
            "Room 0:98 did not import $74:$00 and the Ricky's Gloves " +
            "spawn-mode-$05 treasure object exactly.");

        static Vector2 Point(int packedPosition) => new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        Vector2 dirtPoint = Point(0x24);

        void Configure(int state, bool gloves)
        {
            _dialogue.Close();
            _saveData.SetRoomFlag(
                group, room, OracleSaveData.RoomFlagItem, value: false);
            _saveData.WriteWramByte(rickyStateAddress, checked((byte)state));
            _inventory.LoseTreasure(glovesTreasure);
            if (gloves)
                _inventory.GiveTreasure(glovesTreasure, 0);
        }

        void ExpectSuppressed(int state, bool gloves, string phase)
        {
            Configure(state, gloves);
            LoadValidationRoom(group, room);
            FailIf(
                _entities.Entities<GroundTreasurePickup>().Any() ||
                _rooms.CurrentRoom.GetMetatile(dirtPoint) != 0x3a,
                $"Room 0:98 did not suppress $74:$00 and remove dirt for {phase}.");
        }

        ExpectSuppressed(0, gloves: false, "clear wRickyState bit $01");
        ExpectSuppressed(
            talkedMask | completeMask,
            gloves: false,
            "set wRickyState bit $20");
        ExpectSuppressed(
            talkedMask,
            gloves: true,
            "already-obtained treasure $48");

        Configure(talkedMask, gloves: false);
        LoadValidationRoom(group, room);
        GroundTreasurePickup gloves =
            _entities.Entities<GroundTreasurePickup>().Single();
        OracleRoomData gloveRoom = _rooms.CurrentRoom;
        FailIf(
            gloveRoom.GetMetatile(dirtPoint) !=
                gloveRoom.GetOriginalMetatile(dirtPoint) ||
            gloveRoom.GetMetatile(dirtPoint) == 0x3a ||
            gloves.Position != new Vector2(0x48, 0x28) ||
            gloves.State != PickupState.Initializing || gloves.Visible ||
            gloves.PixelHash == 0,
            "Eligible room 0:98 did not retain its dirt and preload hidden gloves.");

        _entities.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            gloves.State != PickupState.Spawning ||
            gloves.SpawnSubstate != 1 || gloves.Visible,
            "Spawn mode $05 did not remember packed dirt position $24 before waiting.");

        _player.WarpTo(gloves.Position);
        _player.Face(Vector2I.Right);
        _sound.ClearPlayRequestAudit();
        FailIf(
            !_shovel.TryDig(gloves.Position, Vector2I.Right),
            "The original room 0:98 dirt tile was not shovel-breakable.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            gloves.SpawnSubstate != 2 || !gloves.Visible ||
            gloves.BuriedAngle != 0x08 || gloves.SpeedZ != -0x100 ||
            gloves.Position != new Vector2(0x48, 0x28) ||
            gloves.State != PickupState.Spawning ||
            _inventory.HasTreasure(glovesTreasure) || _dialogue.IsOpen,
            "Digging packed position $24 did not launch the gloves right on the " +
            "following $74/$60 update without collecting them under Link.");

        _entities.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            gloves.Position.X != 0x49 || gloves.ZFixed >= 0 ||
            gloves.State != PickupState.Spawning ||
            _inventory.HasTreasure(glovesTreasure) || _dialogue.IsOpen,
            "Ricky's Gloves did not apply SPEED_080 and speedZ=-$100 motion " +
            "without entering the pickup path during the bounce.");

        for (int update = 0;
            update < 100 && gloves.State != PickupState.Waiting;
            update++)
        {
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(
            gloves.State != PickupState.Waiting || gloves.ZFixed != 0 ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence) != 2,
            "Spawn mode $05 did not stop after its two source half-speed bounces.");

        TreasureObjectRecord gloveObject =
            _treasures.GetObject("TREASURE_OBJECT_RICKY_GLOVES_00");
        _player.WarpTo(gloves.Position);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            gloves.State != PickupState.Collected ||
            !_inventory.HasTreasure(glovesTreasure) ||
            _saveData.HasRoomFlag(
                group, room, OracleSaveData.RoomFlagItem) ||
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(gloveObject.Message) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 1,
            "Touching the landed gloves did not grant treasure $48 without setting " +
            "room flag $20 and open TX_0067.");

        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            !gloves.Held || !_player.IsHoldingItemOneHand ||
            gloves.Position != _player.Position + new Vector2(-4, -14) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndGetItem) != 2,
            "Ricky's Gloves did not enter the next-update one-hand get-item pose.");

        _dialogue.Close();
        _interactions.Update(1.0 / 60.0, _player);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(
            _player.IsHoldingItemOneHand ||
            _entities.Entities<GroundTreasurePickup>().Any(),
            "Closing TX_0067 did not release Link and delete the gloves.");

        LoadValidationRoom(group, room);
        FailIf(
            _entities.Entities<GroundTreasurePickup>().Any() ||
            _rooms.CurrentRoom.GetMetatile(dirtPoint) != 0x3a,
            "Collected Ricky's Gloves respawned or left room 0:98 dirt behind.");

        GD.Print(
            "Validated room 0:98 $74:$00 predicates, dirt replacement, hidden " +
            "spawn-mode-$05 dig trigger, SPEED_080/two-bounce launch, treasure " +
            "$48 TX_0067 one-hand pickup, and re-entry suppression.");
    }
}

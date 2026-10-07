using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDungeonKeyDoors()
    {
        CompareDungeonKeyLocksRom();
        ReinitializeGameplayForValidation();
        const int group = 4;
        const int roomId = 0x0a;
        var database = new DungeonKeyDoorDatabase();
        bool hasLeftDoor = database.TryGet(
            2, 0x73, out DungeonKeyDoorDatabaseRecord leftDoor);
        bool hasBossRight = database.TryGet(
            2, 0x75, out DungeonKeyDoorDatabaseRecord bossRight);
        var keyBlockDatabase = new DungeonKeyBlockDatabase();
        DungeonKeyBlockDatabaseRecord keyBlock = keyBlockDatabase.Record;
        FailIf(
            database.Count != 8 ||
            !hasLeftDoor || !hasBossRight ||
            !database.TryGet(1, 0x73, out _) ||
            !database.TryGet(5, 0x73, out _) ||
            database.TryGet(0, 0x73, out _) ||
            database.TryGet(3, 0x73, out _) ||
            database.TryGet(4, 0x73, out _) ||
            leftDoor.Direction != Vector2I.Left || leftDoor.OpenTile != 0xa0 ||
            leftDoor.PushCounter != 20 || leftDoor.DoorFrameWait != 6 ||
            leftDoor.NoKeyTextId != 0x5100 || leftDoor.UsesBossKey ||
            !bossRight.UsesBossKey || bossRight.KeyGraphic != 0x43 ||
            bossRight.NoKeyTextId != 0x5101,
            "The imported $70-$77 small-key/boss-key door table is incomplete.");
        FailIf(
            keyBlock.ClosedTile != 0x1e || keyBlock.KeyGraphic != 0x42 ||
            keyBlock.OpenTile != 0xa0 || keyBlock.RoomFlag != 0x80 ||
            keyBlock.PushCounter != 20 ||
            keyBlock.OpenSound != SoundId.SndOpenChest ||
            keyBlock.KeySound != SoundId.SndGetSeed ||
            keyBlock.NoKeyTextId != 0x5102 ||
            keyBlock.NoKeyMessage != "Huh? This block\nhas a keyhole." ||
            keyBlock.PuffSound != SoundId.SndPoof ||
            !keyBlockDatabase.SupportsActiveCollisions(1) ||
            !keyBlockDatabase.SupportsActiveCollisions(2) ||
            !keyBlockDatabase.SupportsActiveCollisions(5) ||
            keyBlockDatabase.SupportsActiveCollisions(0) ||
            keyBlockDatabase.SupportsActiveCollisions(3) ||
            keyBlockDatabase.SupportsActiveCollisions(4) ||
            !keyBlock.Source.Contains("nextToKeyBlock", StringComparison.Ordinal),
            "The imported dungeon key-block $1e contract is incomplete.");

        // Past overworld room $dc reuses dungeon door index $73 as ordinary
        // scenery at $46. interactableTilesTable dispatches active-collision
        // mode 0 through @overworld, where $73 has no key-door behavior. Its
        // room-flag bit $08 is likewise an overworld flag, not a door flag.
        {
            const int overworldGroup = 1;
            const int overworldRoomId = 0xdc;
            const byte overworldFlag08 = 0x08;
            OracleSaveData overworldSave = OracleSaveData.CreateStandardGame();
            overworldSave.SetRoomFlag(
                overworldGroup, overworldRoomId, overworldFlag08);
            var overworldRooms = new RoomSession(
                overworldGroup, overworldRoomId,
                () => 0, () => { }, overworldSave);
            var overworldTreasures = new TreasureDatabase();
            var overworldInventory = new InventoryState(
                overworldTreasures,
                overworldSave,
                () => overworldRooms.CurrentDungeonIndex);
            using var overworldFixture = RoomEntityValidationFixture.Attach(
                this,
                "OverworldDungeonKeyTileValidation",
                new()
                {
                    SaveData = overworldSave,
                    Inventory = overworldInventory,
                    Treasures = overworldTreasures,
                    Rooms = overworldRooms
                });
            RoomEntityManager overworldEntities = overworldFixture.Manager;
            overworldEntities.LoadRoom(
                overworldGroup, overworldRooms.CurrentRoom);
            var overworldSounds = new List<int>();
            overworldEntities.SoundRequested += overworldSounds.Add;
            var overworldController = new DungeonKeyDoorController(
                overworldRooms,
                overworldInventory,
                overworldEntities,
                overworldTreasures,
                () => 0,
                overworldSounds.Add);
            string overworldMessage = string.Empty;
            overworldController.MessageRequested +=
                message => overworldMessage = message;

            OracleRoomData overworldRoom = overworldRooms.CurrentRoom;
            Vector2 sceneryCenter = new(0x68, 0x48);
            Vector2 linkRightOfScenery =
                sceneryCenter + Vector2.Right * 10;
            FailIf(
                overworldRoom.ActiveCollisions != 0 ||
                overworldRooms.CurrentDungeonIndex != -1 ||
                overworldRoom.GetPackedPosition(sceneryCenter) != 0x46 ||
                overworldRoom.GetMetatile(sceneryCenter) != 0x73,
                "Past overworld room 1:dc did not retain scenery $73 at $46 " +
                "when its overworld room-flag bit $08 was set.");

            for (int frame = 0; frame < leftDoor.PushCounter / 2; frame++)
            {
                overworldController.UpdatePushAttempt(
                    linkRightOfScenery, Vector2I.Left, Vector2.Left);
            }
            FailIf(
                overworldMessage.Length != 0 ||
                overworldController.Opening ||
                overworldController.RemainingPushFrames != keyBlock.PushCounter ||
                overworldRoom.GetMetatile(sceneryCenter) != 0x73 ||
                overworldEntities.Entities<DungeonKeyUseEffect>().Count != 0 ||
                overworldEntities.Entities<PuzzlePuffEffect>().Count != 0 ||
                overworldSounds.Count != 0,
                "Room 1:dc/$46 scenery $73 entered the dungeon key-door path " +
                "despite active-collision mode 0.");
            overworldController.Free();
        }

        LoadValidationRoom(group,roomId);
        _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
        int dungeon = _rooms.CurrentDungeonIndex;
        FailIf(dungeon != 0x0d || !_hud.DungeonKeyDisplayActive || _hud.DungeonIndex != dungeon ||
            _hud.StatusMapTileForValidation(0x0a) != 0x0a ||
            _hud.StatusMapTileForValidation(0x0b) != 0x1b ||
            _hud.StatusMapTileForValidation(0x0c) != 0x10 + _inventory.GetDungeonSmallKeys(dungeon) ||
            _hud.StatusMapTileForValidation(0x2a) != 0x10 + Mathf.Clamp(_hud.Rupees,0,999) / 100 ||
            _hud.StatusMapTileForValidation(0x2b) != 0x10 + Mathf.Clamp(_hud.Rupees,0,999) / 10 % 10 ||
            _hud.StatusMapTileForValidation(0x2c) != 0x10 + Mathf.Clamp(_hud.Rupees,0,999) % 10,
            "Dungeon key/X/count and rupee digits must coexist in the original HUD cells.");
        GD.Print("Validated ROM key-block/small-key/boss-key gameplay and independent import, HUD and scenery goldens.");
    }

}

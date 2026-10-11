using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeaChestRom()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool linked in new[] { false, true })
        foreach (int roomId in new[] { 0xe8, 0xff })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _inventory.GiveTreasure(TreasureId.MermaidSuit, 0);
            _inventory.GiveTreasure(TreasureId.Flippers, 0);
            _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(3, roomId);
            var controller = _entities.Entities<ChestContentsRoomEntity>().Single();
            int slot = _entities.InteractionSlot(controller);
            int address = 0xd040 + slot * 256;
            int subid = roomId == 0xe8 ? 0x15 : 0x16;
            bool ring = roomId == 0xe8 ? linked : !linked;
            // Independent source expectations: dc:$15/$16 swap the two
            // entries; RING_1e is treasure$2d, subid$1e, parameter$11.
            int treasureId = ring ? 0x2d : 0x34;
            int treasureSubid = ring ? 0x1e : 1;
            FailIf(!new ChestDatabase().TryGet(3, roomId, 0x35, out var chest) ||
                chest.TreasureId != 0 || _currentRoom.GetPackedStorageMetatile(0x35) != 0xf1,
                "Sea chest3:$e8/$ff must retain its static NONE record and source chest at $35.");
            _player.WarpTo(new(88, 104)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Sea chest approach must start on actual open ship floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 88, 104);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // The native file-load path copies wFileIsLinkedGame to wIsLinkedGame.
            rom[0xcc01] = linked ? (byte)1 : (byte)0;
            rom[address] = 1; rom[address + 1] = 0xdc; rom[address + 2] = (byte)subid;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, lastHeld = 0;
            void Step(int count = 1, int angle = 0xff, bool press = false)
            {
                int held = (press ? 1 : 0) | (angle == 0 ? 0x40 : 0);
                int edge = held & ~lastHeld; lastHeld = held;
                StepGameplayUpdates(count, angle == 0xff ? Vector2.Zero : Vector2.Up,
                    MenuRomActions(held), MenuRomActions(edge), batch, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter);
                    edge = 0; rom.AdvanceTileGraphics();
                    string context = $"Sea chest3:${roomId:x2}, linked={linked}, batch={batch}, update{++update}";
                    CompareSomariaMotionRom(rom, context);
                    for (int a = 0xc672; a < 0xc6cc; a++)
                        FailIf(_saveData.ReadWramByte(a) != rom[a], context + $": inventory${a:x4} differs.");
                    for (int a = 0xc5c0; a < 0xc600; a++)
                        FailIf(_saveData.ReadWramByte(a) != rom[a], context + $": unappraised ring${a:x4} differs.");
                    FailIf(_saveData.GetRoomFlags(3, roomId) != rom[0xc800 + roomId] ||
                        _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride) != rom[0xcca3] ||
                        _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != rom[0xcca4] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisableLinkCollisionsAndMenu) != rom[0xcbca],
                        context + $": flags=${_saveData.GetRoomFlags(3, roomId):x2}/${rom[0xc800 + roomId]:x2}, " +
                        $"override=${_runtimeState.ReadWramByte(WramAddress.wChestContentsOverride):x2}${_runtimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1):x2}/${rom[0xcca3]:x2}${rom[0xcca4]:x2}, " +
                        $"masks=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}, ${_runtimeState.ReadWramByte(WramAddress.wDisableLinkCollisionsAndMenu):x2}/${rom[0xcbca]:x2}.");
                    if (_interactions.ChestReward is { } reward)
                        FailIf(reward.Position != new Vector2(rom[0xd04d], rom[0xd04b]) ||
                            reward.VisualGraphic != rom[0xd042] ||
                            reward.Visible != ((rom[0xd05a] & 0x80) != 0), context + ": reserved treasure rise differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + ": reward sound order differs.");
                });
            }
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x83); rom[0xcc8a] = 0x83;
            Step();
            FailIf(rom[address + 4] != 1 || rom[0xcca3] != 0,
                "DC:$15/$16 state zero must not publish chest contents.");
            Step(3);
            FailIf(rom[0xcca3] != 0, "Initialized chest override must freeze under the interaction-disable mask.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            Step();
            FailIf(rom[0xcca3] != treasureId || rom[0xcca4] != treasureSubid || rom[address] != 0 ||
                _entities.Entities<ChestContentsRoomEntity>().Count != 0,
                "DC:$15/$16 must publish the linked/unlinked source selection once, then delete.");
            // These ship interiors retain underwater physics: fresh direction
            // edges start Mermaid strokes, rather than continuous walking.
            for (int stroke = 0; _player.Position.Y > 72 && stroke < 12; stroke++)
            { Step(); Step(8, 0); }
            Step(8, 0); Step(4);
            FailIf(_currentRoom.IsSolid(_player.Position) || _player.Position.Y > 75,
                $"Sea chest3:${roomId:x2} must be reachable through ship geometry: Link={_player.PrecisePosition}, native state=${rom[0xd004]:x2}/${rom[0xd005]:x2}, input=${rom[0xcc29]:x2}.");
            Step(press: true);
            FailIf(!_interactions.ChestRewardActive || _currentRoom.GetPackedStorageMetatile(0x35) != 0xf0 ||
                rom[0xcca3] != 0 || rom[0xcca4] != 0,
                "A-button chest opening must consume the override and open tile$35.");
            Step(33);
            FailIf(!_dialogue.IsOpen || rom[0xcba0] == 0 ||
                (ring ? _inventory.UnappraisedRingCount != 1 || _inventory.UnappraisedRingAt(0) != 0x51 : _inventory.GashaSeeds != 1),
                $"Sea chest3:${roomId:x2}, linked={linked}, ring={ring} must grant after rise: text={_dialogue.IsOpen}/${rom[0xcba0]:x2}, seeds={_inventory.GashaSeeds}, rings={_inventory.UnappraisedRingCount}, firstRing=${_inventory.UnappraisedRingAt(0):x2}.");
            Step(3);
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(); Step(press: true);
            FailIf(_interactions.ChestRewardActive, "Opened sea chest must not grant twice.");
            LoadValidationRoom(0, 0x60); LoadValidationRoom(3, roomId);
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x35) != 0xf0 ||
                _entities.Entities<ChestContentsRoomEntity>().Count != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride) != 0,
                "Opened sea chest must persist and suppress its contents controller on re-entry.");
        }
        ReinitializeGameplayForValidation();
        LoadValidationRoom(3, 0xe8);
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wChestContentsOverride) != 0x34 ||
            _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != 1,
            "Unopened sea chest must publish its source Gasha contents before exit.");
        LoadValidationRoom(0, 0x60);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wChestContentsOverride) != 0 ||
            _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != 0,
            "Room reload's $cc5c..$cce8 clear must retire unopened chest overrides.");
        LoadValidationRoom(3, 0xe8);
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wChestContentsOverride) != 0x34 ||
            _runtimeState.ReadWramByte(WramAddress.wChestContentsOverride + 1) != 1,
            "Unopened sea chest must restore its contents on re-entry.");
        GD.Print("Validated sea chest3:$e8/$ff source overrides, linked/unlinked rewards, native dispatch/freeze, reachable opening, rise/grant/sounds, cleanup, repeat and re-entry in split/batched gameplay.");
    }
}

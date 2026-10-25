using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedDungeonScripts()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            LoadValidationRoom(4, 0x05);
            _player.WarpTo(new(104, 72));
            FailIf(_collision.Collides(_player.Position), "Maku Path reward approach must start on source floor.");
            var script = _entities.Entities<SharedDungeonScriptRoomEntity>().Single();
            _runtimeState.SetWramByte(0xcfc1, 0xa5); _runtimeState.SetWramByte(0xcfc2, 0x5a);
            Step();
            var rupee = _entities.Entities<GroundTreasurePickup>().Single();
            FailIf(script.Finished || rupee.Record.TreasureObject != "TREASURE_OBJECT_RUPEES_0c" ||
                rupee.Position != new Vector2(136, 72) || _runtimeState.ReadWramByte(0xcfc1) != 0 ||
                _runtimeState.ReadWramByte(0xcfc2) != 0, "Maku Path $20:$02 must clear scratch and spawn the source 30-Rupee object in state0.");
            Step();
            FailIf(!script.Finished, "Maku Path reward script must end on the update after spawnitem.");
            int rupees = _inventory.Rupees;
            StepGameplayUpdates(24, Vector2.Right, batched: batch);
            FailIf(_inventory.Rupees != rupees + 30 || !_saveData.HasRoomFlag(4, 0x05, 0x20),
                $"Maku Path floor approach must collect 30 Rupees and persist flag $20; Link={_player.Position}.");
            _dialogue.Close(); Step(8);
            LoadValidationRoom(4, 0x05); Step(2);
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 || _entities.Entities<SharedDungeonScriptRoomEntity>().Count != 0,
                "Collected Maku Path Rupees must not respawn on re-entry.");

            LoadValidationRoom(4, 0x09);
            var torchState = SomariaPrivate<LightableTorchState>(_entities.Entities<LightableTorchScannerRoomEntity>().Single(), "_state");
            IsolateHerosCave(e => e is SharedDungeonScriptRoomEntity or LightableTorchScannerRoomEntity or LightableTorchRoomEntity);
            _player.WarpTo(new(120, 72));
            Step(2);
            var torches = _entities.Entities<LightableTorchRoomEntity>().ToArray();
            FailIf(torches.Length != 0, "The clean-US 4:09 layout contains no tile $08 for its placed scanner.");
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, "Maku Path key must wait for exactly one lit torch.");
            // This placed source branch has no torch in its clean-US layout.
            // Stage its external count; D3 below exercises the actual PART producer.
            torchState.IncrementLitCount();
            Step();
            FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, "Torch equality must yield before spawning the key.");
            Step();
            var key = _entities.Entities<GroundTreasurePickup>().Single();
            FailIf(key.Record.TreasureObject != "TREASURE_OBJECT_SMALL_KEY_01" || key.Record.SpawnMode != TreasureSpawnMode.FromScreenTop ||
                key.Position != new Vector2(168, 24), "Maku Path $20:$03 must create its falling Small Key at $18,$a8.");
            Step(26); // stairs: initial wait30, then checknoenemies yields.
            FailIf(_saveData.HasRoomFlag(4, 0x09, 0x80), "Maku Path stairs set completion before wait30 and checknoenemies.");
            Step(3); // checknoenemies, sound, then flag write.
            FailIf(!_saveData.HasRoomFlag(4, 0x09, 0x80), "Maku Path stairs must publish flag $80 after its solve cue.");
            Step(2); // puff, then wait15 installs.
            Step(14);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x1a) == 0x45, "Maku Path stairs appeared before the fifteenth wait update.");
            Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x1a) != 0x45, "Maku Path stairs must replace tile $1a with $45 on wait expiry.");
            LoadValidationRoom(4, 0x09); Step(2);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x1a) != 0x45, "Maku Path stairs must survive room re-entry.");

            LoadValidationRoom(4, 0x55);
            IsolateHerosCave(e => e is SharedDungeonScriptRoomEntity or LightableTorchScannerRoomEntity or LightableTorchRoomEntity);
            _player.WarpTo(new(120, 72)); Step(2);
            torches = _entities.Entities<LightableTorchRoomEntity>().ToArray();
            FailIf(torches.Length != 2, "D3 room 4:55 requires its two source torches.");
            torches[0].ApplySeedHit(torches[0].CollisionBounds, torches[0].Position, ItemId.EmberSeed, new List<RoomEntitySpawn>());
            Step(4);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x4c) == 0xf1, "One lit torch must not satisfy D3's exact-two chest script.");
            torches[1].ApplySeedHit(torches[1].CollisionBounds, torches[1].Position, ItemId.EmberSeed, new List<RoomEntitySpawn>());
            Step(); Step(3); Step(14);
            FailIf(_currentRoom.GetPackedStorageMetatile(0x4c) == 0xf1, "D3 chest appeared before its separate sound, puff and wait15 commands.");
            Step();
            FailIf(_currentRoom.GetPackedStorageMetatile(0x4c) != 0xf1 || _entities.Entities<SharedDungeonScriptRoomEntity>().Count != 0,
                "D3's exact-two script must write chest $f1 and end on the fifteenth wait update.");
        }
    }

    private void ValidateSharedSwitchesAndSarcophagi()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            LoadValidationRoom(4, 0x53);
            IsolateHerosCave(e => e is SwitchTriggerTranslatorRoomEntity);
            _entities.SetTrigger(1, true); _entities.SetTrigger(7, true);
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                foreach (byte switches in new byte[] { 0, 4, 0x80, 0x84, 0 })
                {
                    _runtimeState.SetWramByte(0xcdd3, switches); Step(3);
                    FailIf(_entities.ActiveTriggers != (0x82 | (switches & 4)), "INTERAC $24:$21 must mirror only switch bit 2 even under text.");
                }
            }
            finally { _entities.TextActiveSource = text; }
            foreach (int mask in new[] { 0, 1, 2, 4, 8, 15, 0 })
            {
                int[] rooms = [0x5d, 0x5f, 0x61, 0x63];
                for (int i = 0; i < 4; i++) _saveData.SetRoomFlag(4, rooms[i], 0x40, (mask & (1 << i)) != 0);
                _runtimeState.SetWramByte(0xcdd3, 0x05);
                LoadValidationRoom(4, 0x66); Step();
                FailIf(_runtimeState.ReadWramByte(0xcdd3) != (0x05 | mask << 4) || _entities.Entities<DungeonCrystalRestoreRoomEntity>().Count != 0,
                    $"INTERAC $21:$18 must OR the four crystal flags into bits 4..7 and delete, mask=${mask:x2}.");
            }
            foreach (var c in new (int Group, int Room, int Count)[] { (1,0x5b,1), (1,0x6d,1), (1,0x7c,1), (5,0xc7,6) })
            {
                LoadValidationRoom(c.Group, c.Room); Step();
                var actors = _entities.Entities<SarcophagusRoomEntity>();
                FailIf(actors.Count != c.Count || actors.Any(e => e.State != 1), $"Room {c.Group:x1}:{c.Room:x2} must initialize all {c.Count} source sarcophagi.");
                _saveData.SetRoomFlag(c.Group, c.Room, 0x40); LoadValidationRoom(c.Group, c.Room); Step();
                int remaining = c.Group == 5 ? 5 : 0;
                FailIf(_entities.Entities<SarcophagusRoomEntity>().Count != remaining, $"Room {c.Group:x1}:{c.Room:x2} must suppress only nonzero-subid sarcophagi after flag $40.");
            }
        }
    }

    private void ValidateSharedDecorationsAndGasha()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            _saveData.SetMakuTreeState(4); _saveData.SetGlobalFlag(AgesGlobalFlag.MakuTreeDisappeared);
            _saveData.SetGlobalFlag(AgesGlobalFlag.MakuTreeSaved);
            foreach (bool finished in new[] { false, true, false })
            foreach (var c in new (int Group, int Room, int X, int Y)[] { (0,0x38,0x84,0x40), (0,0x67,0x50,0x3c), (1,0x67,0x50,0x3c) })
            {
                _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, finished);
                LoadValidationRoom(c.Group, c.Room);
                FailIf(_entities.EntityAdapters<MakuLinkStatueRoomEntity>().Count() != 1,
                    $"Room {c.Group:x1}:{c.Room:x2} must allocate its $6b:$15 slot before the finished-game gate.");
                Step();
                var statues = _entities.EntityAdapters<MakuLinkStatueRoomEntity>().ToArray();
                FailIf(statues.Length != (finished ? 1 : 0), $"Room {c.Group:x1}:{c.Room:x2} statue finished-game gate differs.");
                if (finished) FailIf(statues[0].Node.Position != new Vector2(c.X, c.Y) ||
                    _currentRoom.GetTerrainInfo(new(c.X, c.Y)).Collision != 0x0f,
                    $"Room {c.Group:x1}:{c.Room:x2} statue lost its source position/solid tile.");
            }
            _saveData.SetGlobalFlag(AgesGlobalFlag.FinishedGame, false);
            foreach (bool portal in new[] { false, true, false })
            {
                LoadValidationRoom(0, 0x38);
                FailIf(_entities.Entities<MakuPortalDecorationRoomEntity>().Count != 1, "Maku Tree must allocate its $dc:$14 helper after $87:$00.");
                // Stage only this native handler's external tile input.
                _currentRoom.SetPositionTileAndCollision(new(0x78,0x58), portal ? (byte)0xdc : (byte)0xf9, null, 0);
                Step(3);
                FailIf(_entities.Entities<MakuPortalDecorationRoomEntity>().Count != 0 ||
                    _entities.Entities<DecorationRoomEntity>().Count != (portal ? 1 : 0), "Maku Tree portal helper must create $80:$00 only for tile $dc, then delete.");
                if (portal) FailIf(_entities.Entities<DecorationRoomEntity>().Single().Position != new Vector2(0x78,0x58), "Portal decoration must copy its producer's XY bytes.");
            }
            _saveData.SetGlobalFlag(AgesGlobalFlag.MoblinsKeepDestroyed, false);
            LoadValidationRoom(0, 0x09); Step();
            var flag = _entities.Entities<DecorationRoomEntity>().Single();
            FailIf(flag.Position != new Vector2(0x5e,0x18) || !flag.Visible, "King Moblin's flag must initialize at its source placement.");
            _saveData.SetGlobalFlag(AgesGlobalFlag.MoblinsKeepDestroyed); Step();
            FailIf(flag.Finished, "The $80:$01 destruction gate runs only in state0.");
            LoadValidationRoom(0, 0x09); Step();
            FailIf(_entities.Entities<DecorationRoomEntity>().Count != 0, "Destroyed King Moblin Keep must hide the flag on re-entry.");

            foreach (var c in new (int Room, int Essence, int X, int Y)[] { (0xe0,3,0x38,0x58), (0xe1,6,0x48,0x38), (0xe2,5,0x48,0x38) })
            {
                ReinitializeGameplayForValidation();
                LoadValidationRoom(0, c.Room);
                var producer = _entities.Entities<MakuGashaRewardRoomEntity>().Single();
                FailIf(_entities.InteractionSlot(producer) != 2, "Maku Gasha room-load helper must allocate slot $d2 before initializeRoom/placed objects.");
                Step();
                FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0 || !producer.Finished, "Missing essence must delete the Gasha helper without a reward.");
                _inventory.GiveTreasure(TreasureId.Essence, (c.Essence + 1) & 7);
                LoadValidationRoom(0, c.Room); Step();
                FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, $"Maku room 0:{c.Room:x2} accepted the wrong essence.");
                _inventory.GiveTreasure(TreasureId.Essence, c.Essence);
                LoadValidationRoom(0, c.Room);
                Vector2 rewardPoint = new(c.X,c.Y);
                var approach = new[] { Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up }.First(direction =>
                    new[] { 8,16,24 }.All(distance => !_collision.Collides(rewardPoint + direction * distance)));
                _player.WarpTo(rewardPoint + approach * 24); Step(2);
                var reward = _entities.Entities<GroundTreasurePickup>().Single();
                FailIf(reward.Position != new Vector2(c.X,c.Y) || reward.Record.TreasureObject != "TREASURE_OBJECT_GASHA_SEED_07",
                    $"Maku room 0:{c.Room:x2} lost its Gasha Seed $34:$07 position or identity.");
                FailIf(_collision.Collides(_player.Position), "Gasha approach must begin on the source floor.");
                int seeds = _inventory.GashaSeeds;
                StepGameplayUpdates(24, -approach, batched: batch); Step(40);
                FailIf(_inventory.GashaSeeds != seeds + 1 || !_saveData.HasRoomFlag(0,c.Room,0x20),
                    $"Maku room 0:{c.Room:x2} Gasha pickup must grant one seed and set item flag $20.");
                _dialogue.Close(); Step(4);
                LoadValidationRoom(0,c.Room); Step(2);
                FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0, "Collected Maku Gasha reward must not respawn.");
            }
        }
    }

    private void ValidateSharedEntryText()
    {
        foreach (bool batch in new[] { false, true })
        foreach (var c in new (int SourceGroup, int SourceRoom, int Group, int Room, int Text)[] {
            (4,0xea,4,0xd0,0x0209), (1,0xba,5,0xf9,0x120a) })
        {
            ReinitializeGameplayForValidation();
            void Step(int count = 1, Vector2? move = null) => StepGameplayUpdates(count, move ?? Vector2.Zero, batched: batch);
            LoadValidationRoom(c.Group, c.Room); _player.WarpTo(new(120,136)); Step();
            FailIf(_entities.Entities<RoomEntryTextRoomEntity>().Count != 0 || _dialogue.IsOpen,
                "Direct loading without wScrollMode bit $02 must delete entry text.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                LoadValidationRoom(c.SourceGroup,c.SourceRoom);
                _transitions.ApplyWarp(_player, new(c.SourceGroup,c.SourceRoom,-1,0,4,c.Group,c.Room,0xff,9,3,SourceFallback:true));
                int updates = 0;
                while (IsTransitioning && updates++ < 160) Step();
                FailIf(IsTransitioning, "Entry text warp did not finish its native entrance walk.");
                FailIf(_collision.Collides(_player.Position), "Entry text must approach its actor through the original entrance floor.");
                for (int i = 0; i < 16 && !_dialogue.IsOpen; i++) Step(move: Vector2.Up);
                FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != c.Text,
                    $"Room {c.Group:x1}:{c.Room:x2} warp entry must show TX_{c.Text:x4}; active={_rooms.ActiveGroup:x1}:{_currentRoom.Id:x2}, Link={_player.Position}, helpers={_entities.Entities<RoomEntryTextRoomEntity>().Count}, text={_dialogue.SourceTextId}.");
                _dialogue.Close(); Step(4);
                FailIf(_entities.Entities<RoomEntryTextRoomEntity>().Count != 0 || _dialogue.IsOpen,
                    "Entry text must delete after opening its message and remain closed until re-entry.");
            }
        }
    }

    private void ValidateSymmetryCompanionBarrier()
    {
        foreach (bool batch in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (var c in new (int Room, int X)[] { (0x22,0x38), (0x24,0x68) })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced, false);
            _saveData.WriteWramByte(AgesWramAddress.wAnimalCompanion,(byte)id);
            Vector2 start = new(c.X,32);
            CompanionRuntimeState.BeginAnimalRide(_runtimeState,_saveData,id,c.Room,start,ObjectDirection.Up);
            LoadValidationRoom(0,c.Room);
            IsolateHerosCave(e => e is ICompanionBarrierTarget or SymmetryCompanionBarrierRoomEntity);
            FailIf(_collision.Collides(start), "Symmetry barrier approach must start on source floor.");
            void Step(int count = 1, Vector2? move = null) => StepGameplayUpdates(count, move ?? Vector2.Zero, batched: batch);
            Step(2);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; i < 40 && !_dialogue.IsOpen; i++) Step(move: Vector2.Up);
                int expected = id switch { 0x0b => 0x200a, 0x0c => 0x2109, _ => 0x220a };
                var companion = _entities.EntityAdapters<IRoomEntity>().OfType<ICompanionBarrierTarget>().Single();
                FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != expected || (int)companion.BarrierPosition.Y != 17,
                    $"Room 0:{c.Room:x2}, companion ${id:x2}: native boundary must clamp Y=$11 and show TX_{expected:x4}; position={companion.BarrierPosition}.");
                _dialogue.Close(); Step(5, Vector2.Down);
            }
            _saveData.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced); Step();
            FailIf(_entities.Entities<SymmetryCompanionBarrierRoomEntity>().Count != 0, "Restored Tuni Nut must delete the Symmetry restriction.");
        }
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.WriteWramByte(AgesWramAddress.wDimitriState, 0x80);
            _saveData.WriteWramByte(AgesWramAddress.wAnimalCompanion, 0x0c);
            _inventory.GiveTreasure(TreasureId.Bracelet, 1); _inventory.EquipB(TreasureId.Bracelet);
            LoadValidationRoom(0, 0x22);
            IsolateHerosCave(e => e is SymmetryCompanionBarrierRoomEntity);
            var dimitri = _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(new(56, 40), ObjectDirection.Down, 0, 0x22));
            _player.WarpTo(new(56, 64)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Carried-Dimitri barrier approach must begin on source floor.");
            StepGameplayUpdates(8, Vector2.Up, ["move_up"], batched: batch);
            StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"], batch);
            StepGameplayUpdates(16, Vector2.Zero, ["item"], batched: batch);
            FailIf(!_player.IsCarryingObject || dimitri.Phase != DimitriPhase.Carried,
                $"Symmetry approach must lift Dimitri through the actual Bracelet parent; Link={_player.Position}, phase={dimitri.Phase}.");
            for (int i = 0; i < 60 && !_dialogue.IsOpen; i++)
                StepGameplayUpdates(1, Vector2.Up, ["item", "move_up"], batched: batch);
            FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != 0x2109 || _player.IsCarryingObject || dimitri.Phase != DimitriPhase.ThrownLanding,
                $"Symmetry barrier must drop carried Dimitri through state2/substate3 and show TX_2109; phase={dimitri.Phase}, Link={_player.Position}, Dimitri={dimitri.PrecisePosition}.");
            _dialogue.Close(); StepGameplayUpdates(60, Vector2.Zero, batched: batch);
            FailIf(_player.IsCarryingObject || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0,
                "Carried-Dimitri barrier completion must release the Bracelet pose and input.");
        }
    }
}

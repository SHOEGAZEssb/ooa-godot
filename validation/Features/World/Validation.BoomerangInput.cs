using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBoomerangInput()
    {
        void Room(bool primary = true)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(InventoryState.ItemBoomerang, 2);
            _inventory.EquipA(primary ? InventoryState.ItemBoomerang : 0);
            _inventory.EquipB(primary ? 0 : InventoryState.ItemBoomerang);
            _player.WarpTo(new(72.25f, 80.5f));
            _player.Face(Vector2I.Right);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
        }

        foreach (bool primary in new[] { true, false })
        foreach (bool batched in new[] { false, true })
        {
            Room(primary);
            string button = primary ? "attack" : "item";
            void Step(int count = 1, bool press = false, Vector2? movement = null) =>
                StepGameplayUpdates(count, movement ?? Vector2.Zero, [button], press ? [button] : [], batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Vector2 start = _player.PrecisePosition;
                Step(press: true);
                var item = _entities.Entities<BoomerangItem>().Single();
                var parent = _entities.BoomerangParent;
                FailIf(!parent.Active || parent.Slot != 3 || parent.Mode != 0x21 || parent.Graphic != 0xb0 ||
                    parent.Parameter != 6 || parent.Counter != 8 || !_player.StartedItemAnimationThisUpdate ||
                    item.State != 1 || item.Counter != 40 || item.Angle != 8 || item.PrecisePosition != start,
                    "An A/B throw must initialize parent3/mode$21 and its L1 child in the same gameplay update, copying fractional XY.");
                Step(7, movement: Vector2.Left);
                FailIf(parent.Counter != 1 || !parent.Active || _player.PrecisePosition != start ||
                    _player.FacingVector != Vector2I.Right || _player.StartedItemAnimationThisUpdate,
                    "The throw must lock movement/turning through its eighth displayed update without repeating the start signal.");
                Step(movement: Vector2.Left);
                FailIf(!parent.Active || parent.Parameter != 0x86 || parent.Counter != 127 || _player.PrecisePosition != start,
                    "The terminal parameter appears on update9 and must retain the parent lock for that update.");
                Step(movement: Vector2.Left);
                FailIf(parent.Active || _player.PrecisePosition.X != start.X - 1 || item.State != 1,
                    "Update10 releases the parent lock while the independent child keeps flying.");
                // A new edge cannot allocate a second child or leave Link in
                // a throw pose. Holding the button also never repeats a throw.
                Step(press: true, movement: Vector2.Right);
                FailIf(parent.Active || _entities.Entities<BoomerangItem>().Count != 1 ||
                    _player.PrecisePosition != start || _player.StartedItemAnimationThisUpdate,
                    "A live child must reject a repeat edge without immobilizing Link or publishing a start signal.");
                int remaining = 140;
                while (!item.Finished && remaining-- > 0) Step();
                FailIf(!item.Finished, "The isolated input fixture's boomerang did not return.");
                Step(4);
                FailIf(_entities.Entities<BoomerangItem>().Count != 0 || parent.Active,
                    "Held input must not rethrow after the child releases its slot.");
            }

            Room(primary);
            // Fill all five native child slots without replacing the input
            // owner or its allocation method. These beams are isolated filler.
            for (int i = 0; i < 5; i++)
                _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(160, 112), 1));
            Step(press: true, movement: Vector2.Right);
            FailIf(_entities.Entities<BoomerangItem>().Count != 0 || _entities.BoomerangParent.Active ||
                _player.StartedItemAnimationThisUpdate || _player.PrecisePosition.X != 73.25f,
                "A full dynamic ITEM pool must cancel the throw and release its movement/use bits immediately.");

            Room(primary);
            Step(press: true, movement: new Vector2(1, -1));
            var diagonal = _entities.Entities<BoomerangItem>().Single();
            FailIf(diagonal.Angle != 4 || diagonal.PrecisePosition != new Vector2(72.25f, 80.5f),
                "A diagonal press must copy wLinkAngle instead of quantizing to the facing direction.");
            _dialogue.ShowMessage("Throw pause.", _player.Position.Y);
            Step(12);
            FailIf(_entities.BoomerangParent.Counter != 8 || diagonal.Counter != 40,
                "Text must freeze both the parent pose and initialized child.");
            for (int i = 0; _dialogue.IsOpen && i < 180; i++)
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], i % 12 == 0 ? ["attack"] : [], batched);
            FailIf(_dialogue.IsOpen, "The text fixture must close using normal input.");
            _entities.ClearPhysicalPlayerItems();
            FailIf(_entities.BoomerangParent.Active || _entities.Entities<BoomerangItem>().Count != 0,
                "Clearing physical items must release the parent and dynamic child together.");
            StepGameplayUpdates(1, Vector2.Zero); // Release the textbox's consumed button.
            Step(press: true);
            FailIf(!_entities.BoomerangParent.Active, "A cancelled throw must allow a fresh input edge.");
            var survivingChild = _entities.Entities<BoomerangItem>().Single();
            _playerWorld.ClearItemParents(_player);
            FailIf(_entities.BoomerangParent.Active || survivingChild.Finished || _entities.Entities<BoomerangItem>().Count != 1,
                "Clearing parent items alone must leave the boomerang child and its cap allocated.");
            Step(press: true);
            FailIf(_entities.BoomerangParent.Active || _entities.Entities<BoomerangItem>().Count != 1,
                "Parent cancellation must not bypass the still-live child's one-instance cap.");
        }

        // ParentItem2 is independent of the boomerang's ParentItem3. Both
        // A/B assignments must accept a simultaneous sword and boomerang.
        foreach (bool primary in new[] { true, false })
        {
            Room(primary);
            _inventory.GiveTreasure(InventoryState.ItemSword, 0);
            if (primary) _inventory.EquipB(InventoryState.ItemSword);
            else _inventory.EquipA(InventoryState.ItemSword);
            StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
            FailIf(!_player.IsAttacking || !_player.IsUsingBoomerang || _entities.Entities<BoomerangItem>().Count != 1,
                "Sword and boomerang must coexist in their separate native parents regardless of button assignment.");
        }

        foreach (bool raft in new[] { false, true })
        {
            Room();
            if (raft)
            {
                _saveData.SetGlobalFlag(new RaftDatabase().Behavior.ChangedRoomsFlag);
                LoadValidationRoom(1, 0xa7);
                var raftEntity = _entities.Entities<RaftRoomEntity>().Single();
                _player.WarpTo(raftEntity.PrecisePosition + new Vector2(4, -4), recordSafe: false);
                _entities.Update(1.0 / 60.0, _player); // Publish the raft's support before Link's water handler.
                StepGameplayUpdates(3, Vector2.Zero);
                FailIf(!_player.RaftRideActive, "The raft fixture must provide a live mounted object owner.");
            }
            else _player.SetMinecartRidePosition(new(72, 89), 1, 0, Vector2.Zero);
            _player.Face(Vector2I.Right);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(!_entities.BoomerangParent.Active || _entities.BoomerangParent.Mode != (raft ? 0x21 : 0x25) ||
                _entities.BoomerangParent.Graphic != (raft ? 0xb0 : 0xcc),
                $"Minecart throws use mode$25; the raft exception retains mode$21: raft={raft}, active={_entities.BoomerangParent.Active}, mode={_entities.BoomerangParent.Mode:x2}, graphic={_entities.BoomerangParent.Graphic:x2}, A={_inventory.EquippedA:x2}, riding={_player.RaftRideActive}, swimming={_player.TopDownSwimming}, child={_entities.Entities<BoomerangItem>().Count}.");
            StepGameplayUpdates(8, Vector2.Left, batched: true);
            FailIf(_player.FacingVector != Vector2I.Right ||
                _entities.BoomerangParent.Graphic != (raft ? 0xb0 : 0x58),
                "Mounted terminal graphics and turning lock must survive through update9.");
            var mountedChild = _entities.Entities<BoomerangItem>().Single();
            int catchLimit = 140;
            while (mountedChild.State != 4 && catchLimit-- > 0)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(mountedChild.State != 4, "The mounted throw must reach its hidden catch state.");
            Vector2 fractions = mountedChild.PrecisePosition - mountedChild.Position;
            StepGameplayUpdates(1, Vector2.Zero);
            Vector2 mountedPosition = raft ? _entities.Entities<RaftRoomEntity>().Single().PrecisePosition.Floor() : new(72, 89);
            FailIf(mountedChild.PrecisePosition != mountedPosition + fractions,
                "The catch delay must copy the mounted object's high XY, rather than the offset Link sprite.");
        }

        foreach (bool mermaid in new[] { false, true })
        {
            PrepareSwimmingRoom(mermaid: mermaid);
            _inventory.GiveTreasure(InventoryState.ItemBoomerang, 2);
            _inventory.EquipB(InventoryState.ItemBoomerang);
            StepGameplayUpdates(1, Vector2.Zero, ["item"], ["item"]);
            FailIf(_entities.BoomerangParent.Active || _entities.Entities<BoomerangItem>().Count != 0,
                "Swimming must reject a boomerang even when B input reaches checkUseItems.");
        }
        LoadValidationRoom(0, 0x60);
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaInstrumentInput()
    {
        // checkUseItems allocates both inputs before updating ParentItem2..5.
        // Cane initializes in slot2; harpFluteParent state0 subsequently fails
        // checkNoOtherParentItemsInUse in slot5 before setting wcc95 bit7.
        foreach (int instrument in new[] { InventoryState.ItemHarp, InventoryState.ItemFlute })
        foreach (bool canePrimary in new[] { false, true })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(InventoryState.ItemSomaria, 1);
            _inventory.GiveTreasure(instrument, 0);
            _inventory.EquipA(canePrimary ? InventoryState.ItemSomaria : instrument);
            _inventory.EquipB(canePrimary ? instrument : InventoryState.ItemSomaria);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            string caneButton = canePrimary ? "attack" : "item";
            string instrumentButton = canePrimary ? "item" : "attack";
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 72));
                _player.Face(Vector2I.Down);
                Step();
                StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
                FailIf(!_player.IsUsingSomaria || _player.IsUsingHarp || _harp.IsPlaying,
                    $"Cane ParentItem2 must initialize before instrument${instrument:x2} checks other slots, for either A/B assignment.");
                Step(12);
                StepGameplayUpdates(1, Vector2.Zero, [instrumentButton], [instrumentButton]);
                FailIf(!_player.IsUsingSomaria || _player.IsUsingHarp,
                    "An instrument press during the Cane swing must not interrupt it.");
                Step(10);
                FailIf(_player.IsUsingSomaria || _entities.Entities<SomariaBlock>().Count() != 1,
                    "Rejected instrument input must leave one completed Cane block.");
                StepGameplayUpdates(1, Vector2.Zero, [instrumentButton], [instrumentButton]);
                FailIf(!_player.IsUsingHarp || !_harp.IsPlaying,
                    "After the Cane parent clears, a fresh instrument press must start normally.");
                Step();
                StepGameplayUpdates(1, Vector2.Zero, [caneButton], [caneButton]);
                FailIf(!_player.IsUsingHarp || _player.IsUsingSomaria,
                    "An already-playing instrument's wcc95 bit7 must prevent new Cane input.");
                for (int update = 0; update < 512 && _player.IsUsingHarp; update++) Step();
                FailIf(_player.IsUsingHarp || _harp.IsPlaying,
                    "Crown Dungeon instrument playback must finish and release its input lock.");
            }
        }
    }
}

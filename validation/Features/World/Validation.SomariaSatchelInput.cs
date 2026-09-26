using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSomariaSatchelInput()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool canePrimary in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 0);
            _inventory.GiveTreasure(TreasureId.EmberSeeds, 5);
            _inventory.EquipA(canePrimary ? TreasureId.CaneOfSomaria : TreasureId.SeedSatchel);
            _inventory.EquipB(canePrimary ? TreasureId.SeedSatchel : TreasureId.CaneOfSomaria);
            for (int y = 8; y < 176; y += 16)
            for (int x = 8; x < 240; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            int Seeds() => (_inventory.EmberSeeds >> 4) * 10 + (_inventory.EmberSeeds & 15);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _entities.ClearPhysicalPlayerItems();
                _player.WarpTo(new(72, 40));
                _player.Face(Vector2I.Down);
                Step();
                int seeds = Seeds();
                StepGameplayUpdates(1, Vector2.Zero, ["attack", "item"], ["attack", "item"]);
                FailIf(!_player.IsUsingSomaria || !_player.IsUsingSeedSatchel || Seeds() != seeds - 1 ||
                    _entities.Entities<EmberSeedEffect>().Count != 1 || _entities.Somaria!.Parent!.Parameter != 0,
                    $"Cane/satchel separate slots: A-cane={canePrimary}, repeat={repeat}, cane={_player.IsUsingSomaria}, satchel={_player.IsUsingSeedSatchel}, seeds={Seeds()}/{seeds}, children={_entities.Entities<EmberSeedEffect>().Count}, parameter={_entities.Somaria?.Parent?.Parameter}.");
                Step(13);
                FailIf(_entities.Entities<SomariaBlock>().Count != 0 || !_player.IsUsingSomaria || _player.Position != new Vector2(72, 40),
                    "Satchel completion must not release the Cane's movement lock or skip its block-creation delay.");
                Step();
                FailIf(_entities.Entities<SomariaBlock>().Count != 1 || _entities.Somaria!.Weapon!.State != 2,
                    "Overlapping satchel use must preserve Cane update14 creation and its reserved weapon owner.");
                Step(12);
                FailIf(_player.IsUsingSomaria || _player.IsUsingSeedSatchel || Seeds() != seeds - 1,
                    "Both parents must finish independently without consuming another seed.");
                for (int i = 0; i < 120 && _entities.Entities<EmberSeedEffect>().Any(); i++) Step();
                FailIf(_entities.Entities<EmberSeedEffect>().Any(), "Let the previous seed's flame finish before repeating.");
            }
        }
    }
}

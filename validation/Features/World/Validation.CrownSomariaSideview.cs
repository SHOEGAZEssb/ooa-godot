using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownSomariaSideview()
    {
        foreach (bool batched in new[] { false, true })
        foreach (string button in new[] { "attack", "item" })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(6, 0x93);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.EquipA(button == "attack" ? TreasureId.CaneOfSomaria : TreasureId.None);
            _inventory.EquipB(button == "item" ? TreasureId.CaneOfSomaria : TreasureId.None);
            Vector2? start = null;
            for (int y = 24; y < _currentRoom.Height - 24 && start is null; y += 16)
            for (int x = 24; x < _currentRoom.Width - 40 && start is null; x += 16)
            {
                Vector2 point = new(x, y);
                if (!_collision.Collides(point) &&
                    _currentRoom.GetTerrainInfo(point).Collision == 0 &&
                    _currentRoom.GetTerrainInfo(point + new Vector2(16, 0)).Collision == 0 &&
                    _currentRoom.GetTerrainInfo(point + new Vector2(0, 16)).Collision == 0x0f &&
                    _currentRoom.GetTerrainInfo(point + new Vector2(16, 16)).Collision == 0x0f)
                    start = point;
            }
            FailIf(start is null, "Crown passage6:93 must supply an actual clear floor approach for Cane input.");
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(start!.Value);
                Step(20);
                _player.Face(Vector2I.Right);
                FailIf(!_player.IsGroundedForFloorButton, "Side-view Cane fixture must settle on the original room floor.");
                FailIf(!_player.NativeNormalStateForInteraction,
                    "Ordinary Crown side-view movement must remain in native Link state01.");
                StepGameplayUpdates(1, Vector2.Zero, [button], [button]);
                Step(13);
                FailIf(!_player.IsUsingSomaria || _entities.Entities<SomariaBlock>().Any(),
                    "Side-view input must retain the normal Cane delay before child allocation.");
                Step();
                var block = _entities.Entities<SomariaBlock>().Single();
                FailIf(block.State != 1 || block.ZHigh != 0,
                    "Side-view ITEM$18 must merge copied Z into Y during phase-in initialization.");
                Step(9);
                FailIf(block.State != 3 || block.Finished || _player.IsUsingSomaria ||
                    _currentRoom.GetMetatile(block.Position) != 0xda,
                    "Supported side-view Cane must finish with one solid block after the parent clears.");
                Vector2 support = block.Position + new Vector2(0, 16);
                byte supportTile = _currentRoom.GetMetatile(support);
                _currentRoom.SetPositionTileAndCollision(support, supportTile, 0, 0);
                Step();
                FailIf(!block.Finished || _entities.Entities<SomariaBlock>().Any() ||
                    _currentRoom.GetMetatile(block.Position) == 0xda,
                    "Loss of full support must remove the side-view block and restore its underlying tile.");
                _currentRoom.SetPositionTileAndCollision(support, supportTile, 0x0f, 0);
                Step();
            }
        }
    }
}

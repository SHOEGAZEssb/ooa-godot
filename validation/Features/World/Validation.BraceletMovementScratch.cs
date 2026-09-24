using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBraceletMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (bool dropped in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _inventory.EquipA(InventoryState.ItemBracelet);
            for (int y = 48; y <= 112; y += 16)
            for (int x = 96; x <= 128; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            _currentRoom.SetPositionTileAndCollision(new(120, 88), 0x10, 15, 0);
            _player.WarpTo(new(120, 106));
            _player.Face(Vector2I.Up);
            // Approach the pot on clear floor, then pull and lift normally.
            StepGameplayUpdates(8, Vector2.Up, batched: batch);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
            StepGameplayUpdates(11, Vector2.Down, ["attack"], batched: batch);
            StepGameplayUpdates(13, Vector2.Zero, batched: batch);
            FailIf(_bracelet.State != BraceletState.Holding || _bracelet.LiftedObject is null,
                $"Bracelet scratch fixture must lift the real pot through input: {_bracelet.State} at {_player.Position}.");
            byte[] expected = dropped ? [0xff, 0xa5, 0xa5, 0xa5] : [0x80, 0xfe, 0, 0];
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, i == 0 ? (byte)0xff : (byte)0xa5);
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"ITEM$16 dropped={dropped}: scratch ${0xcec0 + i:x4} differs after the thrown item update.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, dropped ? Vector2.Zero : Vector2.Up, ["attack"], ["attack"], batch);
            var item = _bracelet.LiftedObject!;
            FailIf(item is not { Thrown: true }, "Bracelet input must release the lifted tile.");
            Vector2 before = item.GroundPosition;
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(observations != 3 || item.GroundPosition != before + (dropped ? Vector2.Zero : new Vector2(0, -3)),
                "Thrown tiles must execute SPEED_180 each update; angle-$ff drops must preserve scratch.");
        }
        ReinitializeGameplayForValidation();
    }
}

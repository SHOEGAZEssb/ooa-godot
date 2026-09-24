using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateThrownPotDamage()
    {
        foreach (bool batch in new[] { false, true })
        foreach (bool dropped in new[] { false, true })
        foreach (int delay in new[] { 0, 9 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 1);
            _inventory.EquipA(InventoryState.ItemBracelet);
            for (int y = 32; y <= 112; y += 16)
            for (int x = 96; x <= 144; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);

            // Repeat after impact/recovery so stale parent state cannot prevent reuse.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                _currentRoom.SetPositionTileAndCollision(new(120, 88), 0x10, 15, 0);
                _player.WarpTo(new(120, 106));
                _player.Face(Vector2I.Up);
                StepGameplayUpdates(8, Vector2.Up, batched: batch);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batch);
                StepGameplayUpdates(11, Vector2.Down, ["attack"], batched: batch);
                StepGameplayUpdates(13, Vector2.Zero, batched: batch);
                FailIf(!_bracelet.HoldingTile || _bracelet.LiftedObject is null,
                    "4:$a8 damage fixture failed to lift pot $10 through adjacent floor.");
                StepGameplayUpdates(1, dropped ? Vector2.Zero : Vector2.Up,
                    ["attack"], ["attack"], batch);
                StepGameplayUpdates(delay, Vector2.Zero, batched: batch);
                var pot = _bracelet.LiftedObject!;
                FailIf(pot is not { Thrown: true } ||
                    _bracelet.State != (delay == 0 ? BraceletState.Throwing : BraceletState.Projectile),
                    "ITEM $16 must remain airborne during and after Link's throw animation.");
                Vector2 ground = pot.GroundPosition;
                int z = pot.ZFixed;
                int speedZ = pot.SpeedZ;
                int speedRaw = pot.SpeedRaw;
                Vector2I direction = pot.ThrowDirection;
                Node parent = pot.GetParent();
                int health = _player.HealthQuarters;
                FailIf(!_player.ApplyEnemyContactDamage(_player.Position + Vector2.Left * 16, quarters: 1) ||
                    _player.HealthQuarters != health - 1 || _player.KnockbackFrames <= 0,
                    "Thrown-pot regression must accept actual contact damage and enter recoil.");
                // bombsBraceletParent state3 unlinks relatedObj2 before throwing;
                // dropLinkHeldItem cannot re-release state3 ITEM $16. Its position,
                // angle and velocity survive Link's parent cancellation unchanged.
                FailIf(_bracelet.LiftedObject != pot || _bracelet.State != BraceletState.Projectile ||
                    pot.GroundPosition != ground || pot.ZFixed != z || pot.SpeedZ != speedZ ||
                    pot.SpeedRaw != speedRaw || pot.ThrowDirection != direction || pot.GetParent() != parent,
                    $"ITEM $16 damage re-released an airborne pot (drop={dropped}, delay={delay}, batch={batch}).");

                // Source itemWeights row $00: SPEED_180, speedZ=$ff10, gravity=$1c.
                // The first throw update already applied velocity then gravity.
                FailIf(speedZ != (dropped ? 0 : -0xf0) + (delay + 1) * 0x1c,
                    "ITEM $16 did not retain the source throw velocity before recoil.");
                int remaining = 0;
                int landingZ = z;
                int landingSpeed = speedZ;
                do
                {
                    landingZ += landingSpeed;
                    landingSpeed += 0x1c;
                    remaining++;
                } while (landingZ < 0);
                StepGameplayUpdates(remaining - 1, Vector2.Zero, batched: batch);
                int updates = remaining - 1;
                FailIf(_bracelet.LiftedObject != pot ||
                    pot.GroundPosition != ground + (dropped ? Vector2.Zero : Vector2.Up * (1.5f * updates)) ||
                    pot.ZFixed != z + updates * speedZ + 0x1c * updates * (updates - 1) / 2 ||
                    pot.SpeedZ != speedZ + updates * 0x1c,
                    "ITEM $16 must retain its full ballistic arc through Link's recoil until landing.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batch);
                FailIf(_bracelet.LiftedObject is not null || _bracelet.State != BraceletState.Idle ||
                    _entities.Entities<RockDebrisEffect>().Count != 1,
                    "ITEM $16 must break exactly on landing, producing its pot debris after damage.");
                StepGameplayUpdates(60, Vector2.Zero, batched: batch);
                FailIf(_bracelet.LiftedObject is not null || _bracelet.State != BraceletState.Idle,
                    "Completed pot flight must leave the Bracelet ready for another lift.");
            }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated thrown/dropped pot motion and landing through damage during/after throw, repeated with batched gameplay updates.");
    }

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

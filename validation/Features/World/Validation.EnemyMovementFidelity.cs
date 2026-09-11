using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyMovementReturnFlags()
    {
        LoadValidationRoom(4, 0x44);
        var node = new Node2D();
        var movement = new EnemyTerrainMovement(node, _currentRoom);
        void Check(string branch, Vector2 start, int speed, int angle,
            int walls, Vector2 expected, bool expectedMoving)
        {
            node.Position = start;
            bool moving = movement.MoveGivenAdjacentWalls(angle, speed,
                new EnemyAdjacentWallProbe(walls, (walls & 0x0c) != 0, (walls & 3) != 0));
            FailIf(node.Position != expected || moving != expectedMoving,
                $"ecom_applyGivenVelocityGivenAdjacentWalls {branch}, speed=${speed:x2}, " +
                $"angle=${angle:x2}, walls=${walls:x2}: expected {expected}/{expectedMoving}, " +
                $"got {node.Position}/{moving}.");
        }

        // commonCode.s slides by signed $0060, setting hFF8D only below
        // SPEED_140 ($32). Both cumulative probe tables use these same bits.
        Vector2 center = new(64.5f, 64.5f);
        Check("upper right probe, slow", center, 0x2d, 0x08, 2, center + new Vector2(0, 0.375f), true);
        Check("upper right probe, charge", center, 0x32, 0x08, 2, center + new Vector2(0, 0.375f), false);
        Check("lower right probe, charge", center, 0x32, 0x08, 1, center + new Vector2(0, -0.375f), false);
        Check("left upward probe, charge", center, 0x32, 0x00, 8, center + new Vector2(0.375f, 0), false);
        Check("right upward probe, charge", center, 0x32, 0x00, 4, center + new Vector2(-0.375f, 0), false);
        Check("two slides, charge", center, 0x32, 0x0c, 9, center + new Vector2(0.375f, -0.375f), false);
        Check("two slides, slow", center, 0x2d, 0x0c, 9, center + new Vector2(0.375f, -0.375f), true);
        Check("full wall", center, 0x32, 0x08, 3, center, false);

        // bank3.objectSpeedTable gives +/-$16 at SPEED_20, angle $04/$0c,
        // and +/-$3e for X at SPEED_140, angle $01/$1f. A negative low byte
        // passes cp $20/$60 even without a pixel carry. Expectations are
        // source literals, not calculations using the runtime speed table.
        Check("small positive component", center, 0x05, 0x0c, 3, center + new Vector2(0, 22 / 256.0f), false);
        Check("small negative component", center, 0x05, 0x04, 3, center + new Vector2(0, -22 / 256.0f), true);
        Check("small positive component with carry", new(64.5f, 64.96875f), 0x05, 0x0c, 3, new(64.5f, 65.0546875f), true);
        Check("charge component below $60", center, 0x32, 0x01, 0x0c, center + new Vector2(62 / 256.0f, 0), false);
        Check("negative charge component", center, 0x32, 0x1f, 0x0c, center + new Vector2(-62 / 256.0f, 0), true);
        Check("X word overflow", new(255.875f, 64.5f), 0x0a, 0x08, 0, new(0.125f, 64.5f), true);
        Check("Y word underflow", new(64.5f, 0), 0x05, 0x00, 0, new(64.5f, 255.875f), true);
        node.Free();
        GD.Print("Validated enemy movement status independently of displacement, " +
            "SPEED_140 slide threshold, signed fractional components, and 8.8 wrap.");
    }

    private void ValidateEnemyCornerCharges()
    {
        const double update = 1.0 / 60.0;
        // Actual room geometry with the source's right-facing probes:
        // center twice, (x+$06,y-$01), (x+$06,y+$05).
        int RightWalls(Vector2 position)
        {
            int x = Mathf.FloorToInt(position.X);
            int y = Mathf.FloorToInt(position.Y);
            bool Solid(int px, int py) => _currentRoom.IsSolidForEnemyMovement(
                new Vector2(px, py), holesAreWalls: true);
            return (Solid(x, y) ? 0x0c : 0) | (Solid(x + 6, y - 1) ? 2 : 0) |
                (Solid(x + 6, y + 5) ? 1 : 0);
        }
        Vector2 FindCorner(int distance)
        {
            for (int y = 24; y < _currentRoom.Height - 24; y++)
            for (int x = distance + 24; x < _currentRoom.Width - 24; x++)
            {
                var corner = new Vector2(x, y);
                if (RightWalls(corner) is not (1 or 2)) continue;
                if (Enumerable.Range(1, distance).All(offset =>
                    RightWalls(corner - new Vector2(offset, 0)) == 0)) return corner;
            }
            throw new InvalidOperationException(
                $"Room 4:${_currentRoom.Id:x2} lacks a {distance}-pixel path into a source corner probe.");
        }
        string Run(bool beetle, bool batched)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, beetle ? 0x44 : 0x1c);
            EnemyCharacter enemy = beetle ? _entities.Entities<SpikedBeetleCharacter>()[0]
                : _entities.Entities<RopeCharacter>()[0];
            int distance = beetle ? 48 : 12;
            Vector2 corner = FindCorner(distance);
            enemy.Position = corner - new Vector2(distance, 0) + new Vector2(0.25f, 0.5f);
            _player.WarpTo(enemy.Position + new Vector2(20, 0), recordSafe: false);
            Vector2 start = enemy.Position;
            base._Process(2 * update);
            bool Charging() => beetle
                ? ((SpikedBeetleCharacter)enemy).State == SpikedBeetleState.Charging
                : ((RopeCharacter)enemy).State == RopeState.Charging;
            FailIf(!Charging() || enemy.Position != start,
                $"ENEMY_${(beetle ? 0x14 : 0x10):x2} did not acquire a rightward charge after state 0.");
            // Move Link off the locked line before contact, also preventing
            // the resting beetle from immediately reacquiring him.
            _player.WarpTo(new Vector2(start.X, start.Y > 64 ? 32 : 96), recordSafe: false);
            int frames = 0;
            bool charging = true;
            Vector2 expectedPosition = enemy.Position;
            while (charging && frames < 120)
            {
                Vector2 before = expectedPosition;
                int walls = RightWalls(before);
                frames++;
                int speed = beetle ? Math.Min(60, 10 + 5 * ((frames + 2) / 4)) : 50;
                expectedPosition = walls == 0 ? before + new Vector2(speed / 40.0f, 0)
                    : before + new Vector2(0, walls == 1 ? -0.375f : 0.375f);
                charging = walls == 0 || speed < 50;
                if (batched) continue;
                int randomCalls = _entities.RandomCalls;
                var expectedRandom = new OracleRandom();
                expectedRandom.RestoreState(_random.CaptureState());
                OracleRandomResult directionRoll = expectedRandom.Next();
                base._Process(update);
                FailIf(enemy.Position != expectedPosition || Charging() != charging,
                    $"ENEMY_${(beetle ? 0x14 : 0x10):x2} corner charge update {frames}: " +
                    $"walls=${walls:x2}, speed=${speed:x2}, expected {expectedPosition}, got {enemy.Position}.");
                if (!beetle && !charging)
                {
                    var rope = (RopeCharacter)enemy;
                    FailIf(_entities.RandomCalls != randomCalls + 1 ||
                        rope.Angle != (directionRoll.High & 0x18) ||
                        rope.Counter != 0x70 + (directionRoll.Low & 0x70),
                        "Rope $10 corner stop did not consume exactly one shared RNG call " +
                        "for angle=high&$18 and counter1=$70+(low&$70).");
                }
            }
            FailIf(frames >= 120, "Corner charge never reached the source blocked return.");
            int Remaining() => beetle ? ((SpikedBeetleCharacter)enemy).Counter1 : ((RopeCharacter)enemy).Cooldown;
            FailIf(!batched && Remaining() != (beetle ? 30 : 0x40),
                "The corner slide did not install the source 30-update rest / $40-update cooldown.");
            // The batched host call crosses charge movement, the blocked
            // update, and the first eight recovery ticks together.
            if (batched) base._Process((frames + 8) * update);
            else for (int i = 0; i < 8; i++) base._Process(update);
            FailIf(Remaining() != (beetle ? 22 : 0x38),
                "Corner rest/cooldown did not advance on the following eight gameplay updates.");
            string snapshot = $"{frames}:{enemy.Position}:{Remaining()}:{enemy.AnimationFrame}:" +
                $"{_random.CaptureState()}:{_inventory.HealthQuarters}";
            int recovery = beetle ? 22 : 0x38;
            if (batched) base._Process(recovery * update);
            else for (int i = 0; i < recovery; i++) base._Process(update);
            FailIf(beetle
                ? ((SpikedBeetleCharacter)enemy).State != SpikedBeetleState.Wandering ||
                    ((SpikedBeetleCharacter)enemy).Speed != 10
                : Remaining() != 0 || ((RopeCharacter)enemy).State != RopeState.Wandering,
                "Corner recovery did not complete on the source 30th/$40th update.");
            // Repeat the charge after recovery with the same real corner.
            enemy.Position = start;
            _player.WarpTo(start + new Vector2(20, 0), recordSafe: false);
            base._Process(update);
            FailIf(!Charging() || enemy.Position != start,
                "The enemy could not acquire another charge after corner recovery.");
            return snapshot + $":{_random.CaptureState()}:{enemy.AnimationFrame}";
        }
        foreach (bool beetle in new[] { false, true })
            FailIf(Run(beetle, false) != Run(beetle, true),
                $"ENEMY_${(beetle ? 0x14 : 0x10):x2} corner recovery diverged in batched host updates.");
        GD.Print("Validated Rope $10 and Spiked Beetle $14 corner slides through real room " +
            "geometry, charge termination, recovery counters and split/batched gameplay updates.");
    }
}

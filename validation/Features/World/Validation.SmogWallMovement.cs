using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogWallMovement()
    {
        var data = new SmogWallDatabase();
        var memory = new OracleRuntimeState();
        var velocityOwner = new ValidationMovementCharacter();
        velocityOwner.BindMovementMemory(memory);
        OracleObjectVelocity Velocity(int speed, int angle) => velocityOwner.MovementVelocity(speed, angle);
        Vector2I[] front = [new(0,-16),new(16,0),new(0,16),new(-16,0)];
        Vector2I[] probes = [new(-8,-9),new(15,0),new(-15,17),new(15,0),
            new(-16,-16),new(0,15),new(17,-15),new(0,15)];
        FailIf(!data.FrontOffsets.SequenceEqual(front) || !data.ProbeOffsets.SequenceEqual(probes) ||
            !data.Speeds.SequenceEqual(new[] { 0, 0, 0x23, 0x14, 0x0a }),
            "Smog tables must retain source cardinal offsets, cumulative eight-probe walk and subid speeds.");
        foreach (int subid in new[] { 2, 0x82 })
        {
            var puffs = new List<Vector2>();
            var move = new SmogWallMovement(data, subid, OracleObjectPosition.FromPixels(new(72,72)), 0, _ => 0, Velocity);
            move.Update(puffs.Add);
            int finalXVelocity = subid == 2 ? 0x00e0 : 0xff20;
            FailIf(memory.ReadWramByte(0xcec0) != 0 || memory.ReadWramByte(0xcec1) != 0 ||
                memory.ReadWramByte(0xcec2) != (finalXVelocity & 255) || memory.ReadWramByte(0xcec3) != (finalXVelocity >> 8),
                "Smog's second movement must replace the first movement's scratch with the post-turn signed $00e0 vector.");
            FailIf(move.Position.YFixed != 0x4720 || move.Position.XFixed != (subid == 2 ? 0x48e0 : 0x4720) ||
                move.Substate != 1 || move.MissingWallCounter != 16 || move.Direction != (subid == 2 ? 1 : 3) ||
                move.WallCoordinateSum != (subid == 2 ? 9 : 7),
                $"Smog${subid:x2} must move $e0 forward, turn toward its missing wall, then move $e0 again on its first update.");
            for (int tick = 2; tick <= 16; tick++) move.Update(puffs.Add);
            FailIf(move.Position != OracleObjectPosition.FromPixels(new(72,72)) || move.MissingWallCounter != 1 || puffs.Count != 0,
                "Four missing-wall turns close a square; small Smog must not respawn through update16.");
            move.Update(puffs.Add);
            FailIf(!puffs.SequenceEqual(new Vector2[] { new(72,71),new(72,72) }) ||
                move.Position.YFixed != 0x4740 || move.Position.XFixed != 0x4800 || move.Substate != 0 ||
                move.Direction != 0 || move.MissingWallCounter != 0,
                "Small Smog update17 must puff at both high-byte positions, retain fractions on reset and move again afterward.");
        }
        foreach (int subid in new[] { 3, 0x83 })
        {
            var puffs = new List<Vector2>();
            var move = new SmogWallMovement(data, subid, OracleObjectPosition.FromPixels(new(72,72)), 0, _ => 0, Velocity);
            for (int tick = 0; tick < 20; tick++) move.Update(puffs.Add);
            FailIf(move.Position.YFixed != 0x4780 || move.Position.XFixed != (subid == 3 ? 0x5200 : 0x3e00) ||
                move.Substate != 1 || move.MissingWallCounter != 16 || puffs.Count != 0,
                "Medium Smog keeps moving at $80 in the new direction instead of decrementing the missing-wall counter or respawning.");
        }
        foreach (int subid in new[] { 2, 0x82 })
        {
            byte collision = 1; // Any nonzero raw byte is a wall; no quadrant mask.
            var move = new SmogWallMovement(data, subid, OracleObjectPosition.FromPixels(new(72,72)), 0, _ => collision, Velocity);
            move.Update(_ => FailIf(true, "Blocked Smog must not respawn."));
            var stopped = move.Position;
            FailIf(stopped.YFixed != 0x4720 || stopped.XFixed != 0x4800 || move.Substate != 2 || move.AdjacentWalls != 255,
                "Hugged and blocked Smog moves once before turning in place.");
            for (int turn = 2; turn <= 4; turn++)
            {
                for (int offset = 0; offset < 4; offset++) memory.SetWramByte(0xcec0 + offset, 0xa5);
                move.Update(_ => { });
                FailIf(Enumerable.Range(0, 4).Any(offset => memory.ReadWramByte(0xcec0 + offset) != 0xa5),
                    "Smog's blocked turn-only substate must preserve movement scratch.");
                FailIf(move.Position != stopped || move.Direction != ((subid == 2 ? -turn : turn) & 3),
                    "Substate2 must rotate once per blocked update without moving.");
            }
            collision = 0;
            move.Update(_ => { });
            FailIf(move.Substate != 0 || move.Position.YFixed != 0x4640 || move.Position.XFixed != 0x4800,
                "Opening a wall in substate2 returns to substate0 and moves once in the current direction.");
        }
        var reads = new List<int>();
        var wrapped = new SmogWallMovement(data, 2, OracleObjectPosition.FromPixels(new(8,8)), 0,
            packed => { reads.Add(packed); return 1; }, Velocity);
        wrapped.Update(_ => { });
        FailIf(!reads.SequenceEqual(new[] { 0xf0,0xf0,0x00,0x00,0xff,0x0f,0xf1,0x01 }),
            "Smog probes must accumulate signed offsets with byte wrapping before packed tile lookup, including padding/outside storage.");
        for (int bit = 0; bit < 8; bit++)
        {
            int calls = 0;
            var move = new SmogWallMovement(data, 2, OracleObjectPosition.FromPixels(new(72,72)), 0,
                _ => (byte)(calls++ % 8 == bit ? 0x10 : 0), Velocity);
            move.Update(_ => { });
            FailIf(move.AdjacentWalls != (0x80 >> bit), "Smog eight raw collision probes must shift into the source bit order.");
        }
        velocityOwner.Free();
        GD.Print("Validated isolated Smog wall table bytes, double movement, both turn senses, small respawn fractions/timing, medium continuation and wrapped cumulative probes.");
    }
}

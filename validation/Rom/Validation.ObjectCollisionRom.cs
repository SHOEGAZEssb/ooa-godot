using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateObjectCollisionGeometryRom()
    {
        var rom = new ObjectCollisionRom();
        int cases = 0;
        foreach (int axis in new[] { 0, 1 })
        foreach (int radiusA in new[] { 0, 1, 6, 127, 128, 255 })
        for (int radiusB = 0; radiusB < 256; radiusB++)
        for (int delta = 0; delta < 256; delta++)
        {
            int origin = delta % 3 == 0 ? 0 : delta % 3 == 1 ? 127 : 255;
            Vector2 target = new(origin, origin), other = target;
            other[axis] = (origin + delta) & 255;
            Vector2 a = Vector2.One, b = Vector2.One;
            a[axis] = radiusA; b[axis] = radiusB;
            rom[0xd08b] = (byte)target.Y; rom[0xd08d] = (byte)target.X;
            rom[0xd0a6] = (byte)a.Y; rom[0xd0a7] = (byte)a.X;
            rom[0xd60b] = (byte)other.Y; rom[0xd60d] = (byte)other.X;
            rom[0xd626] = (byte)b.Y; rom[0xd627] = (byte)b.X;
            bool expected = rom.Call(ObjectCollisionRom.Overlap);
            // Different low bytes must not change the integer hitbox comparison.
            Rect2 boundsA = new(target + Vector2.One * 0.25f - a, a * 2);
            Rect2 boundsB = new(other + Vector2.One * 0.75f - b, b * 2);
            FailIf(RoomEntityManager.ObjectCollisionXYOverlaps(boundsA, boundsB) != expected,
                $"ROM XY axis={axis}, radii=${radiusA:x2}/${radiusB:x2}, origin=${origin:x2}, delta=${delta:x2}.");
            cases++;
        }
        // Link's shared body helper is another consumer of the same byte arithmetic.
        for (int radius = 0; radius < 256; radius++)
        for (int delta = 0; delta < 256; delta++)
        {
            rom[0xd08b] = rom[0xd60b] = 64;
            rom[0xd08d] = 0; rom[0xd60d] = (byte)delta;
            rom[0xd0a6] = rom[0xd627] = rom[0xd626] = 6;
            rom[0xd0a7] = (byte)radius;
            bool expected = rom.Call(ObjectCollisionRom.Overlap);
            FailIf(Player.EnemyCollisionOverlaps(new(delta, 64), new(new(-radius, 58), new(radius * 2, 12))) != expected,
                $"ROM Link body radius=${radius:x2}, delta=${delta:x2}.");
            cases++;
        }
        rom[0xd00b] = rom[0xd08b] = 64;
        rom[0xd00d] = rom[0xd08d] = 80;
        rom[0xd026] = rom[0xd027] = rom[0xd0a6] = rom[0xd0a7] = 6;
        for (int firstZ = 0; firstZ < 256; firstZ++)
        for (int secondZ = 0; secondZ < 256; secondZ++)
        {
            rom[0xd00f] = (byte)firstZ; rom[0xd08f] = (byte)secondZ;
            bool expected = rom.Call(ObjectCollisionRom.LinkOverlap);
            FailIf(RoomEntityManager.ObjectCollisionZOverlaps(firstZ, secondZ, 7) != expected,
                $"ROM height check Z=${firstZ:x2}/${secondZ:x2}.");
            cases++;
        }
        GD.Print($"Validated {cases} ROM object XY/Link-body/Z collision cases including fractional positions, byte wrapping and asymmetric edges.");
    }
}

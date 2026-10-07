using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombchuSteeringRom(BombchuDatabase data)
    {
        var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 72, 72);
        var nativeAnimation = new ObjectAnimationRom(3, 0x0d);
        const int item = 0xd700;
        rom[item] = 1; rom[item + 1] = 0x0d;
        rom.Word(item + 0x18, 0xd080);
        rom[item + 0xb] = rom[item + 0xd] = 72;
        int comparisons = 0;
        void Initialize(BombchuSteering steering)
        {
            rom[item + 8] = (byte)steering.Direction;
            rom[item + 9] = (byte)steering.Angle;
            rom[item + 0x31] = (byte)steering.Turn;
            rom[item + 0x32] = rom[item + 0x33] = rom[item + 0x34] = 0;
        }
        void Compare(BombchuSteering steering, bool animationChanged, string context)
        {
            FailIf(rom[item + 9] != steering.Angle || rom[item + 8] != steering.Direction ||
                rom[item + 0x31] != steering.Turn || (rom[item + 0x32] != 0) != steering.Clinging ||
                rom[item + 0x33] != steering.FormerAngle || (rom[item + 0x34] != 0) != steering.Ceiling,
                $"ITEM$0d {context}: native angle/direction/turn/cling/former/ceiling differs.");
            if (animationChanged)
            {
                nativeAnimation.Set(steering.Animation);
                FailIf(rom.Word(item + 0x22) != nativeAnimation.Pointer,
                    $"ITEM$0d {context}: native walking/ceiling animation selector differs.");
            }
            comparisons++;
        }
        // Eight corner/axis targets plus near-boundary targets. Inputs are
        // coordinates; the original relative-angle helper supplies the native
        // answer before its Bombchu caller selects an axis/turn direction.
        Vector2[] targets = [new(72, 32), new(80, 32), new(96, 32), new(112, 32),
            new(112, 48), new(112, 64), new(112, 72), new(112, 80), new(112, 96), new(112, 112),
            new(96, 112), new(80, 112), new(72, 112), new(64, 112), new(48, 112), new(32, 112),
            new(32, 96), new(32, 80), new(32, 72), new(32, 64), new(32, 48), new(32, 32),
            new(48, 32), new(64, 32)];
        foreach (bool side in new[] { false, true })
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (Vector2 target in targets)
        {
            // Vertical side-view steering is reached while climbing; using
            // the normal constructor here declares that existing axis.
            var steering = new BombchuSteering(data, direction, false);
            Initialize(steering);
            rom[0xd08b] = (byte)target.Y; rom[0xd08d] = (byte)target.X;
            int relative = OracleObjectMovement.Shared.RelativeAngle(new Vector2(72, 72), target);
            bool changed = steering.Home(relative, side);
            rom.HomeBombchu(item, side);
            Compare(steering, changed, $"homing side={side}, dir={direction}, target={target}");
            if (!side)
            {
                changed = steering.TurnFromImpassableTile();
                rom.TurnBombchuFromHole(item);
                Compare(steering, changed, "impassable turn");
            }
        }
        foreach (int direction in new[] { 1, 3 })
        {
            var steering = new BombchuSteering(data, direction, true);
            Initialize(steering);
            rom.Word(item + 0x14, -128); rom[item + 0x10] = 0;
            foreach (byte collision in new byte[] { 0, 0xff, 0x0f, 0x0f, 0, 0x0f, 0x0f, 0x0f, 0x0f, 0 })
            {
                for (int packed = 0; packed < 256; packed++) rom[0xce00 + packed] = collision;
                var result = steering.CheckWalls(_ => collision);
                rom.CheckBombchuWalls(item);
                Compare(steering, result.AnimationChanged, $"wall collision=${collision:x2}");
                FailIf(result.ResetSpeedZ && rom.Word(item + 0x14) != 0,
                    "ITEM$0d uncling must clear both vertical-speed bytes.");
            }
        }
        for (int packed = 0; packed < 256; packed++) rom[0xce00 + packed] = 0;
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int x in new[] { 28, 29, 31, 32, 35, 36 })
        foreach (int y in new[] { 25, 26, 29, 30, 31, 32, 35, 36 })
        {
            rom[item + 9] = (byte)(direction * 8);
            rom[item + 0xb] = (byte)y; rom[item + 0xd] = (byte)x;
            Vector2I probe = new Vector2I(x, y) + data.FrontOffset(direction * 8);
            int packed = (probe.Y & 0xf0) | (probe.X >> 4);
            rom[0xce00 + packed] = 0x0f;
            FailIf(rom.ReadBombchuFrontCollision(item) != 0x0f,
                $"ITEM$0d front offset dir={direction}, XY={x},{y}: native collision probe differs.");
            rom[0xce00 + packed] = 0;
            comparisons++;
        }
        FailIf(rom.RandomCalls != 0 || rom.Sounds.Count != 0, "ITEM$0d steering must consume no RNG or cues.");
        GD.Print($"Compared {comparisons} bounded native Bombchu steering cases: both homing axes, source turn choice, boundary reversal, wall attachment, ceiling graphics and vertical-speed reset.");
    }
}

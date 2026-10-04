using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBraceletGrabEdgesRom()
    {
        // objectHCheckCollisionWithLink and checkGrabbableObjects share the
        // facing projection, Link.zh-$03 and _checkCollisionWithHAndD.
        var rom = new ObjectCollisionRom();
        var geometry = new BraceletGrabGeometry();
        Vector2I[] projection = [new(0, -6), new(5, 0), new(0, 5), new(-6, 0)];
        int cases = 0;
        for (int direction = 0; direction < 4; direction++)
        foreach (int origin in new[] { 0, 80, 255 })
        foreach (int radius in new[] { 4, 6, 8 })
        foreach (int axis in new[] { 0, 1 })
        foreach (int delta in new[] { -radius - 7, -radius - 6, -radius - 5, 0, radius + 5, radius + 6, radius + 7 })
        foreach (int z in new[] { -11, -10, -9, -3, 3, 4, 5 })
        foreach (bool pending in new[] { false, true })
        {
            Vector2 link = new(origin, origin);
            Vector2 target = link + (Vector2)projection[direction];
            target[axis] += delta;
            target = new((byte)(int)target.X, (byte)(int)target.Y);
            rom[0xd008] = (byte)direction;
            rom[0xd00b] = rom[0xd00d] = (byte)origin;
            rom[0xd00f] = 0;
            rom[0xd026] = rom[0xd027] = 6;
            rom[0xd78b] = (byte)target.Y;
            rom[0xd78d] = (byte)target.X;
            rom[0xd78f] = unchecked((byte)z);
            rom[0xd7a6] = rom[0xd7a7] = (byte)radius;
            rom[0xd7aa] = pending ? (byte)0x80 : (byte)0;
            bool expected = rom.Call(0x1c89, obj: 0xd000, other: 0xd780);
            bool actual = geometry.Overlaps(new(link - new Vector2(6, 6), new(12, 12)), 0, direction,
                new(target - new Vector2(radius, radius), new(radius * 2, radius * 2)), z, pending);
            FailIf(actual != expected,
                $"Bracelet native grab direction={direction}, origin=${origin:x2}, radius={radius}, axis={axis}, delta={delta}, z={z}, pending={pending}: runtime={actual}, ROM={expected}.");
            cases++;
        }
        GD.Print($"Validated {cases} executed-ROM Bracelet grab XY/Z boundaries, facing projection, byte wrapping and pending-hit rejection.");
    }

    private void CompareBraceletPullCancellationRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            BraceletRom rom = PrepareBraceletRom(primary: primary, underlying: 0xa1);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            StepBraceletRom(rom, 6, batched, 16, held: true, primary: primary);
            StepBraceletRom(rom, 3, batched, held: true, primary: primary); // Reset pull animation.
            StepBraceletRom(rom, 10, batched, 16, held: true, primary: primary);
            FailIf(_bracelet.State != BraceletState.GrabbingWall || rom.ChildActive,
                "Changing pull direction must restart the eleven-update tile lift gate.");
            StepBraceletRom(rom, 1, batched, primary: primary); // Release the assigned button.
            FailIf(_bracelet.State != BraceletState.Idle || _currentRoom.GetMetatile(new(120, 88)) != 0x10,
                "Cancelling a wall pull must preserve pot $10 and release the parent.");
            StepBraceletRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            StepBraceletRom(rom, 11, batched, 16, held: true, primary: primary);
            FailIf(_currentRoom.GetMetatile(new(120, 88)) != 0xa1 || rom[0xcf57] != 0xa1,
                "A moved dungeon pot must restore source layout floor $a1, rather than the generic $a0 replacement.");
            StepBraceletRom(rom, 13, batched, primary: primary);
        }
        foreach (bool batched in new[] { false, true })
        {
            BraceletRom rom = PrepareBraceletRom(tile: 0xb0);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true);
            StepBraceletRom(rom, 23, batched, 16, held: true);
            FailIf(_bracelet.Counter != 11 || rom.ChildActive || _currentRoom.GetMetatile(new(120, 88)) != 0xb0,
                "An unbreakable wall must retain the terminal pull frame and retry without creating a child.");
            StepBraceletRom(rom, 1, batched);
        }
        GD.Print("Validated executed-ROM Bracelet pull reset/cancellation/retry, failed unbreakable-wall lifts and moved-pot underlying $a1 restoration through A/B and individual/batched gameplay updates.");
    }
}

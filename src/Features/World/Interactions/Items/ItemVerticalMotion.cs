using Godot;

namespace oracleofages;

// commonCode1.s:itemUpdateThrowingVertically. State lives with the item; the
// helper returns the contact and effect for its caller's deletion policy.
internal static class ItemVerticalMotion
{
    internal static bool AdvanceSideview(OracleRoomData room, ref Vector2 position,
        ref int z, ref int speedZ, ref int flags, int gravity, out int effect)
    {
        position.Y = (byte)((int)position.Y + (sbyte)(byte)(z >> 8)) + position.Y - Mathf.Floor(position.Y);
        z = 0;
        Vector2 pixel = position.Floor();
        int kind = ItemHazardDatabase.Shared.Hazard(room.ActiveCollisions,
            room.GetMetatile(new(pixel.X, (byte)((int)pixel.Y + 5))));
        int previous = flags;
        flags = ((flags & 0xb8) ^ 0x80) | kind;
        if (((kind ^ previous) & 1) != 0) flags |= 0x40;
        bool rising = speedZ < 0;
        bool collision = room.IsSolid(rising ? pixel : new(pixel.X, (byte)((int)pixel.Y + 5)));
        effect = 0;
        if (!rising && collision) { flags |= 0x10; return true; }
        flags &= ~0x10;
        bool waterPause = !collision && (kind & 1) != 0 && (flags & 0x80) != 0;
        if (!waterPause)
        {
            if (!collision)
                position.Y = unchecked((ushort)(Mathf.RoundToInt(position.Y * 256) + speedZ)) / 256.0f;
            speedZ = unchecked((short)(speedZ + gravity));
            int maximum = collision || (kind & 1) == 0 ? 0x300 : 0x100;
            if (speedZ >= maximum) speedZ = maximum;
        }
        // The wrapper checks splash transitions even when leaving water.
        effect = (kind & 4) != 0 ? 4 : (flags & 0x40) != 0 ? 1 : 0;
        return false;
    }
}

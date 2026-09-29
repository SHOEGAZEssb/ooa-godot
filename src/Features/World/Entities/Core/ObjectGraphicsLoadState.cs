using System;

namespace oracleofages;

// Native residency, distinct from Godot's texture cache. A missing header
// suspends interactionInitGraphics while VBlank transfers its tiles.
internal sealed class ObjectGraphicsLoadState(OracleRuntimeState memory)
{
    private const int Cursor = 0xcc06;
    private const int Headers = 0xcc08;

    internal bool Require(byte header)
    {
        if (header == 0) return false;
        for (int slot = 0; slot < 8; slot++)
        {
            int address = Headers + slot * 2;
            if (memory.ReadWramByte(address) != header) continue;
            memory.SetWramByte(address + 1, 1);
            return false;
        }
        // findUnusedIndexInLoadedObjectGfx searches cyclically from $cc06,
        // retaining the successful slot as its cursor.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            byte slot = memory.ReadWramByte(Cursor);
            if (slot >= 8) throw new InvalidOperationException("Invalid wLoadedObjectGfxIndex at $cc06.");
            int address = Headers + slot * 2;
            if (memory.ReadWramByte(address + 1) == 0)
            {
                memory.SetWramByte(address, header);
                memory.SetWramByte(address + 1, 1);
                return true;
            }
            memory.SetWramByte(Cursor, (byte)((slot + 1) & 7));
        }
        throw new InvalidOperationException(
            $"loadGraphics.s:addIndexToLoadedObjectGfx has no unused slot for header ${header:x2}; " +
            "this object graphics residency path needs an explicit refresh.");
    }
}

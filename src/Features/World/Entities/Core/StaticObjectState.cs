using System;
using System.Collections.Generic;

namespace Oracles;

// staticObjects.s and bank0.s: buffer lifetime is independent of actor lifetime.
internal static class StaticObjectState
{
    internal static StaticDungeonObjectDatabase Data(OracleRuntimeState memory) =>
        StaticDungeonObjectDatabase.For(OracleAssetPack.ForGame(memory.Game));

    internal static void Clear(OracleRuntimeState memory)
    {
        var data = Data(memory);
        for (int offset = 0; offset < data.Slots * 8; offset++) memory.SetWramByte(data.Start + offset, 0);
    }

    internal static void LoadDungeon(OracleRuntimeState memory, int dungeon)
    {
        var records = Data(memory).Records(dungeon);
        Clear(memory);
        foreach (var record in records) Write(memory, record);
    }

    internal static void Write(OracleRuntimeState memory, StaticObjectRecord record)
    {
        int at = Address(memory, record.Slot);
        byte[] bytes = [checked((byte)record.Type), checked((byte)record.Room), checked((byte)record.Id),
            checked((byte)record.SubId), checked((byte)record.Y), checked((byte)record.X)];
        for (int index = 0; index < bytes.Length; index++) memory.SetWramByte(at + index, bytes[index]);
    }

    internal static int Address(OracleRuntimeState memory, int slot)
    {
        var data = Data(memory);
        if (slot < 0 || slot >= data.Slots) throw new ArgumentOutOfRangeException(nameof(slot));
        return data.Start + slot * 8;
    }

    internal static void DeleteRelated(OracleRuntimeState memory, int actorAddress)
    {
        int pointer = memory.ReadWramByte(actorAddress + 0x16) | memory.ReadWramByte(actorAddress + 0x17) << 8;
        if (pointer == 0) return;
        var data = Data(memory);
        if (pointer < data.Start || pointer >= data.Start + data.Slots * 8 || (pointer & 7) != 0)
            throw new NotSupportedException($"objectDeleteRelatedObj1AsStaticObject: actor${actorAddress:x4}, unsupported related pointer${pointer:x4}.");
        memory.SetWramByte(actorAddress + 0x16, 0); memory.SetWramByte(actorAddress + 0x17, 0);
        for (int offset = 0; offset < 8; offset++) memory.SetWramByte(pointer + offset, 0);
    }

    internal static bool SaveActor(OracleRuntimeState memory, int actorAddress, int type, int room)
    {
        var data = Data(memory);
        for (int slot = 0; slot < data.Slots; slot++)
        {
            int at = data.Start + slot * 8;
            if (memory.ReadWramByte(at) != 0) continue;
            Write(memory, new(slot, type, room, memory.ReadWramByte(actorAddress + 1), memory.ReadWramByte(actorAddress + 2),
                memory.ReadWramByte(actorAddress + 0x0b), memory.ReadWramByte(actorAddress + 0x0d), "bank0.s:objectSaveAsStaticObject"));
            memory.SetWramByte(actorAddress + 0x16, (byte)(at & 0xff)); memory.SetWramByte(actorAddress + 0x17, (byte)(at >> 8));
            return true; // The final two static bytes retain their previous values.
        }
        return false;
    }

    internal static IEnumerable<StaticObjectRecord> InRoom(OracleRuntimeState memory, int room)
    {
        var data = Data(memory);
        int low = data.Start & 0xff;
        for (int visited = 0; visited < 32; visited++)
        {
            int at = (data.Start & 0xff00) | low, type = memory.ReadWramByte(at);
            int slot = (at - data.Start) / 8;
            if (type == 0 || (type & 0x80) != 0 || memory.ReadWramByte(at + 1) != room)
            {
                low = (low + 8) & 0xff;
                if (data.Slots == 16 ? low == 0 : low >= (data.Start & 0xff) + data.Slots * 8) yield break;
                continue;
            }
            if (type is < 1 or > 5) throw new NotSupportedException($"staticObjects.parseStaticObjects: slot${slot:x2} at${at:x4}, type${type:x2} is unsupported.");
            if (type >= 3)
            {
                if (slot < 0 || slot >= data.Slots)
                    throw new NotSupportedException($"staticObjects.parseStaticObjects: native @end continued beyond wStaticObjects to matching actor data at${at:x4}; this raw alias has no construction binding.");
                yield return new(slot, type, room, memory.ReadWramByte(at + 2), memory.ReadWramByte(at + 3),
                    memory.ReadWramByte(at + 4), memory.ReadWramByte(at + 5), $"staticObjects.parseStaticObjects:wStaticObjects+${slot * 8:x2}");
            }
            // Native @end bypasses @noMatch's boundary test, including the final slot.
            low = (low + 8) & 0xff;
        }
        throw new NotSupportedException("staticObjects.parseStaticObjects: native matching records cycle around the entire $cd page.");
    }
}

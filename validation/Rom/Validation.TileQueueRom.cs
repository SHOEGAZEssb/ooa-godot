using Godot;
using System.Collections.Generic;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateChangedTileQueueRom()
    {
        var rom = new TileQueueRom();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var head = typeof(ChangedTileQueue).GetField("_head", flags)!;
        var tail = typeof(ChangedTileQueue).GetField("_tail", flags)!;
        for (int mode = 0; mode < 256; mode++)
        for (int count = 0; count < 32; count++)
        {
            int cursor = mode & 31;
            rom.Reset(cursor);
            var queue = new ChangedTileQueue();
            head.SetValue(queue, cursor); tail.SetValue(queue, cursor);
            var accepted = new List<ChangedTileWrite>();
            var drawn = new List<ChangedTileWrite>();
            for (int i = 0; i < count; i++)
            {
                byte position = (byte)(i % 3 == 0 ? 0x11 : (i % 11) * 16 + i % 15);
                byte tile = (byte)(mode + i);
                FailIf(rom.SetTile(position, tile) != queue.TryWrite(position, tile, accepted.Add), "ROM setTile acceptance differs.");
                FailIf(rom[0xcf00 + position] != tile || rom[0xce00 + position] != (byte)(tile ^ 0x5a) || rom.Bank(3, 0xdf00 + position) != 0,
                    "Native setTile must update layout/collision without touching the underlying layout.");
            }
            rom.Drain((byte)mode); queue.UpdateGraphics((byte)mode, drawn.Add);
            Compare();
            // Fill after the drain to exercise wrapped allocation and rejection.
            for (int i = 0; i < 33; i++)
            {
                byte tile = (byte)(0x80 + i);
                int before = accepted.Count;
                bool native = rom.SetTile(0x11, tile);
                FailIf(native != queue.TryWrite(0x11, tile, accepted.Add), "ROM wrapped/full queue acceptance differs.");
                byte logicalTile = accepted.FindLast(write => write.Position == 0x11).Tile;
                FailIf(accepted.Count != before + (native ? 1 : 0) || rom[0xcf11] != logicalTile ||
                    rom[0xce11] != (byte)(logicalTile ^ 0x5a) || rom.Bank(3, 0xdf11) != 0,
                    "Rejected queue writes must preserve terrain and invoke no logical update callback.");
            }
            while (queue.Count != 0)
            {
                drawn.Clear(); rom.Drain(1); queue.UpdateGraphics(1, drawn.Add); Compare();
            }
            void Compare()
            {
                FailIf(queue.Count != rom.Count || (int)head.GetValue(queue)! != rom[0xccdf] || (int)tail.GetValue(queue)! != rom[0xcce0] ||
                    rom[0xffa5] != drawn.Count * 13, $"ROM queue cursors/budget differ for mode=${mode:x2}, count={count}.");
                for (int entry = 0; entry < drawn.Count; entry++)
                {
                    var write = drawn[entry]; int command = 0xc400 + entry * 13;
                    int address = 0x9800 + (write.Position >> 4) * 64 + (write.Position & 15) * 2;
                    FailIf((rom[command + 1] | rom[command + 2] << 8) != address,
                        "Native VBlank commands did not retain the runtime drain's position/order.");
                    int[] offsets = [3, 4, 6, 7, 8, 9, 11, 12];
                    for (int i = 0; i < 8; i++)
                        FailIf(rom[command + offsets[i]] != (byte)(write.Tile + i * 17),
                            "Native VBlank command mapping differs from the ordered graphics write.");
                }
            }
        }
        GD.Print("Validated ROM tile queue across all 256 scroll masks, 32 occupancies, all cursor rotations, wrap, overflow and ordered VBlank mapping commands.");
    }
}

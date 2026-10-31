using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedLinkAnimationRom()
    {
        int comparisons = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var memory = new OracleRuntimeState(game);
            var animations = LinkNativeAnimationDatabase.For(OracleAssetPack.ForGame(game));
            var native = new LinkCollisionRom(game: game);
            int set = game == OracleGame.Ages ? 0x2b0a : 0x2a51;
            int animate = set - 0x1b;
            void Check(string context)
            {
                foreach (int address in new[] { 0xd020, 0xd021, 0xd022, 0xd023, 0xd030, 0xd031 })
                    FailIf(memory.ReadWramByte(address) != native[address],
                        $"{game}/{context}: native animation ${address:x4}: runtime${memory.ReadWramByte(address):x2}, ROM${native[address]:x2}.");
                comparisons++;
            }
            for (int mode = 0; mode < 0x38; mode++)
            {
                animations.Set(memory, mode); native.Call(set, bank: 0, accumulator: mode); Check($"set${mode:x2}");
                for (int update = 1; update <= 640; update++)
                {
                    animations.Animate(memory); native.Call(animate, bank: 0); Check($"mode${mode:x2}/update{update}");
                }
                // SetAnimation reloads even the same mode, discarding the live cursor/counter.
                animations.Set(memory, mode); native.Call(set, bank: 0, accumulator: mode); Check($"reset${mode:x2}");
            }
            animations.Set(memory, 0x10); native.Call(set, bank: 0, accumulator: 0x10);
            memory.SetWramByte(0xd020, 0); native[0xd020] = 0;
            memory.SetWramByte(0xd030, 0x06); native[0xd030] = 0x06;
            for (int update = 0; update < 258; update++)
            {
                animations.Animate(memory); native.Call(animate, bank: 0); Check($"raw counter/mode/update{update}");
            }
            int cursor = memory.ReadWramByte(0xd022) | memory.ReadWramByte(0xd023) << 8;
            memory.SetWramByte(0xd020, 1); native[0xd020] = 1;
            memory.SetWramByte(0xd022, (byte)(cursor - 3)); native[0xd022] = (byte)(cursor - 3);
            memory.SetWramByte(0xd023, (byte)((cursor - 3) >> 8)); native[0xd023] = (byte)((cursor - 3) >> 8);
            animations.Animate(memory); native.Call(animate, bank: 0); Check("raw cursor rewind");
            byte before = memory.ReadWramByte(0xd030);
            try { animations.Set(memory, 0x38); throw new Exception("Invalid native Link mode was accepted."); }
            catch (NotSupportedException) { }
            FailIf(memory.ReadWramByte(0xd030) != before, "Unsupported Link mode must fail before native state mutation.");
            FailIf(native.RandomUpdates != 0, "Native Link animation must not consume RNG.");
        }
        GD.Print($"Validated {comparisons} both-ROM Link animation states: all $38 modes, long-frame fallthrough, signed loops, repeated initialization and live cursor/counter/mode aliases.");
    }
}

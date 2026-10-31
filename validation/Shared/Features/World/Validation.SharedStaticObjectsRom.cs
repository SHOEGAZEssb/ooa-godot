using System;
using System.Linq;
using Godot;
using System.IO;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedStaticObjectsRom()
    {
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var memory = new OracleRuntimeState(game);
            var data = StaticObjectState.Data(memory);
            var native = new LinkCollisionRom(game: game);
            int slots = ages ? 8 : 16, end = 0xcd80 + slots * 8;
            int[] counts = ages ? [0,0,3,0,4,0,0,0,2,0,0,1,0,0,0,0] : [0,1,0,0,4,7,4,1,4,0,0,1];
            FailIf(data.Start != 0xcd80 || data.Slots != slots || data.Dungeons != counts.Length,
                $"{game}: native static buffer capacity or pointer count differs.");
            void Compare(string phase)
            {
                for (int at = 0xcd7f; at <= 0xce00; at++)
                    FailIf(memory.ReadWramByte(at) != native[at], $"{game}: {phase}, static byte${at:x4} differs.");
                for (int at = 0xd100; at < 0xd140; at++)
                    FailIf(memory.ReadWramByte(at) != native[at], $"{game}: {phase} changed independent D1 byte${at:x4}.");
            }
            for (int dungeon = 0; dungeon < counts.Length; dungeon++)
            {
                for (int at = 0xcd7f; at <= 0xce00; at++) memory.SetWramByte(at, native[at] = (byte)(at * 7));
                for (int at = 0xd100; at < 0xd140; at++) memory.SetWramByte(at, native[at] = (byte)(at * 11));
                native[ages ? 0xcc39 : 0xcc55] = (byte)dungeon;
                native.Call(ages ? 0x5085 : 0x4ea4, bank: ages ? 0x16 : 0x15);
                StaticObjectState.LoadDungeon(memory, dungeon);
                Compare($"load dungeon${dungeon:x2}");
                FailIf(data.Records(dungeon).Count != counts[dungeon], $"{game}: source static list${dungeon:x2} count differs.");
                for (int slot = 0; slot < slots; slot++)
                    FailIf(memory.ReadWramByte(0xcd80 + slot * 8 + 6) != 0 || memory.ReadWramByte(0xcd80 + slot * 8 + 7) != 0,
                        $"{game}: dungeon loading must clear both trailing bytes in every native static slot.");
            }
            for (int free = 0; free <= slots; free++)
            foreach (int type in new[] { 3, 4, 5 })
            {
                int actor = type == 3 ? 0xd240 : type == 4 ? 0xd280 : 0xd2c0;
                for (int at = 0xcd7f; at <= 0xce00; at++) memory.SetWramByte(at, native[at] = (byte)(at * 13));
                for (int slot = 0; slot < slots; slot++) memory.SetWramByte(0xcd80 + slot * 8, native[0xcd80 + slot * 8] = 3);
                if (free < slots) memory.SetWramByte(0xcd80 + free * 8, native[0xcd80 + free * 8] = 0);
                for (int offset = 0; offset < 64; offset++) memory.SetWramByte(actor + offset, native[actor + offset] = (byte)(offset * 3));
                native[ages ? 0xffae : 0xffac] = (byte)(actor & 0xff);
                native[ages ? 0xcc30 : 0xcc4c] = 0x5a;
                int find = ages ? 0x31a7 : 0x307e, save = ages ? 0x31cf : 0x30a3;
                byte[] caller = [0xcd,(byte)find,(byte)(find >> 8),0xc0,0x3e,(byte)type,0xc3,(byte)save,(byte)(save >> 8)];
                for (int index = 0; index < caller.Length; index++) native[0xc300 + index] = caller[index];
                native.Call(0xc300, bank: 0, objectPage: 0xd2);
                bool saved = StaticObjectState.SaveActor(memory, actor, type, 0x5a);
                FailIf(saved != (free < slots), $"{game}: native static allocation at slot${free:x2} differs.");
                Compare($"save type${type:x2}/slot${free:x2}");
                for (int offset = 0; offset < 64; offset++)
                    FailIf(memory.ReadWramByte(actor + offset) != native[actor + offset], $"{game}: static saving actor byte${actor + offset:x4} differs.");
                if (!saved) continue;
                native.Call(ages ? 0x31b8 : 0x308c, bank: 0, objectPage: 0xd2);
                StaticObjectState.DeleteRelated(memory, actor);
                Compare($"delete type${type:x2}/slot${free:x2}");
                FailIf(memory.ReadWramByte(actor + 0x16) != 0 || memory.ReadWramByte(actor + 0x17) != 0 ||
                    native[actor + 0x16] != 0 || native[actor + 0x17] != 0,
                    $"{game}: deleting a related static object must unlink the actor.");
            }
            StaticObjectState.Clear(memory); native.Call(ages ? 0x319f : 0x3076, bank: 0);
            Compare("clear");
            for (int slot = 0; slot < slots; slot++)
            {
                int type = slot % 6;
                StaticObjectState.Write(memory, new(slot, type == 0 ? 0x83 : type, slot == 1 ? 0x5b : 0x5a,
                    0x96, slot, 24 + slot, 40 + slot, "native parser fixture"));
            }
            if (!ages) memory.SetWramByte(end - 8, 0x83);
            int[] admitted = StaticObjectState.InRoom(memory, 0x5a).Select(record => record.Slot).ToArray();
            int[] expected = Enumerable.Range(0, slots).Where(slot => slot % 6 >= 3 && (ages || slot != slots - 1)).ToArray();
            FailIf(!admitted.SequenceEqual(expected), $"{game}: static parsing must preserve ordered room/type/high-bit admission, including the upper eight Seasons slots.");
            // @end bypasses the boundary test: the native final matching record
            // can expose $cdc0 (Ages) or byte-wrapped $cd00 (Seasons).
            for (int at = 0xcd00; at < 0xce00; at++) memory.SetWramByte(at, native[at] = 0);
            for (int at = 0xd000; at < 0xe000; at++) native[at] = 0;
            int last = end - 8, alias = ages ? end : 0xcd00;
            foreach (int at in new[] { last, alias })
            {
                byte[] bytes = [3,0x5a,(byte)(at == last ? 0x96 : 0x7e),0x5a,24,40];
                for (int index = 0; index < bytes.Length; index++) memory.SetWramByte(at + index, native[at + index] = bytes[index]);
            }
            native[ages ? 0xcc30 : 0xcc4c] = 0x5a;
            bool nativeCycled = false;
            try { native.Call(ages ? 0x5015 : 0x4e35, bank: ages ? 0x16 : 0x15); }
            catch (InvalidDataException error) when (!ages && error.Message.Contains("exceeded 100000 instructions")) { nativeCycled = true; }
            FailIf(nativeCycled == ages, $"{game}: Seasons' matching final record must cycle, while Ages returns after its out-of-buffer alias.");
            FailIf(native[0xd240] != 3 || native[0xd241] != 0x16 || native[0xd340] != 1 || native[0xd341] != 0x7e,
                $"{game}: the executed native parser must retain enabled-ID bit7 and the final-slot raw alias.");
            bool rejected = false;
            try { _ = StaticObjectState.InRoom(memory, 0x5a).ToArray(); }
            catch (NotSupportedException error) { rejected = error.Message.Contains($"at${alias:x4}"); }
            FailIf(!rejected, $"{game}: an unbound native static alias must diagnose instead of silently ending at capacity.");
            FailIf(native.RandomUpdates != 0, "Native static load/save/delete must not consume RNG.");
        }
        GD.Print("Validated both-ROM static table loads, complete buffer spans, independent D1 lifetime, every free/full slot, all object categories, trailing-byte retention and actor unlinking.");
    }
}

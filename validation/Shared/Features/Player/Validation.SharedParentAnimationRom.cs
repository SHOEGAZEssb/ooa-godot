using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedParentAnimationRom()
    {
        int cases = 0;
        foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var memory = new OracleRuntimeState(game);
            var native = new LinkCollisionRom(game: game);
            native[0xd001] = 0; native[0xd030] = 0; native[0xd031] = 0x2a;
            native[0xd034] = 0x55;
            for (int profile = 0; profile < 7; profile++)
            for (int self = 0; self < 256; self++)
            for (int priority = 0; priority < 256; priority++)
            {
                memory.SetWramByte(0xd03f, (byte)self); native[0xd03f] = (byte)self;
                for (int slot = 0; slot < 4; slot++)
                {
                    int value = profile < 4 ? slot == profile ? priority : 0 : profile == 4 ? priority :
                        profile == 5 ? (slot & 1) == 0 ? priority : 0 : slot == 0 ? 255 : slot == 3 ? 0 : priority;
                    int address = 0xd200 + slot * 256;
                    memory.SetWramByte(address | 0x3f, (byte)value);
                    native[address | 0x3f] = (byte)value;
                    native[address] = (byte)((self + priority + slot) & 3);
                    native[address | 0x31] = (byte)(0x60 + slot);
                }
                native.Call(game == OracleGame.Ages ? 0x4553 : 0x4549, bank: 6, captureB: true);
                int page = ParentItemAnimationState.SelectPage(memory);
                int expectedGraphic = page == 0xd0 ? 0x2a : 0x60 + page - 0xd2;
                FailIf(native[0xc202] != expectedGraphic || native[0xd034] != 0x55,
                    $"{game}: func_4553 profile{profile}, self${self:x2}, priority${priority:x2}: selected page${page:x2}/graphic${expectedGraphic:x2}, native B${native[0xc202]:x2}; var34 must retain $55 outside WALK.");
                cases++;
            }
            FailIf(native.RandomUpdates != 0, "Native parent animation selection must not consume game RNG.");
        }
        GD.Print($"Validated {cases} both-ROM parent selections: every self/parent priority byte, all four slots, later-slot ties, self ties, and enabled-independent claims.");
    }
}

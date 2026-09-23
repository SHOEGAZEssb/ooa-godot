using Godot;
using System;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogFireTimer()
    {
        var data = new SmogFireTimerDatabase();
        // First two rows come from clean-US $0f:$7520/$7643 after the
        // unsigned $fe/$ff table index. Remaining rows are smog.s named data.
        int[][] expected = [
            [0x3a,0x12,0x1e,0xa9], [0xd7,0x1e,0xb1,0x2a],
            [0x78,0xf0,0xff,0xff], [0x78,0x78,0xb4,0xf0], [0x50,0x50,0x64,0x78], [0x32,0x32,0x3c,0x50],
            [0,0,0,0], [0x50,0x78,0x96,0xb4], [0x32,0x50,0x96,0xb4], [0x32,0x50,0x64,0x96],
            [0x5a,0x78,0x64,0x96], [0x1e,0x28,0x32,0x3c], [0x14,0x1e,0x32,0x3c], [0x14,0x1e,0x28,0x32]
        ];
        for (int row = 0; row < expected.Length; row++)
        for (int high = 0; high <= 0x80; high += 0x80)
        for (int random = 0; random < 256; random++)
        {
            int subid = (row < 2 ? row : 2 + (row - 2) / 4) | high;
            int phase = row < 2 ? 0 : (row - 2) % 4;
            int wanted = expected[row][random & 3];
            FailIf(data.Select(subid, phase, random) != wanted,
                $"Smog subid${subid:x2}, phase{phase}, RNG${random:x2} must preserve clean-US timer byte${wanted:x2}.");
        }
        foreach (int row in new[] { 0, 1, 2, 6, 10, 13 })
        {
            int subid = row < 2 ? row : 2 + (row - 2) / 4;
            int phase = row < 2 ? 0 : (row - 2) % 4;
            for (int choice = 0; choice < 4; choice++)
            {
                int calls = 0, duration = expected[row][choice];
                var timer = new SmogFireTimer(data);
                timer.Reset(subid, phase, () => { calls++; return choice; });
                FailIf(calls != 1 || timer.Remaining != duration, "Smog reset must consume exactly one RNG draw, even for intro/zero timers.");
                for (int tick = 1; tick <= duration + 2; tick++)
                {
                    bool fire = timer.Advance();
                    FailIf(fire != (duration != 0 && tick == duration) || timer.Remaining != Math.Max(0,duration-tick) || calls != 1,
                        "Smog fire request must occur only on the 1->0 update, without another random draw or zero underflow.");
                }
            }
        }
        bool rejected = false;
        try { data.Select(5,0,0); }
        catch (NotSupportedException error) { rejected = error.Message.Contains("smog_setCounterToFireProjectile"); }
        FailIf(!rejected, "Smog unsupported timer dispatch must retain source-aware diagnostics.");
        GD.Print("Validated clean-US Smog intro out-of-table reads, phase timers, RNG masking/consumption and exact countdown boundaries.");
    }
}

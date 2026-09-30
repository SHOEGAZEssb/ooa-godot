using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateCpuDecimalArithmetic()
    {
        var memory = new byte[0x10000];
        var cpu = new OracleCpu(a => memory[a], (a, v) => memory[a] = (byte)v, static _ => { },
            (pc, detail) => new InvalidOperationException($"BCD CPU ${pc:x4}: {detail}"));
        for (int a = 0; a < 100; a++)
        for (int b = 0; b < 100; b++)
        for (int carry = 0; carry < 2; carry++)
        foreach (bool subtract in new[] { false, true })
        {
            byte[] program = [0xaf, (byte)(carry == 1 ? 0x37 : 0x00),
                0x3e, (byte)(a / 10 * 16 + a % 10),
                (byte)(subtract ? 0xde : 0xce), (byte)(b / 10 * 16 + b % 10),
                0x27, 0xf5, 0xc1, 0x78, 0xea, 0x00, 0xc2, 0x79, 0xea, 0x01, 0xc2, 0xc9];
            program.CopyTo(memory, 0xc100);
            cpu.RunCall(0xc100);
            // Independent base-ten oracle for valid packed-BCD operands.
            int value = subtract ? a - b - carry : a + b + carry;
            int wrapped = (value + 100) % 100;
            int expectedFlags = (subtract ? 0x40 : 0) | (wrapped == 0 ? 0x80 : 0) |
                (value is < 0 or >= 100 ? 0x10 : 0);
            FailIf(memory[0xc200] != wrapped / 10 * 16 + wrapped % 10 || memory[0xc201] != expectedFlags,
                $"DAA decimal arithmetic a={a}, b={b}, carry={carry}, subtract={subtract}.");
        }
        GD.Print("Validated 40000 ADC/SBC+DAA cases against decimal arithmetic, including carries, borrows and Z/N/H/C flags.");
    }
}

using Godot;
using System;
using System.IO;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOracleRandomRom()
    {
        byte[] rom = ValidationRom.LoadCleanUs();
        const int entry = 0x0453;
        // code/bank0.s:getRandomNumber_noPreserveVars, clean US $00:$0453.
        byte[] signature = [0xf0, 0x94, 0x6f, 0x4f, 0xf0, 0x95, 0x67, 0x47,
            0x29, 0x09, 0x7c, 0xe0, 0x95, 0x81, 0xe0, 0x94, 0xc9];
        FailIf(!rom.AsSpan(entry, signature.Length).SequenceEqual(signature),
            "getRandomNumber_noPreserveVars changed at clean-US $00:$0453.");

        // A validation-owned caller records the routine's returned A/H/L at
        // $c200-$c202. It performs no RNG arithmetic and leaves the seed bytes
        // untouched. This observes registers without runtime test hooks or
        // reflection into the instruction interpreter.
        const int caller = 0xc100, result = 0xc200, rng1 = 0xff94, rng2 = 0xff95;
        byte[] capture = [0xcd, 0x53, 0x04, // call $0453
            0xea, 0x00, 0xc2,             // ld ($c200),a
            0x7c, 0xea, 0x01, 0xc2,       // ld a,h; ld ($c201),a
            0x7d, 0xea, 0x02, 0xc2,       // ld a,l; ld ($c202),a
            0xc9];                        // ret
        byte[] memory = new byte[0x10000];
        int Read(int address)
        {
            if (address >= entry && address < entry + signature.Length) return rom[address];
            if (address >= caller && address < caller + capture.Length) return capture[address - caller];
            if (address is rng1 or rng2 or >= 0xdfee and <= 0xdff1) return memory[address];
            throw new InvalidDataException($"RNG reference read outside its contract at ${address:x4}.");
        }
        int resultWrites = 0;
        void Write(int address, int value)
        {
            if (address is >= result and <= result + 2) resultWrites |= 1 << (address - result);
            else if (address is not (rng1 or rng2 or >= 0xdfee and <= 0xdff1))
                throw new InvalidDataException($"RNG reference write outside its contract at ${address:x4}.");
            memory[address] = (byte)value;
        }
        var cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"RNG reference at ${pc:x4}: {detail}."));
        var random = new OracleRandom();
        OracleRandomState initial = random.CaptureState();
        for (int seed = 0; seed <= 0xffff; seed++)
        {
            byte low = (byte)seed, high = (byte)(seed >> 8);
            memory[rng1] = low;
            memory[rng2] = high;
            resultWrites = 0;
            random.RestoreState(initial with { Rng1 = low, Rng2 = high });
            cpu.RunCall(caller); // Bounded instruction execution; no hardware clock drives gameplay.
            OracleRandomResult actual = random.Next();
            OracleRandomState after = random.CaptureState();
            var expected = new OracleRandomResult(memory[result], memory[result + 1], memory[result + 2]);
            FailIf(resultWrites != 7 || actual != expected ||
                after.Rng1 != memory[rng1] || after.Rng2 != memory[rng2] ||
                after.Calls != 1 || after.LastResult != actual,
                $"getRandomNumber_noPreserveVars $00:$0453 seed=${seed:x4}: " +
                $"expected A/HL=${expected.Value:x2}/${expected.High:x2}{expected.Low:x2}, " +
                $"HRAM=${memory[rng2]:x2}{memory[rng1]:x2}; " +
                $"actual A/HL=${actual.Value:x2}/${actual.High:x2}{actual.Low:x2}, " +
                $"state=${after.Rng2:x2}{after.Rng1:x2}, calls={after.Calls}.");
        }
        GD.Print("Validated all 65,536 RNG seeds against executed clean-US $00:$0453, including A/HL and both seed bytes.");
    }
}

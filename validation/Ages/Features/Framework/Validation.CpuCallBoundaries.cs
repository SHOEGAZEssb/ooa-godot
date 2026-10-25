using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateCpuCallBoundaries()
    {
        byte[] memory = new byte[0x10000];
        int clocks = 0;
        var reads = new List<int>();
        var boundaries = new List<int>();
        var cpu = new OracleCpu(address => { reads.Add(address); return memory[address]; },
            (address, value) => memory[address] = (byte)value, cycles => clocks += cycles,
            (pc, detail) => new InvalidOperationException($"Boundary CPU ${pc:x4}: {detail}"));
        // LD A,$42; LD ($c200),A; undeclared opcode. Stop before fetching it.
        byte[] program = [0x3e, 0x42, 0xea, 0x00, 0xc2, 0xd3];
        program.CopyTo(memory, 0xc100);
        cpu.RunCallUntil(0xc100, address => { boundaries.Add(address); return address == 0xc105; });
        FailIf(memory[0xc200] != 0x42 || clocks != 24 || cpu.InstructionAddress != 0xc105 ||
            reads.Contains(0xc105) || !boundaries.SequenceEqual(new[] { 0xc100, 0xc102, 0xc105 }),
            "CPU boundary $c105 must preserve prior writes and clocks, without fetching the next opcode or stopping on operands.");

        reads.Clear(); clocks = 0; memory[0xc200] = 0;
        cpu.RunCallUntil(0xc100, static _ => true, instructionLimit: 1);
        FailIf(reads.Count != 0 || clocks != 0 || memory[0xc200] != 0 || cpu.InstructionAddress != 0xc100,
            "A CPU boundary at entry must execute no instruction.");

        memory[0xc105] = 0xc9; // RET. An operand address is not an instruction boundary.
        cpu.RunCallUntil(0xc100, static address => address == 0xc101);
        int boundedClocks = clocks;
        int[] boundedReads = reads.ToArray();
        reads.Clear(); clocks = 0;
        cpu.RunCall(0xc100);
        FailIf(clocks != 40 || boundedClocks != clocks || !reads.SequenceEqual(boundedReads),
            "Ordinary return and a bounded call without a matching instruction must retain identical bus accesses and cycles.");

        // JP $c100. A nonmatching boundary cannot bypass the instruction limit.
        memory[0xc100] = 0xc3; memory[0xc101] = 0x00; memory[0xc102] = 0xc1;
        bool limited = false;
        try { cpu.RunCallUntil(0xc100, static _ => false, instructionLimit: 2); }
        catch (InvalidOperationException exception)
        { limited = exception.Message.Contains("$c100") && exception.Message.Contains("exceeded 2 instructions"); }
        FailIf(!limited, "Bounded CPU calls must retain source-aware instruction-limit failures.");
    }
}

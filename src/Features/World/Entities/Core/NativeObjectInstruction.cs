namespace Oracles;

internal sealed record NativeObjectInstruction(int Address, int Next, int Opcode, bool Header,
    int Flags, int Id, int SubId, int Y, int X, int Position, int Var03, int Target, int Mask, string Source);

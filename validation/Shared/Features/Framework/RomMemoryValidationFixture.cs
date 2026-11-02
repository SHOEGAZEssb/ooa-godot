namespace Oracles;

internal sealed class RomMemoryValidationFixture(OracleGame game) : RomFixture("memory policy", 4, game)
{
    internal int Peek(int address) => Read(address);
    internal void Poke(int address, int value) => Write(address, value);
    internal void SetCaller(byte[] bytes) => _caller = bytes;
    protected override bool Allowed(int address) => address == 0xc200;
    protected override bool RomBankAllowed(int bank) => bank == 4;
}

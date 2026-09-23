namespace oracleofages;

// Shared wcc93: doorController owns the count, boss initialization sets bit7.
internal sealed class BossShutterSignal
{
    internal byte Value { get; private set; }
    internal void Clear() => Value = 0;
    internal void BeginBossEntry() => Value |= 0x80;
    internal void Opened() => Value = unchecked((byte)(Value + 1));
    internal void Closed()
    {
        if (Value == 0) return;
        Value--;
        if ((Value & 0x7f) == 0) Value &= 0x7f;
    }
}

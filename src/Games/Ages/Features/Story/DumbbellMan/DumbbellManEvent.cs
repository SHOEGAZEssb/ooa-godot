namespace Oracles;

internal sealed class DumbbellManEvent : TradeNpcEvent
{
    internal DumbbellManEventDatabase Database { get; }
    public DumbbellManEvent(RoomEventContext context) : this(context, new DumbbellManEventDatabase()) { }
    private DumbbellManEvent(RoomEventContext context, DumbbellManEventDatabase database)
        : base(context, "DumbbellMan", database.Record, database.Commands, 0x0b21) => Database = database;
    protected override bool AcceptText(int textId) => textId is >= 0x0b1d and <= 0x0b24;
}

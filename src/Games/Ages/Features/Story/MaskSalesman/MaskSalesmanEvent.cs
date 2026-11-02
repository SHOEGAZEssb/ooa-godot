namespace Oracles;

internal sealed class MaskSalesmanEvent : TradeNpcEvent
{
    internal MaskSalesmanEventDatabase Database { get; }
    public MaskSalesmanEvent(RoomEventContext context) : this(context, new MaskSalesmanEventDatabase()) { }
    private MaskSalesmanEvent(RoomEventContext context, MaskSalesmanEventDatabase database)
        : base(context, "MaskSalesman", database.Record, database.Commands, 0x0b10) => Database = database;
    protected override bool AcceptText(int textId) => textId == 0x0b45 || textId is >= 0x0b0d and <= 0x0b15;
    public override void ShowText(int textId, string message, int? textboxPosition) =>
        ShowTradeText(textId, message, textboxPosition);
}

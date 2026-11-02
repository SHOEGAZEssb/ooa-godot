
namespace Oracles;

internal readonly record struct TradeNpcEventRecord(
    int Group, int Room, int InteractionId, int SubId, string Animation0, string Animation1,
    int InitialAnimation, int CollisionRadiusY, int CollisionRadiusX, int RoomFlag,
    int RequiredTradeItem, int RewardTreasure, int RewardParameter, string RewardObject,
    int InitialScriptUpdates, bool AlwaysUpdate)
{
    internal static TradeNpcEventRecord Load(string file, string schemaName)
    {
        var row = GeneratedTable.Load(AgesGameContent.AssetRoot + $"cutscenes/{file}_event.tsv",
            new GeneratedTableSchema(schemaName, GeneratedTableKeySemantics.Ordered,
                ["group", "room", "id", "subid", "animation0", "animation1", "initial-animation",
                 "collision-y", "collision-x", "room-flag", "required-trade", "reward-treasure",
                 "reward-parameter", "reward-object", "initial-script-updates", "always-update"],
                headerRequired: true)).SingleRow();
        return new(row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.RequiredString(4), row.RequiredString(5), row.HexByte(6), row.HexByte(7),
            row.HexByte(8), row.HexByte(9), row.HexByte(10), row.HexByte(11), row.HexByte(12),
            row.RequiredString(13), row.UnsignedDecimal(14), row.Boolean01(15));
    }

    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

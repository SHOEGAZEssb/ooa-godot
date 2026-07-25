using System;

namespace oracleofages;

/// <summary>
/// Typed INTERAC_ERA_OR_SEASON_INFO $e0 visuals, motion, and full-room-load
/// predicates imported from interactionData.s, eraOrSeasonInfo.s, and the
/// shared tileset/global-state definitions.
/// </summary>
internal sealed class EraInfoDatabase
{
    private readonly EraInfoDatabaseRecord[] _records =
        new EraInfoDatabaseRecord[2];

    internal EraInfoDatabaseRecord Present => _records[0];
    internal EraInfoDatabaseRecord Past => _records[1];

    public EraInfoDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/era_info.tsv",
            new GeneratedTableSchema(
                "INTERAC_ERA_OR_SEASON_INFO records",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "sprite", "tile-base", "palette", "animation",
                    "start-y", "start-x", "enter-step", "target-x",
                    "hold-updates", "exit-step", "exit-updates",
                    "outdoors-mask", "large-indoors-mask", "past-mask",
                    "suppress-global-flag", "sent-back-address",
                    "sent-back-value", "source"
                ],
                ["subid"],
                headerRequired: true));

        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int subId = row.HexByte(0);
            if (subId >= _records.Length)
                throw row.Invalid(0, "INTERAC_ERA_OR_SEASON_INFO subid 00 or 01");
            _records[subId] = new EraInfoDatabaseRecord(
                subId,
                row.RequiredString(1),
                row.HexByte(2),
                row.Decimal(3, 0, 7),
                row.RequiredString(4),
                row.HexByte(5),
                row.HexByte(6),
                row.UnsignedDecimal(7),
                row.HexByte(8),
                row.UnsignedDecimal(9),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.HexByte(12),
                row.HexByte(13),
                row.HexByte(14),
                row.HexByte(15),
                row.HexWord(16),
                row.HexByte(17),
                row.RequiredString(18));
            count++;
        }

        if (count != 2 ||
            Present is not
            {
                SubId: 0,
                Sprite: "spr_present_past_symbols",
                TileBase: 0x00,
                Palette: 1
            } ||
            Past is not
            {
                SubId: 1,
                Sprite: "spr_present_past_symbols",
                TileBase: 0x08,
                Palette: 3
            } ||
            Present.Animation != Past.Animation ||
            Present.StartY != 0x0a ||
            Present.StartX != 0xb0 ||
            Present.EnterStep != 4 ||
            Present.TargetX != 0x10 ||
            Present.HoldUpdates != 40 ||
            Present.ExitStep != 6 ||
            Present.ExitUpdates != 6 ||
            Present.OutdoorsMask != 0x01 ||
            Present.LargeIndoorsMask != 0x10 ||
            Present.PastMask != 0x80 ||
            Present.SuppressGlobalFlag != 0x16 ||
            Present.SentBackAddress !=
                OracleRuntimeState.SentBackByStrangeForceAddress ||
            Present.SentBackValue != 1 ||
            !SharedContractMatches(Past))
        {
            throw new InvalidOperationException(
                "Imported INTERAC_ERA_OR_SEASON_INFO $e0 contract is incomplete.");
        }
    }

    internal EraInfoDatabaseRecord ForTilesetFlags(byte tilesetFlags) =>
        (tilesetFlags & Present.PastMask) != 0 ? Past : Present;

    private bool SharedContractMatches(EraInfoDatabaseRecord record) =>
        record.Animation == Present.Animation &&
        record.StartY == Present.StartY &&
        record.StartX == Present.StartX &&
        record.EnterStep == Present.EnterStep &&
        record.TargetX == Present.TargetX &&
        record.HoldUpdates == Present.HoldUpdates &&
        record.ExitStep == Present.ExitStep &&
        record.ExitUpdates == Present.ExitUpdates &&
        record.OutdoorsMask == Present.OutdoorsMask &&
        record.LargeIndoorsMask == Present.LargeIndoorsMask &&
        record.PastMask == Present.PastMask &&
        record.SuppressGlobalFlag == Present.SuppressGlobalFlag &&
        record.SentBackAddress == Present.SentBackAddress &&
        record.SentBackValue == Present.SentBackValue;
}

internal readonly record struct EraInfoDatabaseRecord(
    int SubId,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    int StartY,
    int StartX,
    int EnterStep,
    int TargetX,
    int HoldUpdates,
    int ExitStep,
    int ExitUpdates,
    int OutdoorsMask,
    int LargeIndoorsMask,
    int PastMask,
    int SuppressGlobalFlag,
    int SentBackAddress,
    int SentBackValue,
    string Source);

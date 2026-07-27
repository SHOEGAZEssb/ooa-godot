using System;

namespace oracleofages;

/// <summary>
/// Source-defined fallback outcomes for common sign and chest tile handlers.
/// These records are separate from room-specific sign/chest lookup tables.
/// </summary>
internal sealed class TileInteractionFallbackDatabase
{
    internal TileInteractionTextFallback ChestWrongSide { get; }
    internal TileInteractionTextFallback SignWrongSide { get; }
    internal TileInteractionTextFallback SignNoMatch { get; }
    internal TileInteractionChestFallback ChestNoMatch { get; }

    internal TileInteractionFallbackDatabase(TreasureDatabase treasures)
    {
        ArgumentNullException.ThrowIfNull(treasures);
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tile_interaction_fallbacks.tsv",
            new GeneratedTableSchema(
                "common tile-interaction fallbacks",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "text-id", "treasure-object", "treasure-id",
                    "subid", "parameter", "graphic", "amount",
                    "message-base64", "source"
                ],
                ["kind"],
                headerRequired: true));

        TileInteractionTextFallback? chestWrongSide = null;
        TileInteractionTextFallback? signWrongSide = null;
        TileInteractionTextFallback? signNoMatch = null;
        TileInteractionChestFallback? chestNoMatch = null;
        foreach (GeneratedTableRow row in table.Rows)
        {
            string kind = row.RequiredString(0);
            int textId = row.HexWord(1);
            string message = row.Base64Utf8(8);
            string source = row.RequiredString(9);
            if (message.Length == 0)
                throw row.Invalid(8, "a nonempty source dialogue");

            if (kind != "chest-no-match")
            {
                for (int column = 2; column <= 7; column++)
                {
                    if (row.String(column).Length != 0)
                    {
                        throw row.Invalid(
                            column,
                            "an empty treasure field for a dialogue fallback");
                    }
                }

                var record = new TileInteractionTextFallback(
                    textId,
                    message,
                    source);
                switch (kind)
                {
                    case "chest-wrong-side":
                        chestWrongSide = record;
                        break;
                    case "sign-wrong-side":
                        signWrongSide = record;
                        break;
                    case "sign-no-match":
                        signNoMatch = record;
                        break;
                    default:
                        throw row.Invalid(
                            0,
                            "chest-wrong-side, sign-wrong-side, sign-no-match, or chest-no-match");
                }
                continue;
            }

            var fallback = new TileInteractionChestFallback(
                row.RequiredString(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                textId,
                row.HexByte(6),
                row.UnsignedDecimal(7),
                message,
                source);
            TreasureObjectRecord treasure =
                treasures.GetObject(fallback.TreasureObject);
            if (treasure.TreasureId != fallback.TreasureId ||
                treasure.SubId != fallback.SubId ||
                treasure.Parameter != fallback.Parameter ||
                treasure.TextId != fallback.TextId ||
                treasure.Graphic != fallback.Graphic ||
                treasure.Message != fallback.Message)
            {
                throw new InvalidOperationException(
                    $"Common missing-chest fallback {fallback.Source} does not " +
                    $"match generated {fallback.TreasureObject}.");
            }
            chestNoMatch = fallback;
        }

        if (table.Rows.Count != 4 ||
            chestWrongSide is null ||
            signWrongSide is null ||
            signNoMatch is null ||
            chestNoMatch is null)
        {
            throw new InvalidOperationException(
                "Expected exactly four common tile-interaction fallback records.");
        }

        ChestWrongSide = chestWrongSide.Value;
        SignWrongSide = signWrongSide.Value;
        SignNoMatch = signNoMatch.Value;
        ChestNoMatch = chestNoMatch.Value;
    }
}

internal readonly record struct TileInteractionTextFallback(
    int TextId,
    string Message,
    string Source);

internal readonly record struct TileInteractionChestFallback(
    string TreasureObject,
    int TreasureId,
    int SubId,
    int Parameter,
    int TextId,
    int Graphic,
    int Amount,
    string Message,
    string Source)
{
    internal ChestRecord At(int group, int room, int position) => new(
        group,
        room,
        position,
        TreasureObject,
        TreasureId,
        SubId,
        Parameter,
        TextId,
        Graphic,
        Amount,
        Message);
}

using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Source-derived linkedGameNpcScript data for INTERAC_LINKED_GAME_GHINI
/// ($cb) and the five-character secret generator used by \secret1.
/// </summary>
public sealed class LinkedGameGhiniDatabase
{
    private const int GameIdAddress = 0xc600;
    private const int PlaytimeCounterAddress = 0xc622;
    private const int ShortSecretIndexAddress = 0xc6fb;
    private const int SecretType = 3;

    private static readonly byte[] XorCipher =
    [
        0x15, 0x23, 0x2e, 0x04, 0x0d, 0x3f, 0x1a, 0x10,
        0x3a, 0x2f, 0x1e, 0x20, 0x0f, 0x3e, 0x36, 0x37,
        0x09, 0x29, 0x3b, 0x31, 0x02, 0x16, 0x3d, 0x38,
        0x28, 0x13, 0x34, 0x32, 0x01, 0x0b, 0x0a, 0x35,
        0x0e, 0x1b, 0x12, 0x2c, 0x21, 0x2d, 0x25, 0x30,
        0x19, 0x2a, 0x06, 0x39, 0x3c, 0x17, 0x33, 0x18
    ];

    private static readonly string[] SecretSymbols =
    [
        "B", "D", "F", "G", "H", "J", "L", "M",
        "\\spade", "\\heart", "\\diamond", "\\club", "#",
        "N", "Q", "R", "S", "T", "W", "Y", "!",
        "\\circle", "\\triangle", "\\rectangle", "+", "-",
        "b", "d", "f", "g", "h", "j", "m", "$", "*", "/", ":", "~",
        "n", "q", "r", "s", "t", "w", "y", "?", "%", "&", "<", "=", ">",
        "2", "3", "4", "5", "6", "7", "8", "9",
        "\\up", "\\down", "\\left", "\\right", "@"
    ];

    public Record Data { get; }

    public LinkedGameGhiniDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/linked_game_ghini.tsv",
            new GeneratedTableSchema(
                "linked-game Ghini",
                GeneratedTableKeySemantics.Ordered,
                [
                    "secret-index", "short-secret-index", "began-flag",
                    "offer-text-id", "refusal-text-id", "explanation-text-id",
                    "secret-text-id", "final-text-id", "offer-utf8-base64",
                    "refusal-utf8-base64", "explanation-utf8-base64",
                    "secret-utf8-base64", "final-utf8-base64", "source"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Linked-game Ghini data should have one row, got {table.Rows.Count}.");
        GeneratedTableRow row = table.Rows[0];
        Data = new Record(
            row.HexByte(0), row.HexByte(1), row.HexByte(2),
            row.HexWord(3), row.HexWord(4), row.HexWord(5),
            row.HexWord(6), row.HexWord(7),
            row.Base64Utf8(8), row.Base64Utf8(9), row.Base64Utf8(10),
            row.Base64Utf8(11), row.Base64Utf8(12), row.RequiredString(13));
        if (Data.SecretIndex != 0x01 || Data.ShortSecretIndex != 0x21 ||
            Data.BeganFlag != 0x51 || Data.OfferTextId != 0x4d05 ||
            Data.FinalTextId != 0x4d09 ||
            !Data.SecretMessage.Contains("\\secret1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Malformed linked-game Ghini record from {Data.Source}.");
        }
        if (SecretSymbols.Length != 64 || XorCipher.Length != 48)
            throw new InvalidOperationException("Linked-secret symbol/cipher tables are incomplete.");
    }

    internal string GenerateSecret(OracleSaveData save)
    {
        byte[] values = GenerateSecretValues(save);
        var result = new System.Text.StringBuilder();
        foreach (byte value in values)
            result.Append(SecretSymbols[value]);
        return result.ToString();
    }

    internal byte[] GenerateSecretValues(OracleSaveData save)
    {
        EnsureGameId(save);
        int gameIdLow = save.ReadWramByte(GameIdAddress);
        int gameIdHigh = save.ReadWramByte(GameIdAddress + 1) & 0x7f;
        save.WriteWramByte(ShortSecretIndexAddress, (byte)Data.ShortSecretIndex);

        int sum = (gameIdLow + gameIdHigh) & 0xff;
        int swappedHighNibble = (Data.ShortSecretIndex >> 4) & 0x0f;
        int lowBitOffset = (Data.ShortSecretIndex & 1) << 2;
        int cipherIndex = ((sum + swappedHighNibble) ^ lowBitOffset) & 0x07;

        byte[] buffer = new byte[20];
        InsertBits(buffer, cipherIndex, 3);
        InsertBits(buffer, SecretType, 2);
        InsertBits(buffer, gameIdLow, 8);
        InsertBits(buffer, gameIdHigh, 7);
        InsertBits(buffer, Data.ShortSecretIndex, 6);
        InsertBits(buffer, 0, 4);

        int checksum = 0;
        foreach (byte value in buffer)
            checksum += value;
        buffer[^1] |= (byte)(checksum & 0x0f);

        byte[] shortBuffer = new byte[5];
        Array.Copy(buffer, buffer.Length - shortBuffer.Length, shortBuffer, 0, shortBuffer.Length);
        int cipherOffset = ((shortBuffer[0] & 0x38) >> 3) * 4;
        for (int index = 0; index < shortBuffer.Length; index++)
        {
            int cipher = XorCipher[cipherOffset + index];
            if (index == 0)
                cipher &= 0x07;
            shortBuffer[index] ^= (byte)cipher;
        }
        return shortBuffer;
    }

    private static void InsertBits(byte[] buffer, int value, int bitCount)
    {
        int source = value & 0xff;
        for (int bit = 0; bit < bitCount; bit++)
        {
            int carry = source & 1;
            source >>= 1;
            for (int index = buffer.Length - 1; index >= 0; index--)
            {
                int previous = buffer[index];
                buffer[index] = (byte)(((previous << 1) | carry) & 0x3f);
                carry = (previous >> 5) & 1;
            }
        }
    }

    private static void EnsureGameId(OracleSaveData save)
    {
        if (save.ReadWramByte(GameIdAddress) != 0 ||
            save.ReadWramByte(GameIdAddress + 1) != 0)
        {
            return;
        }

        byte low = save.ReadWramByte(PlaytimeCounterAddress);
        byte high = (byte)(save.ReadWramByte(PlaytimeCounterAddress + 1) & 0x7f);
        if (low == 0 && high == 0)
        {
            // The hardware source repeatedly reads R_DIV until nonzero. A
            // monotonic millisecond counter supplies the same intentionally
            // non-deterministic fallback when a synthetic/debug file has no
            // playtime-derived ID yet.
            low = (byte)(Time.GetTicksMsec() & 0xff);
            if (low == 0)
                low = 1;
        }
        save.WriteWramByte(GameIdAddress, low);
        save.WriteWramByte(GameIdAddress + 1, high);
    }

    public readonly record struct Record(
        int SecretIndex,
        int ShortSecretIndex,
        int BeganFlag,
        int OfferTextId,
        int RefusalTextId,
        int ExplanationTextId,
        int SecretTextId,
        int FinalTextId,
        string OfferMessage,
        string RefusalMessage,
        string ExplanationMessage,
        string SecretMessage,
        string FinalMessage,
        string Source);
}

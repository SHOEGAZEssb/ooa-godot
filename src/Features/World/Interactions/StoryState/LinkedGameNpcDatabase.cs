using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived linkedGameNpcScript records and the five-character secret
/// generator used by the text engine's \secret1 command.
/// </summary>
public sealed class LinkedGameNpcDatabase
{
    private const int GameIdAddress = 0xc600;
    private const int PlaytimeCounterAddress = 0xc622;
    private const int ShortSecretIndexAddress = 0xc6fb;
    private const int SecretType = 3;

    private readonly Dictionary<
        (int Group, int Room, int InteractionId, int SubId),
        LinkedGameNpcDatabaseRecord> _records = [];
    private readonly byte[] _xorCipher;
    private readonly string[] _secretSymbols;

    public IReadOnlyCollection<LinkedGameNpcDatabaseRecord> Records =>
        _records.Values;
    internal IReadOnlyList<byte> XorCipher => _xorCipher;
    internal IReadOnlyList<string> SecretSymbols => _secretSymbols;

    public LinkedGameNpcDatabase()
    {
        GeneratedTable cipher = GeneratedTable.Load(
            "res://assets/oracle/objects/linked_secret_cipher.tsv",
            new GeneratedTableSchema(
                "linked-secret XOR cipher",
                GeneratedTableKeySemantics.Unique,
                ["index", "xor"],
                ["index"],
                headerRequired: true));
        var cipherValues = new List<byte>();
        foreach (GeneratedTableRow row in cipher.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index != cipherValues.Count)
                throw row.Invalid(0, $"the next contiguous index {cipherValues.Count}");
            cipherValues.Add((byte)row.HexByte(1));
        }
        _xorCipher = cipherValues.ToArray();

        GeneratedTable symbols = GeneratedTable.Load(
            "res://assets/oracle/objects/linked_secret_symbols.tsv",
            new GeneratedTableSchema(
                "linked-secret symbols",
                GeneratedTableKeySemantics.Unique,
                ["index", "utf8-base64"],
                ["index"],
                headerRequired: true));
        var symbolValues = new List<string>();
        foreach (GeneratedTableRow row in symbols.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index != symbolValues.Count)
                throw row.Invalid(0, $"the next contiguous index {symbolValues.Count}");
            string symbol = row.Base64Utf8(1);
            if (symbol.Length == 0)
                throw row.Invalid(1, "one display symbol or text command");
            symbolValues.Add(symbol);
        }
        _secretSymbols = symbolValues.ToArray();

        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/linked_game_npcs.tsv",
            new GeneratedTableSchema(
                "linked-game NPC",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "secret-index",
                    "short-secret-index", "began-flag", "has-extra-text",
                    "offer-text-id", "refusal-text-id", "explanation-text-id",
                    "secret-text-id", "final-text-id", "offer-utf8-base64",
                    "refusal-utf8-base64", "explanation-utf8-base64",
                    "secret-utf8-base64", "final-utf8-base64", "source"
                ],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new LinkedGameNpcDatabaseRecord(
                row.HexByte(0), row.HexByte(1), row.HexByte(2), row.HexByte(3),
                row.HexByte(4), row.HexByte(5), row.HexByte(6),
                row.Boolean01(7),
                row.HexWord(8), row.HexWord(9), row.HexWord(10),
                row.HexWord(11), row.HexWord(12),
                row.Base64Utf8(13), row.Base64Utf8(14), row.Base64Utf8(15),
                row.Base64Utf8(16), row.Base64Utf8(17),
                row.RequiredString(18));
            if (!_records.TryAdd(
                    (record.Group, record.Room, record.InteractionId, record.SubId),
                    record))
            {
                throw new InvalidOperationException(
                    $"Duplicate linked-game NPC record in {record.Source}.");
            }
        }

        if (_records.Count != 2 ||
            Get(0, 0x5d, 0xcb, 0x00) is not
                {
                    SecretIndex: 0x01,
                    ShortSecretIndex: 0x21,
                    BeganFlag: 0x51,
                    OfferTextId: 0x4d05,
                    FinalTextId: 0x4d09,
                    HasExtraText: true
                } ||
            Get(0, 0x83, 0xd5, 0x00) is not
                {
                    SecretIndex: 0x06,
                    ShortSecretIndex: 0x26,
                    BeganFlag: 0x56,
                    OfferTextId: 0x4d1e,
                    FinalTextId: 0x4d22,
                    HasExtraText: true
                })
        {
            throw new InvalidOperationException(
                "Linked-game NPC source records are incomplete.");
        }
        foreach (LinkedGameNpcDatabaseRecord record in _records.Values)
        {
            if (record.ShortSecretIndex != 0x20 + record.SecretIndex ||
                !record.SecretMessage.Contains(
                    "\\secret1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Malformed linked-game NPC record from {record.Source}.");
            }
        }
        if (_secretSymbols.Length != 64 || _xorCipher.Length != 48)
        {
            throw new InvalidOperationException(
                "Linked-secret symbol/cipher tables are incomplete.");
        }
    }

    internal bool TryGet(
        NpcRecord npc,
        out LinkedGameNpcDatabaseRecord record) =>
        _records.TryGetValue(
            (npc.Group, npc.Room, npc.Id, npc.SubId), out record);

    internal LinkedGameNpcDatabaseRecord Get(
        int group,
        int room,
        int interactionId,
        int subId)
    {
        if (_records.TryGetValue(
                (group, room, interactionId, subId), out var record))
        {
            return record;
        }
        throw new KeyNotFoundException(
            $"No linked-game NPC record for " +
            $"{group:x1}:{room:x2} ${interactionId:x2}:${subId:x2}.");
    }

    internal string GenerateSecret(
        LinkedGameNpcDatabaseRecord record,
        OracleSaveData save)
    {
        byte[] values = GenerateSecretValues(record, save);
        var result = new System.Text.StringBuilder();
        foreach (byte value in values)
            result.Append(_secretSymbols[value]);
        return result.ToString();
    }

    internal byte[] GenerateSecretValues(
        LinkedGameNpcDatabaseRecord record,
        OracleSaveData save)
    {
        EnsureGameId(save);
        int gameIdLow = save.ReadWramByte(GameIdAddress);
        int gameIdHigh = save.ReadWramByte(GameIdAddress + 1) & 0x7f;
        save.WriteWramByte(
            ShortSecretIndexAddress, (byte)record.ShortSecretIndex);

        int sum = (gameIdLow + gameIdHigh) & 0xff;
        int swappedHighNibble = (record.ShortSecretIndex >> 4) & 0x0f;
        int lowBitOffset = (record.ShortSecretIndex & 1) << 2;
        int cipherIndex =
            ((sum + swappedHighNibble) ^ lowBitOffset) & 0x07;

        byte[] buffer = new byte[20];
        InsertBits(buffer, cipherIndex, 3);
        InsertBits(buffer, SecretType, 2);
        InsertBits(buffer, gameIdLow, 8);
        InsertBits(buffer, gameIdHigh, 7);
        InsertBits(buffer, record.ShortSecretIndex, 6);
        InsertBits(buffer, 0, 4);

        int checksum = 0;
        foreach (byte value in buffer)
            checksum += value;
        buffer[^1] |= (byte)(checksum & 0x0f);

        byte[] shortBuffer = new byte[5];
        Array.Copy(
            buffer, buffer.Length - shortBuffer.Length,
            shortBuffer, 0, shortBuffer.Length);
        int cipherOffset = ((shortBuffer[0] & 0x38) >> 3) * 4;
        for (int index = 0; index < shortBuffer.Length; index++)
        {
            int cipher = _xorCipher[cipherOffset + index];
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
                buffer[index] =
                    (byte)(((previous << 1) | carry) & 0x3f);
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
        byte high =
            (byte)(save.ReadWramByte(PlaytimeCounterAddress + 1) & 0x7f);
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
}

public readonly record struct LinkedGameNpcDatabaseRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int SecretIndex,
    int ShortSecretIndex,
    int BeganFlag,
    bool HasExtraText,
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

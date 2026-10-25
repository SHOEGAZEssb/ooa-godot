using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oracles;

public sealed partial class LinkedGameNpcDatabase
{
    private (int Address, int Bits)[]? _ringFields;
    private byte[]? _ringSpacing;

    private (int Address, int Bits)[] RingFields => _ringFields ??= LoadRingFields();
    private byte[] RingSpacing => _ringSpacing ??= LoadRingSpacing();

    private static (int Address, int Bits)[] LoadRingFields()
    {
        var fields = new List<(int Address, int Bits)>();
        GeneratedTable table = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "objects/linked_secret_ring_fields.tsv",
            new("Ring secret fields", GeneratedTableKeySemantics.Ordered,
                ["index", "address", "bits", "source"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.Decimal(0) != fields.Count)
                throw row.Invalid(0, $"contiguous ring field index {fields.Count}");
            int address = row.HexWord(1);
            int bits = row.Decimal(2, 1, 8);
            if (fields.Count < 8 ? address is < AgesWramAddress.wRingsObtained or >= AgesWramAddress.wRingsObtained + 8 || bits != 8
                : address != AgesWramAddress.wLinkName + 5 || bits != 2)
                throw row.Invalid(1, "eight $c616-$c61d ring bytes followed by two bits of wLinkName+5");
            fields.Add((address, bits));
        }
        if (fields.Count != 9 || fields.Take(8).Select(field => field.Address).Distinct().Count() != 8 ||
            fields.Sum(field => field.Bits) != 66)
            throw new InvalidOperationException("bank3.s:secretDataToEncodeTable@entry2 has incomplete ring fields.");
        return fields.ToArray();
    }

    private static byte[] LoadRingSpacing()
    {
        var spacing = new List<byte>();
        GeneratedTable table = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "objects/linked_secret_ring_spacing.tsv",
            new("Ring secret spacing", GeneratedTableKeySemantics.Ordered,
                ["index", "value", "source"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.Decimal(0) != spacing.Count)
                throw row.Invalid(0, $"contiguous spacing index {spacing.Count}");
            spacing.Add((byte)row.HexByte(1));
        }
        if (!spacing.SequenceEqual(new byte[] { 0x25, 0x05, 0x25, 0x00 }))
            throw new InvalidOperationException("bank3.s:convertSecretBufferToText@entry2 has incomplete ring spacing.");
        return spacing.ToArray();
    }

    internal byte[] GenerateRingSecretValues(OracleSaveData save)
    {
        EnsureGameId(save);
        int gameIdLow = save.ReadWramByte(GameIdAddress);
        int gameIdHigh = save.ReadWramByte(GameIdAddress + 1);
        int cipher = (gameIdLow + gameIdHigh) & 7;
        var buffer = new byte[20];
        InsertBits(buffer, cipher, 3);
        InsertBits(buffer, 2, 2);
        InsertBits(buffer, gameIdLow, 8);
        InsertBits(buffer, gameIdHigh, 7);
        foreach ((int address, int bits) in RingFields)
            InsertBits(buffer, save.ReadWramByte(address), bits);
        InsertBits(buffer, 0, 4);
        return FinishSecretValues(buffer, 15);
    }

    internal (string First, string Second) GenerateRingSecretText(OracleSaveData save)
    {
        byte[] values = GenerateRingSecretValues(save);
        var first = new StringBuilder();
        var second = new StringBuilder();
        int index = 0;
        foreach (byte spacing in RingSpacing)
        {
            int count = spacing & 0x0f;
            if (count == 0) break;
            StringBuilder output = index < 10 ? first : second;
            for (int n = 0; n < count; n++) output.Append(_secretSymbols[values[index++]]);
            if ((spacing & 0xf0) == 0x20) output.Append(' ');
        }
        if (index != 15)
            throw new InvalidOperationException("bank3.s:convertSecretBufferToText@entry2 did not format fifteen symbols.");
        // w7SecretText2 starts twelve bytes after w7SecretText1.
        return (first.ToString(), second.ToString());
    }

    internal bool TryDecodeRingSecret(ReadOnlySpan<byte> symbols, OracleSaveData save,
        out byte[] rings)
    {
        rings = new byte[8];
        if (symbols.Length != 15) return false;
        byte[] buffer = symbols.ToArray();
        foreach (byte symbol in buffer) if (symbol >= 64) return false;
        int cipherOffset = ((buffer[0] & 0x38) >> 3) * 4;
        for (int index = 0; index < buffer.Length; index++)
            buffer[index] ^= (byte)(_xorCipher[cipherOffset + index] & (index == 0 ? 7 : 63));
        int checksum = buffer[^1] & 15;
        buffer[^1] &= 0x30;
        int sum = 0;
        foreach (byte value in buffer) sum += value;
        if ((sum & 15) != checksum) return false;

        int bitOffset = 0;
        int ReadBits(int count)
        {
            int value = 0;
            for (int bit = 0; bit < count; bit++, bitOffset++)
                value |= ((buffer[bitOffset / 6] >> (5 - bitOffset % 6)) & 1) << bit;
            return value;
        }
        _ = ReadBits(3);
        if (ReadBits(2) != 2) return false;
        int low = ReadBits(8), high = ReadBits(7);
        if ((low != 0 || high != 0) &&
            (low != save.ReadWramByte(GameIdAddress) || high != save.ReadWramByte(GameIdAddress + 1)))
            return false;
        foreach ((int address, int bits) in RingFields)
        {
            int value = ReadBits(bits);
            if (address >= AgesWramAddress.wRingsObtained && address < AgesWramAddress.wRingsObtained + 8)
                rings[address - AgesWramAddress.wRingsObtained] = (byte)value;
        }
        return true;
    }
}

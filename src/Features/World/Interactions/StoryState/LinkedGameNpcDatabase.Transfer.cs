using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class LinkedGameNpcDatabase
{
    private ((int Address,int Bits)[] Header,(int Address,int Bits)[] Body)? _transferFields;

    internal bool TryCreateGameFromSecret(ReadOnlySpan<byte> symbols, out OracleSaveData? save)
    {
        save = null;
        if (symbols.Length != 20) return false;
        byte[] buffer = symbols.ToArray();
        foreach (byte symbol in buffer) if (symbol >= 64) return false;
        int cipherOffset = ((buffer[0] & 0x38) >> 3) * 4;
        for (int index = 0; index < buffer.Length; index++)
            buffer[index] ^= (byte)(_xorCipher[cipherOffset + index] & (index == 0 ? 7 : 63));
        int checksum = buffer[^1] & 15;
        buffer[^1] &= 0x30;
        if ((buffer.Sum(value => value) & 15) != checksum) return false;
        int bitOffset = 0;
        byte ReadBits(int count)
        {
            int value = 0;
            for (int bit = 0; bit < count; bit++, bitOffset++)
                value |= ((buffer[bitOffset / 6] >> (5 - bitOffset % 6)) & 1) << bit;
            return (byte)value;
        }
        _transferFields ??= LoadTransferFields();
        var fields = _transferFields.Value;
        var values = new List<(int Address, byte Value)>();
        foreach (var field in fields.Header) values.Add((field.Address, ReadBits(field.Bits)));
        if (values[1].Value != 0) return false;
        foreach (var field in fields.Body) values.Add((field.Address, ReadBits(field.Bits)));
        // fileSelectMode6 rejects an Ages-origin linked secret; hero secrets
        // with the linked bit clear are accepted from either game.
        if (values[4 + 14].Value != 0 && values[4 + 1].Value == 1) return false;
        // loadUnpackedSecretData copies the body and GameID, not the scratch header.
        save = OracleSaveData.CreateNewGame(values.Skip(2).ToArray());
        return true;
    }
    internal byte[] GenerateGameTransferSecretText(OracleSaveData save)
    {
        var values=GenerateGameTransferSecretValues(save);var glyphs=new SecretEntryDatabase().Glyphs;
        var text=new List<byte>();int valueIndex=0,spacingIndex=0;bool terminated=false;
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/linked_secret_transfer_spacing.tsv",new("Transfer secret spacing",GeneratedTableKeySemantics.Ordered,
            ["index","value","source"],headerRequired:true)).Rows)
        {
            if(terminated||row.Decimal(0)!=spacingIndex++)throw row.Invalid(0,"contiguous spacing bytes before the terminator");
            int spacing=row.HexByte(1),count=spacing&15;
            if(count==0){terminated=true;continue;}
            if(valueIndex+count>values.Length)throw row.Invalid(1,"groups within the twenty-symbol buffer");
            for(int index=0;index<count;index++)text.Add(glyphs[values[valueIndex++]]);
            text.Add((byte)(spacing&0xf0));
        }
        if(!terminated||valueIndex!=20||text.Count!=24)throw new InvalidOperationException("bank3.s:convertSecretBufferToText: incomplete game-transfer spacing.");
        return text.ToArray();
    }
    private static ((int Address,int Bits)[] Header,(int Address,int Bits)[] Body) LoadTransferFields()
    {
        var header=new List<(int Address,int Bits)>();var body=new List<(int Address,int Bits)>();
        foreach(var row in GeneratedTable.Load("res://assets/oracle/objects/linked_secret_transfer_fields.tsv",new("Game transfer secret fields",GeneratedTableKeySemantics.Ordered,
            ["stream","index","address","bits","source"],headerRequired:true)).Rows)
        {
            var fields=row.RequiredString(0) switch {"header"=>header,"body"=>body,_=>throw row.Invalid(0,"header or body")};
            if(row.Decimal(1)!=fields.Count)throw row.Invalid(1,"contiguous original field order");
            int address=row.HexWord(2);if(address is <0xc600 or >0xc6ff)throw row.Invalid(2,"a source $c6xx byte");
            fields.Add((address,row.Decimal(3,1,8)));
        }
        if(header.Count!=4||header.Sum(field=>field.Bits)!=20||body.Count!=17||body.Sum(field=>field.Bits)!=96||
            header[2].Address!=GameIdAddress||header[3].Address!=GameIdAddress+1)
            throw new InvalidOperationException("bank3.s:secretDataToEncodeTable: incomplete game-transfer header/body.");
        return(header.ToArray(),body.ToArray());
    }
    internal byte[] GenerateGameTransferSecretValues(OracleSaveData save)
    {
        _transferFields??=LoadTransferFields();var fields=_transferFields.Value;
        byte linked=save.ReadWramByte(WramAddress.wFileIsLinkedGame),hero=save.ReadWramByte(WramAddress.wFileIsHeroGame);
        try
        {
            // bank3.s:generateGameTransferSecret temporarily transforms the
            // source file flags, then restores both bytes in their source order.
            save.WriteWramByte(WramAddress.wFileIsHeroGame,(byte)(linked|hero));
            save.WriteWramByte(WramAddress.wFileIsLinkedGame,(byte)((linked^1)|hero));
            EnsureGameId(save);
            int cipher=(save.ReadWramByte(GameIdAddress)+save.ReadWramByte(GameIdAddress+1))&7;
            save.WriteWramByte(fields.Header[0].Address,(byte)cipher);
            save.WriteWramByte(fields.Header[1].Address,0);
            var buffer=new byte[20];
            foreach(var field in fields.Header)InsertBits(buffer,save.ReadWramByte(field.Address),field.Bits);
            foreach(var field in fields.Body)InsertBits(buffer,save.ReadWramByte(field.Address),field.Bits);
            InsertBits(buffer,0,4);return FinishSecretValues(buffer,20);
        }
        finally
        {
            save.WriteWramByte(WramAddress.wFileIsLinkedGame,linked);
            save.WriteWramByte(WramAddress.wFileIsHeroGame,hero);
        }
    }
    private byte[] FinishSecretValues(byte[] buffer,int length)
    {
        int checksum=0;foreach(byte value in buffer)checksum+=value;
        buffer[^1]|=(byte)(checksum&15);
        var result=new byte[length];Array.Copy(buffer,buffer.Length-length,result,0,length);
        int cipherOffset=((result[0]&0x38)>>3)*4;
        for(int index=0;index<result.Length;index++)result[index]^=(byte)(_xorCipher[cipherOffset+index]&(index==0?7:63));
        return result;
    }
}

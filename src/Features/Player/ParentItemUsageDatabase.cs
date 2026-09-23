using System;

namespace oracleofages;

// chooseParentItemSlot is a decision over the live parent slots. This table
// does not own or mirror their lifetime, input state, or animation state.
internal sealed class ParentItemUsageDatabase
{
    private readonly ParentItemUsage[] _items=new ParentItemUsage[32];
    internal ParentItemUsageDatabase()
    {
        var table=GeneratedTable.Load("res://assets/oracle/metadata/parent_item_usage.tsv",
            new GeneratedTableSchema("itemUsageParameterTable",GeneratedTableKeySemantics.Unique,
                ["item-id","usage","input","source"],["item-id"],headerRequired:true));
        if(table.Rows.Count!=32) throw new InvalidOperationException("itemUsageParameterTable requires $20 ordered rows.");
        for(int i=0;i<32;i++)
        {
            var row=table.Rows[i];
            if(row.HexByte(0)!=i) throw row.Invalid(0,"ordered item ID");
            int usage=row.HexByte(1);
            if((usage&15)>5) throw row.Invalid(1,"chooseParentItemSlot selector $00-$05");
            bool edge=row.RequiredString(2) switch
            {
                "just-pressed"=>true, "held"=>false,
                _=>throw row.Invalid(2,"source held/just-pressed byte")
            };
            _items[i]=new(usage,edge,row.RequiredString(3));
        }
    }
    internal ParentItemUsage Item(int id) => id is >=0 and <32 ? _items[id]
        : throw new NotSupportedException($"itemUsageParameterTable ITEM${id:x2} is outside the imported table.");

    internal int ChooseSlot(int id,ReadOnlySpan<ParentItemSlotState> slots)
    {
        if(slots.Length!=4) throw new ArgumentException("Supply native ParentItem2 through ParentItem5 in order.",nameof(slots));
        var usage=Item(id);
        switch(usage.Selector)
        {
            case 0: return -1;
            case 4:
                if(slots[0].Enabled!=0) return -1;
                goto case 2;
            case 2:
                // The source compares IDs even if the enabled byte is zero.
                if(slots[1].Id==id || slots[2].Id==id) return -1;
                goto case 1;
            case 1: return slots[1].Enabled==0?3:slots[2].Enabled==0?4:-1;
            case 3: return usage.Enabled>=slots[0].Enabled?2:-1;
            case 5: return slots[3].Enabled==0?5:-1;
            default: throw new NotSupportedException($"{usage.Source}: unsupported selector ${usage.Selector:x2}.");
        }
    }
}

internal readonly record struct ParentItemUsage(int Usage,bool JustPressed,string Source)
{
    internal int Selector=>Usage&15;
    internal int Enabled=>(Usage&0xf0)+1;
}
internal readonly record struct ParentItemSlotState(byte Enabled,byte Id);

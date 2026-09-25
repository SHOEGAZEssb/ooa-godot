using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateParentItemUsage()
    {
        var data=new ParentItemUsageDatabase();
        // Independent literals from Ages itemUsageParameterTable, not a
        // comparison of the parser's result with the table consumed by it.
        int[] source=[0x00,0x05,0x03,0x23,0x03,0x63,0x02,0x00,
            0x00,0x00,0x73,0x00,0x73,0x02,0x05,0x43,
            0x00,0x05,0x00,0x00,0x00,0x13,0x13,0x01,
            0x00,0x02,0x00,0x00,0x00,0x00,0x00,0x00];
        var slots=new ParentItemSlotState[4];
        for(int item=0;item<32;item++)
        {
            var row=data.Item(item);
            FailIf(row.Usage!=source[item] || row.JustPressed!=(item is not (0 or 1 or 0x16)),
                $"ITEM${item:x2} must retain its source priority/selector and held-versus-pressed input byte.");
            if((source[item]&15)!=3) continue;
            for(int enabled=0;enabled<=255;enabled++)
            {
                slots[0]=new((byte)enabled,4);
                int expected=((source[item]&0xf0)+1)>=enabled?2:-1;
                FailIf(data.ChooseSlot(item,slots)!=expected,
                    $"chooseParentItemSlot ITEM${item:x2} must compare priority+1 with the full enabled byte ${enabled:x2}.");
            }
        }
        slots[0]=new(1,4);
        FailIf(data.ChooseSlot(4,slots)!=2 || data.ChooseSlot(5,slots)!=2,
            "Cane must restart at equal priority and permit a higher-priority sword replacement.");
        slots[0]=new(0x61,5);
        FailIf(data.ChooseSlot(4,slots)!=-1,"Cane cannot replace an active sword parent.");
        slots=new ParentItemSlotState[4];
        FailIf(data.ChooseSlot(0,slots)!=-1 || data.ChooseSlot(0x17,slots)!=3 || data.ChooseSlot(1,slots)!=5,
            "Unusable, dynamic-parent and shield selectors must preserve their distinct native slots.");
        slots[1]=new(1,0x17);
        FailIf(data.ChooseSlot(0x17,slots)!=4,"Feather must allow a second instance in ParentItem4.");
        slots[2]=new(1,0x17);
        FailIf(data.ChooseSlot(0x17,slots)!=-1,"Both occupied dynamic parents must reject another Feather.");
        slots[1]=new(0,0x19); slots[2]=default;
        FailIf(data.ChooseSlot(0x19,slots)!=-1,
            "Unique-item lookup must compare the stale ID even when its enabled byte is zero.");
        slots[1]=new(1,6);
        FailIf(data.ChooseSlot(0x19,slots)!=4,"Satchel must use the free second dynamic parent after a different item.");
        slots[3]=new(1,1);
        FailIf(data.ChooseSlot(0x11,slots)!=-1,"Harp cannot overwrite an occupied ParentItem5.");
        GD.Print("Validated all32 source parent-item usage rows, full enabled-byte priority comparisons, Cane restart/replacement, dynamic slots and unique-item ID checks.");
    }
}

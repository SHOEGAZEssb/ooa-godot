using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedCompanionDebugGrants(FrontendValidationRoot root,bool batched)
    {
        var session=root._gameplaySession!;var rooms=session.Rooms;var save=rooms.SaveData;
        var inventory=root._scene.Player.Inventory;var entities=root._seasonsOpening?.Entities??root._entities;
        var app=new ApplicationValidationFixture(root);var menus=session.Menus;var screen=root._scene.DebugFlagScreen;
        // data/{ages,seasons}/treasureObjectData.s: Seasons has only Flute subid $00, parameter $0c.
        (int SubId,int Companion)[] flutes=save.Game==OracleGame.Ages?[(0,0x0b),(1,0x0c),(2,0x0d)]:[(0,0x0c)];
        var nativeRows=TreasureDatabase.For(rooms.AssetRoot).Objects.Where(row=>row.TreasureId==TreasureId.Flute).ToArray();
        FailIf(!nativeRows.Select(row=>(row.SubId,row.Parameter)).SequenceEqual(flutes),
            $"{save.Game}: F1 must use its native treasureObjectData.s Flute rows.");
        string[] fields=["wRickyState","wDimitriState","wMooshState"];
        byte[] oldStates=fields.Select(field=>save.ReadField(field)).ToArray();
        int oldAnimal=inventory.AnimalCompanion,oldA=inventory.EquippedA,oldB=inventory.EquippedB;
        byte oldIcon=save.ReadField("wFluteIcon");bool oldFlute=inventory.HasTreasure(TreasureId.Flute);
        int oldGroup=rooms.ActiveGroup,oldRoom=rooms.CurrentRoom.Id;bool quest=save.HasGlobalFlag(0x23);
        void Load(int group,int room){if(save.Game==OracleGame.Ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);}
        void Press(string action)=>app.Step(1,Vector2.Zero,held:[action],pressed:[action],batched:batched);
        try
        {
            entities.Clear();Load(0,save.Game==OracleGame.Ages?0x2a:0x97);entities.Clear();
            foreach(var (subid,companion) in flutes)
            {
                int selected=companion-0x0b;
                inventory.LoseTreasure(TreasureId.Flute);save.WriteField("wFluteIcon",0);
                byte[] expected=[1,0x20,0x40];
                for(int index=0;index<3;index++)save.WriteField(fields[index],expected[index]);
                inventory.GiveTreasure(TreasureId.Flute,0x0b+selected);
                FailIf(save.ReadField("wFluteIcon")!=0 || fields.Where((field,index)=>save.ReadField(field)!=expected[index]).Any(),
                    $"{save.Game}: retail Strange Flute must not initialize native companion readiness.");
                Press("debug_flags");
                FailIf(!menus.Flags.IsActive || !menus.Pause.IsOwnedBy(menus.Flags),"F1 companion grants require the actual shared modal owner.");
                screen.SelectTreasureForValidation($"TREASURE_OBJECT_FLUTE_{subid:x2}");
                Press("attack");expected[selected]|=0x80;
                void Check()
                {
                    int address=save.Game==OracleGame.Ages?0xc646:0xc643;
                    for(int index=0;index<3;index++)
                        FailIf(save.ReadWramByte(address+index)!=expected[index],
                            $"{save.Game}: F1 companion ${selected+0x0b:x2} field{fields[index]} expected${expected[index]:x2}/actual${save.ReadWramByte(address+index):x2}; must set only its native bit7 and retain all other state bits.");
                    FailIf(save.HasGlobalFlag(0x23)!=quest,"F1 companion readiness must not complete GLOBALFLAG_GOT_FLUTE $23.");
                }
                Check();
                FailIf(!inventory.HasTreasure(TreasureId.Flute) || inventory.AnimalCompanion!=0x0b+selected || save.ReadField("wFluteIcon")!=selected+1,
                    $"{save.Game}: actual F1 grant must supply the selected callable Flute and icon.");
                if(!OracleSaveData.TryDeserialize(save.Serialize(),save.Game,out var restored))
                    throw new InvalidOperationException($"{save.Game}: callable debug Flute save did not deserialize.");
                FailIf(restored!.ReadField(fields[selected])!=expected[selected] || restored.ReadField("wFluteIcon")!=selected+1 ||
                    restored.ReadField("wAnimalCompanion")!=selected+0x0b,"F1 native readiness/icon/animal must survive explicit serialization.");
                var position=root._scene.Player.PrecisePosition;int rng=entities.Random.Calls;
                app.Step(4,Vector2.Right,batched:batched);Check();
                FailIf(root._scene.Player.PrecisePosition!=position || entities.Random.Calls!=rng,"F1 grant inspection must retain the world pause.");
                Press("attack");Check();
                FailIf(inventory.HasTreasure(TreasureId.Flute),"Repeating a named Flute selection must remove the item without clearing native quest state.");
                Press("attack");Check();Press("debug_flags");
                FailIf(menus.Flags.IsActive || menus.Pause.IsLeased,"F1 grant completion must release the shared pause lease.");
                entities.Clear();
            }
        }
        finally
        {
            if(menus.Flags.IsActive)menus.Flags.CloseImmediatelyForValidation();
            inventory.LoseTreasure(TreasureId.Flute);
            if(oldFlute)inventory.GiveTreasure(TreasureId.Flute,oldAnimal);
            save.WriteField("wAnimalCompanion",(byte)oldAnimal);
            typeof(InventoryState).GetProperty(nameof(InventoryState.AnimalCompanion))!.SetValue(inventory,oldAnimal);
            save.WriteField("wFluteIcon",oldIcon);
            for(int index=0;index<3;index++)save.WriteField(fields[index],oldStates[index]);
            inventory.EquipA(oldA);inventory.EquipB(oldB);Load(oldGroup,oldRoom);entities.Clear();
        }
    }
}

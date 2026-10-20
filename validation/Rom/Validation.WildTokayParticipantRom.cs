using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWildTokayParticipantRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(bool earlier in new[]{false,true})
        foreach(bool red in new[]{false,true})
        foreach(int level in new[]{0,3})
        foreach(int delta in new[]{10,11})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.SetScriptedEquippedItems(0,0);
            var data=new WildTokayGameDatabase();_saveData.WriteWramByte(data.WildLevelAddress,(byte)level);
            LoadValidationRoom(2,0xde);var game=_roomEvents.Get<WildTokayGameEvent>();var record=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x0d).Record;
            game.Cancel();_entities.Clear();_player.WarpTo(new(80,72));_player.Face(Vector2I.Up);
            var actor=new WildTokayParticipantRoomEntity(game,record with{SubId=0x0c,X=24,Y=64,Implementation=NpcImplementationClassification.EventOwned});
            if(earlier)_entities.AddEntity(actor);
            var meat=(WildTokayMeat)_entities.TryCreateInteraction(new WildTokayMeatSpawn())!;
            if(!earlier)_entities.AddEntity(actor);
            F1Set(meat,"<State>k__BackingField",1);F1Set(meat,"<Substate>k__BackingField",2);
            FailIf(!meat.TryUseBracelet(_player,Vector2I.Zero)||!meat.TryUseBracelet(_player,Vector2I.Right),"Participant fixture must acquire and release its actual reserved Bracelet owner.");
            F1Set(meat,"_z",-5*256);F1Set(meat.ReservedThrow!,"<Position>k__BackingField",new Vector2(24+delta,64));
            // Freeze the item phase so the catch helper sees an independently
            // supplied rectangle at the inclusive $0a/$0b boundary. Full
            // moving ITEM C and game-loop handoffs are covered separately.
            _runtimeState.SetWramByte(0xcc8a,0x10);_runtimeState.SetWramByte(0xcfda,(byte)(0xd0+_entities.InteractionSlot(meat)));_runtimeState.SetWramByte(0xcfdb,0x45);
            _runtimeState.SetWramByte(0xcfdf,red?(byte)4:(byte)0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,72);
            int a=0xd040+_entities.InteractionSlot(actor.Node)*256,m=0xd040+_entities.InteractionSlot(meat)*256;
            native[a]=1;native[a+1]=0x48;native[a+2]=0x0c;native[a+11]=64;native[a+13]=24;
            native[m]=1;native[m+1]=0x8c;native[m+4]=2;native[m+5]=2;native[m+15]=0xfb;
            native[0xdc00]=1;native[0xdc0b]=64;native[0xdc0d]=(byte)(24+delta);native[0xcfda]=(byte)(m>>8);native[0xcfdb]=0x45;native[0xcfdf]=red?(byte)4:(byte)0;native[0xcc8a]=0x10;
            int updates=0;string source=$"Wild participant/early{earlier}/red{red}/level{level}/distance{delta}/batch{batch}";
            StepGameplayUpdates(170,Vector2.Zero,batched:batch,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                var npc=(TokayCharacter)actor.Node;
                FailIf(actor.Finished!=(native[a]==0)||!actor.Finished&&(actor.State!=native[a+4]||actor.Substate!=native[a+5]||actor.Counter!=native[a+6]||npc.Position!=new Vector2(native[a+13],native[a+11])||npc.CurrentAnimationCounter!=native[a+0x20]),
                    source+$" update{updates}: catch/pause, movement, animation or lifetime differs (sub{actor.Substate}/{native[a+5]}, c{actor.Counter}/{native[a+6]}, pos{npc.Position}/{native[a+13]},{native[a+11]}, anim{npc.CurrentAnimationCounter}/{native[a+0x20]}).");
                FailIf(_entities.ReservedBraceletChildActive!=(native[0xdc00]!=0)||_runtimeState.ReadWramByte(0xcfde)!=native[0xcfde],source+$" update{updates}: reserved item deletion or result signal differs.");
                foreach(var child in _entities.Entities<TokayAttachedVisualRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(child)*256;
                    if(native[at+4]!=0)FailIf(native[at+1]!=0x63||native[at+2]!=0x73||child.Position!=new Vector2(native[at+13],native[at+11]),source+$" update{updates}: held accessory allocation, initialization or frame offset differs.");
                }
                if(updates==4){_dialogue.ShowMessage("Catch freeze.",104);native[0xcba0]=1;}
                if(updates==7){_dialogue.Close();native[0xcba0]=0;}
                if(updates==12){_runtimeState.SetWramByte(0xcc8a,0x12);native[0xcc8a]=0x12;}
                if(updates==15){_runtimeState.SetWramByte(0xcc8a,0x10);native[0xcc8a]=0x10;}
            });
            bool caught=delta==10;
            FailIf(!actor.Finished||_runtimeState.ReadWramByte(0xcfde)!=(caught?red?1:0:0xff)||meat.Finished!=caught,
                source+": source inclusive catch, final-red success or missed participant failure differs.");
            _entities.Clear();
        }
    }
}

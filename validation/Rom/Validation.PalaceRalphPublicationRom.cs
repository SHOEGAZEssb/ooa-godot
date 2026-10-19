using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePalaceRalphPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(TreasureId.Essence,5);_saveData.SetGlobalFlag(GlobalFlag.Flag0b);
            LoadValidationRoom(1,0x46);_inventory.SetScriptedEquippedItems(0,0);
            var palace=_roomEvents.Get<DekuForestPalaceEvent>();var owner=SomariaPrivate<PalaceRalphEvent>(palace,"_ralph");
            var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record is{Id:0x37,SubId:9});
            _player.WarpTo(new(80,104));_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            StepGameplayUpdates(1,Vector2.Zero); // Consume the already requested native state08 setup.
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x37;native[at+2]=9;native[at+11]=120;native[at+13]=80;
            native.AdvanceInteractions();
            Vector2 initial=new(80.25f,8.5f);F1Set(owner,"_precise",initial);actor.Position=initial.Floor();
            native.Word(at+10,0x0880);native.Word(at+12,0x5040);
            var commands=CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/palace_ralph_commands.tsv");
            var move=commands.OfType<CutsceneMoveCommand>().Single();
            FailIf(move.Angle!=0||move.Counter!=0x28||native[at+16]!=0x50,"Palace Ralph source speed/angle/counter changed.");
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            runner.Start([move with{Source=move.Source with{CommandIndex=0}},
                new CutsceneWaitCommand(move.Source with{CommandIndex=1,Opcode="wait"},255),
                new CutsceneEndCommand(move.Source with{CommandIndex=2,Opcode="scriptend"})]);
            runner.SetInitialMotionRegisters("Ralph",0x50,0);runner.AdvanceFrame();actor.AdvanceAnimationUpdates(3);
            // Declared move-initialization boundary: native moveup installed
            // counter2=$28/animation0, then its handler animated three times.
            native[at+7]=0x28;native[at+9]=0;native[0xffae]=0x40;
            byte[] caller=[0x16,(byte)(at>>8),0xcd,0x58,0x27,0xc9];
            for(int i=0;i<caller.Length;i++)native[0xc100+i]=caller[i];
            SomariaPrivate<FrontendRom>(native,"_rom").Call(0xc100,0);native[0xffae]=0;
            int updates=0,moves=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previous=native.Word(at+10);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(previous!=native.Word(at+10))moves++;
                FailIf(SomariaPrivate<Vector2>(owner,"_precise")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||runner.MovementCounter("Ralph")!=native[at+7]||
                    actor.CurrentAnimationCounter!=native[at+0x20]||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Palace Ralph update{updates}: native move/counter/animation/admission/publication differs.");
                Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Ordinary palace Ralph hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
            Step(38);
            FailIf(moves!=39||runner.MovementCounter("Ralph")!=0,"Palace Ralph must execute exactly39 source steps and its zero/no-motion updates.");
            palace.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(palace.BlocksGameplay||_player.CutsceneControlled||_saveData.HasGlobalFlag(0x32),
                "Cancelling the isolated Ralph boundary must release control without completing flag$32.");
        }
    }
}

using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private WildTokayControllerRoomEntity BeginWildTokayRound(bool batched,bool present=false)
    {
        var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
        InitializeTransientSession(save);var data=new WildTokayGameDatabase();
        _inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.AddRupees(30);_inventory.SetScriptedEquippedItems(TreasureId.Bombs,TreasureId.Sword);
        if(present){_saveData.SetGlobalFlag(data.FinishedGameFlag);_saveData.SetGlobalFlag(data.BeganSecretFlag);}
        LoadValidationRoom(2,present?0xe5:0xde);StepGameplayUpdates(4,Vector2.Zero,batched:batched);
        // Use a declared shared-RNG input for this focused first-round win;
        // the separate executed-ROM matrix covers every level's full schedule.
        _random.RestoreState(_random.CaptureState() with{Rng1=12,Rng2=3});
        var manager=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==(present?0x19:0x0d));
        ApproachHungryTokay(manager,batched);StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
        void Await(int id)
        {
            for(int i=0;i<200&&!_dialogue.IsOpen;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(id)),$"Wild Tokay expected TX_${id:x4}: {_dialogue.CurrentMessage}.");
        }
        if(!present)
        {
            Await(0x0a10);_dialogue.Close();Await(0x0a13);_dialogue.SubmitChoiceForValidation(0);Await(0x0a14);_dialogue.SubmitChoiceForValidation(0);Await(0x0a15);
        }
        else {Await(0x0a51);_dialogue.SubmitChoiceForValidation(0);Await(0x0a4a);_dialogue.SubmitChoiceForValidation(0);Await(0x0a4c);}
        _dialogue.Close();
        for(int i=0;i<200&&!_dialogue.IsOpen;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
        Await(0x0a16);var controller=_entities.EntityAdapters<WildTokayControllerRoomEntity>().Single();
        FailIf(manager.Active||_inventory.EquippedB!=0||_inventory.EquippedA!=TreasureId.Bracelet||_player.Position!=new Vector2(80,72),"Source controller must delete its manager and equip Bracelet after the start fades.");
        _dialogue.Close();StepGameplayUpdates(1,Vector2.Zero,batched:batched);
        FailIf(controller.Substate!=4||_entities.Entities<WildTokayMeat>().Count!=1,"Closing TX_0a16 must begin play with one checked $8c.");
        return controller;
    }
    private void ValidateWildTokayGameplay()
    {
        foreach(bool batched in new[]{false,true})
        {
            var controller=BeginWildTokayRound(batched);int caught=0,throws=0;bool firstThrow=false;
            for(int i=0;i<2300&&controller.Substate==4;i++)
            {
                var participants=_entities.EntityAdapters<WildTokayParticipantRoomEntity>().Where(p=>!p.Finished&&!p.HoldingMeat).ToArray();
                var target=participants.OrderByDescending(p=>p.Node.Position.Y>=0xf0?p.Node.Position.Y-256:p.Node.Position.Y).FirstOrDefault();
                bool press=false;Vector2 movement=Vector2.Zero;
                if(_player.IsCarryingObject)
                {
                    if(target is not null&&_bracelet.State==BraceletState.Idle)
                    {
                        float x=target.Node.Position.X<80?52:108;
                        if(Mathf.Abs(_player.Position.X-x)>1)movement=_player.Position.X<x?Vector2.Right:Vector2.Left;
                        else if(target.Node.Position.Y is >=58 and <128)
                        {
                            movement=target.Node.Position.X<80?Vector2.Left:Vector2.Right;
                            _player.Face((Vector2I)movement);press=true;throws++;firstThrow=true;
                        }
                    }
                }
                else if(_bracelet.State==BraceletState.Idle)
                {
                    if(Mathf.Abs(_player.Position.X-80)>1)movement=_player.Position.X<80?Vector2.Right:Vector2.Left;
                    else if(!_entities.ReservedBraceletChildActive&&_entities.Entities<WildTokayMeat>().Any(m=>m.State==1&&m.Substate==2))
                    {_player.Face(Vector2I.Up);press=true;}
                }
                int before=_entities.EntityAdapters<WildTokayParticipantRoomEntity>().Count(p=>p.HoldingMeat);
                string[] held=movement.X<0?press?["move_left","attack"]:["move_left"]:movement.X>0?press?["move_right","attack"]:["move_right"]:press?["attack"]:[];
                StepGameplayUpdates(1,movement,held,press?["attack"]:[],batched);
                caught+=Math.Max(0,_entities.EntityAdapters<WildTokayParticipantRoomEntity>().Count(p=>p.HoldingMeat)-before);
                FailIf(_runtimeState.ReadWramByte(0xcfde)==0xff,$"Real Wild Tokay play missed a participant at update{i}, caught{caught}, throws{throws}, Link{_player.Position}, carrying{_player.IsCarryingObject}, bracelet{_bracelet.State}, targets{string.Join(';',participants.Select(p=>p.Node.Position))}.");
            }
            FailIf(!firstThrow||caught<2||_runtimeState.ReadWramByte(0xcfde)!=1||controller.Substate!=5,"Real Bracelet throws must feed every first-round Tokay and allow its red final participant to publish success.");
            for(int i=0;i<500&&!_inventory.HasTreasure(0x4d);i++)
            {
                if(_dialogue.IsOpen)_dialogue.Close();StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            }
            FailIf(!_inventory.HasTreasure(0x4d)||_saveData.ReadWramByte(new WildTokayGameDatabase().WildLevelAddress)!=1||_inventory.EquippedB!=TreasureId.Bombs||_inventory.EquippedA!=TreasureId.Sword,
                "Unforced first-round win must return through the actual room warp, restore equipment and award the Scent Seedling.");
            _dialogue.Close();StepGameplayUpdates(80,Vector2.Zero,batched:batched);
            var manager=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x0d);ApproachHungryTokay(manager,batched);
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a10)),"Returned manager must be reachable and accept a repeat conversation after awarding the prize.");
            _dialogue.Close();LoadValidationRoom(4,0xa1);
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_entities.Entities<WildTokayMeat>().Any(),"Leaving Wild Tokay must release input and all room-owned meat.");
        }
    }
    private void ValidateWildTokayLossAndCancellation()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool present in new[]{false,true})
        {
            var controller=BeginWildTokayRound(batched,present);
            for(int i=0;i<400&&controller.Substate==4;i++)StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(controller.Substate!=5||controller.Counter!=30||_runtimeState.ReadWramByte(0xcfde)!=0xff||_runtimeState.ReadWramByte(0xcc8a)!=1,"An unfed participant must publish failure, followed by the controller's next-update loss mask and counter30.");
            for(int i=0;i<400;i++)
            {
                if(_dialogue.IsOpen)
                {
                    int result=present?0x0a4d:0x0a19;
                    if(_dialogue.CurrentMessage==DialogueBox.PlainText(new TokayInteractionDatabase().Text(result)))break;
                    _dialogue.Close();
                }
                StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            }
            FailIf(!_dialogue.ChoiceActive||_inventory.EquippedB!=TreasureId.Bombs||_inventory.EquippedA!=TreasureId.Sword,"The actual loss return must restore equipment and present the retry choice.");
            _dialogue.SubmitChoiceForValidation(1);StepGameplayUpdates(present?21:1,Vector2.Zero,batched:batched);
            if(present){FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a4e)),"Declining the present retry must retain source wait20 and TX_0a4e.");_dialogue.Close();StepGameplayUpdates(1,Vector2.Zero,batched:batched);}
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_runtimeState.ReadWramByte(0xcc02)!=0||_saveData.ReadWramByte(new WildTokayGameDatabase().WildLevelAddress)!=0,"Declining a lost round must release both raw masks without awarding a prize.");

            BeginWildTokayRound(batched,present);StepGameplayUpdates(70,Vector2.Zero,batched:batched);
            _player.Face(Vector2I.Up);StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);StepGameplayUpdates(20,Vector2.Zero,batched:batched);
            FailIf(!_player.IsCarryingObject||_entities.Entities<WildTokayMeat>().Count!=2,"Cancellation fixture must hold actual meat after its replacement allocation.");
            LoadValidationRoom(4,0xa1);
            FailIf(_player.IsCarryingObject||_entities.ReservedBraceletChildActive||_entities.Entities<WildTokayMeat>().Any()||_runtimeState.ReadWramByte(0xcc8a)!=0||
                _inventory.EquippedB!=TreasureId.Bombs||_inventory.EquippedA!=TreasureId.Sword,"Room cancellation must release the carried meat and restore the controller's saved equipment.");
        }
    }
}

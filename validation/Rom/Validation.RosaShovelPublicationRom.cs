using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRosaShovelPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetLinkedGame(true);
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            _saveData.SetRoomFlag(1,0xcb,0x40,false);_inventory.LoseTreasure(TreasureId.Shovel);
            _inventory.SetScriptedEquippedItems(0,0);LoadValidationRoom(1,0xcb);
            var rosa=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x68);
            var digging=_roomEvents.Get<RosaShovelEvent>();
            _player.WarpTo(new(40,16));
            FailIf(_collision.Collides(_player.Position)||_currentRoom.GetTerrainInfo(_player.Position).Hazard!=HazardType.None,
                "Rosa approach must start on actual safe room floor.");
            StepGameplayUpdates(40,Vector2.Down,["move_down"],["move_down"],batched);
            StepGameplayUpdates(40,Vector2.Right,["move_right"],["move_right"],batched);
            FailIf(_collision.Collides(_player.Position)||!rosa.CanTalkTo(_player),
                $"Rosa must be reachable through original geometry: Link={_player.Position}/{_player.FacingVector}, terrain={_currentRoom.GetTerrainInfo(_player.Position)}.");
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen||digging.Stage!=RosaShovelStage.FirstText,"Real A input must start Rosa's shovel script.");
            _dialogue.Close();StepGameplayUpdates(31,Vector2.Zero,batched:batched);
            FailIf(digging.Stage!=RosaShovelStage.MoveRight,"Rosa source wait30 must initialize moveright$30 before motion.");
            var native=new ObjectMotionRom();const int at=ObjectMotionRom.Object+0x40;
            native.Word(at+10,(int)(rosa.Position.Y*256));native.Word(at+12,(int)(rosa.Position.X*256));
            native[at+9]=8;native[at+16]=5; // rosa_subid00Script: SPEED_020,moveright$30.
            byte[] observed=new byte[4];bool armed=false;int visits=0,moves=0;
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);armed=false;
            }
            _runtimeState.Changed+=Capture;
            var phaseProbe=new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++)
                {observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
                armed=true;
            });_entities.AddEntity(phaseProbe);
            void Step(int count,bool frozen=false)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                if(!frozen){if(visits<47){native.Call(ObjectMotionRom.ApplySpeed,slot:0x40);moves++;}visits++;}
                FailIf(SomariaPrivate<Vector2>(digging,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    !Enumerable.Range(0,4).All(i=>observed[i]==native[0xcec0+i])||
                    visits<=48&&SomariaPrivate<int>(digging,"_counter")!=48-visits,
                    $"Rosa source visit{visits}/frozen{frozen}: fractional motion, zero-update counter or immediate publication differs.");
                armed=false;
            });
            Step(4);
            _dialogue.ShowMessage("Rosa ordinary hold.",_player.Position.Y);Step(3,true);_dialogue.Close();
            _runtimeState.SetWramByte(0xcc8a,2);Step(3,true);_runtimeState.SetWramByte(0xcc8a,0);
            _runtimeState.SetWramByte(0xcd00,8);Step(3,true);_runtimeState.SetWramByte(0xcd00,1);
            Step(45);FailIf(moves!=47||digging.Stage!=RosaShovelStage.SecondTextWait,
                "Rosa must retain47 fractional steps and separate zero/install-wait updates.");
            _runtimeState.Changed-=Capture;_entities.RetireEntity(phaseProbe);
            StepGameplayUpdates(70,Vector2.Zero,batched:batched);
            FailIf(!_dialogue.IsOpen||digging.Stage!=RosaShovelStage.SecondText,
                "Rosa's source20/20/30 waits must lead to her second textbox.");
            _dialogue.Close();StepGameplayUpdates(31,Vector2.Zero,batched:batched);
            for(int update=0;(!_inventory.HasTreasure(TreasureId.Shovel)||!_dialogue.IsOpen)&&update<80;update++)
                StepGameplayUpdates(1,Vector2.Zero,batched:batched);
            FailIf(!_inventory.HasTreasure(TreasureId.Shovel)||!_dialogue.IsOpen||digging.Stage!=RosaShovelStage.Reward,
                "Rosa must hand the shovel to the shared get-item owner and retain control during its text.");
            StepGameplayUpdates(8,Vector2.Zero,batched:batched);_dialogue.Close();
            StepGameplayUpdates(40,Vector2.Zero,batched:batched);
            FailIf(digging.HasState||_player.CutsceneControlled||!_saveData.HasRoomFlag(1,0xcb,0x40),
                "Rosa reward completion must persist flag$40 after its final wait and release input.");
            StepGameplayUpdates(8,Vector2.Right,["move_right"],["move_right"],batched);
            FailIf(_collision.Collides(_player.Position)||!rosa.CanTalkTo(_player),"Rosa must remain reachable after moving and giving the shovel.");
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            FailIf(!_dialogue.IsOpen||digging.Stage!=RosaShovelStage.DialogueOnly,
                "Repeating Rosa's real A interaction after completion must use the completed-room dialogue.");
            _dialogue.Close();StepGameplayUpdates(2,Vector2.Zero,batched:batched);digging.Cancel();
            FailIf(_player.CutsceneControlled||SomariaPrivate<RosaNpcRoomEntity?>(digging,"_nativeActor") is not null,
                "Cancelling Rosa after movement must release script/native input ownership.");
        }
    }
}

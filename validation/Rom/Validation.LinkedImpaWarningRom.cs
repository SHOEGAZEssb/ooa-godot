using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkedImpaWarningRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetLinkedGame(true);LoadValidationRoom(4,0xd4);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x60,0x48));_transitions.ResetCamera();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x60,0x48)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom.InitializeLinkGameplay();rom[0xcd17]=0xd0;
            rom[0xcc01]=1;rom[0xc2ef]=9;rom[0xcc03]=1;rom[0xcbe4]=(byte)_statusBar.DisplayedHealth;
            foreach(var pair in new[]{(0xcbc2,15),(0xcfde,10),(0xcfd0,10),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0,generation=0,hold=0,readyAt=-1,pressed=0;bool pressedToContinue=false;
            var sounds=_sound.AttachPlayRequestAudit();
            while(update<1500&&_runtimeState.ReadWramByte(0xcc04)==0)
            {
                pressed=0;
                if(owner.State==0x14&&_dialogue.PrintingComplete)
                {
                    if(readyAt<0)readyAt=update;
                    if(update>=readyAt+3){pressed=1;pressedToContinue=true;}
                }
                else if(owner.State==0x14&&_dialogue.IsPageComplete&&_dialogue.HasNextMessage)pressed=1;
                StepGameplayUpdates(owner.State>=0x14?1:3,Vector2.Zero,pressed:pressed!=0?["attack"]:[],batched:batched,afterUpdate:Compare);
            }
            _sound.SetRequestObserver(null);
            FailIf(update>=1500||!pressedToContinue||rom[0xcc04]!=0x12,
                "Linked Impa warning must wait for printed non-exitable text and a fresh physical key, fade, then trigger CUTSCENE $12.");
            owner.Cancel();owner.Cancel();
            FailIf(owner.HasState||_runtimeState.ReadWramByte(0xcc04)!=0||_player.CutsceneControlled,
                "Cancelling the pending kidnapping handoff must release its trigger and Link ownership.");
            void Compare()
            {
                if(rom[0xcfd0]==0x12&&_dialogue.IsOpen)rom[0xcba0]=_dialogue.PrintingComplete?(byte)0x80:(byte)1;
                rom[0xc482]=(byte)pressed;rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();
                rom.UpdateGameplay(pressed,pressed,255,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                foreach(int at in new[]{0xcbc2,0xcbb3,0xcfd0,0xcfd5,0xcfd6,WramAddress.wDisabledObjects,WramAddress.wMenuDisabled})
                    FailIf(_runtimeState.ReadWramByte(at)!=rom[at],
                        $"Linked Impa update{update} byte${at:x4}: port${_runtimeState.ReadWramByte(at):x2}/ROM${rom[at]:x2}.");
                foreach(var actor in _entities.EntityAdapters<LinkedLeadInActorRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.Actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.Actor.CurrentAnimationCounter!=rom[at+0x20],
                        $"Linked Impa update{update}: native walking/jump animation differs, pos{actor.Actor.Position}/{rom[at+13]},{rom[at+11]},counter{actor.Actor.CurrentAnimationCounter}/{rom[at+0x20]}.");
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||
                    !sounds.Requests.Where(s=>s!=SoundId.SndText&&s!=SoundId.SndText2).SequenceEqual(rom.Sounds),
                    $"Linked Impa update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=rom[0xcfd0]==0x12?0:3;}
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
        }
    }
}

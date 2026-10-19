using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayCookPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(TreasureId.TradeItem,2);
            _saveData.SetRoomFlag(2,0x3f,0x20,false);LoadValidationRoom(2,0x3f);
            var cooking=_roomEvents.Get<TokayCookEvent>();
            var cook=_entities.Entities<TokayCharacter>().Single(n=>n.Record.SubId==5);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(e=>ReferenceEquals(e.Node,cook)));
            _player.WarpTo(new(72,88));_player.BeginCutsceneControl();
            _runtimeState.SetWramByte(0xcc8a,1);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,72,88);
            int at=0xd040+_entities.InteractionSlot(cook)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=5;
            native[at+11]=40;native[at+13]=72;native.AdvanceInteractions();
            // Declare the script's native-tail activation, then hold TX_0a05.
            // Original script dispatch blocks; always-update$48:$05 still
            // executes its six-path native tail under text/masks/scroll.
            cooking.WriteObjectByte("Cook",0x3f,1);native[at+0x3f]=1;
            _dialogue.ShowMessage("Cook native loop.",88);native[0xcba0]=1;
            _dialogue.RevealCurrentPageForValidation();
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();
            int sourceSoundStart=native.Sounds.Count,updates=0,returns=0;
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false;
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                armed=false;
            }
            _runtimeState.Changed+=Capture;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++)
                {observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
                armed=true;
            }));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                int oldAway=native[at+0x3e];native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(oldAway!=0&&native[at+0x3e]==0)returns++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                FailIf(SomariaPrivate<Vector2>(cooking,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(cooking,"_z")!=unchecked((short)native.Word(at+14))||
                    SomariaPrivate<int>(cooking,"_speedZ")!=unchecked((short)native.Word(at+0x14))||
                    SomariaPrivate<int>(cooking,"_jumpState")!=native[at+5]||
                    SomariaPrivate<int>(cooking,"_jumpIndex")!=native[at+0x3b]||
                    SomariaPrivate<bool>(cooking,"_awayFromStart")!=(native[at+0x3e]!=0)||!observed.SequenceEqual(expected),
                    $"Cook native loop update{updates}: original fractional jump, return handshake or publication differs (XY{SomariaPrivate<Vector2>(cooking,"_position")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{SomariaPrivate<int>(cooking,"_jumpState")}/{native[at+5]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}]).");
                FailIf(!sounds.Requests.SequenceEqual(native.Sounds.Skip(sourceSoundStart)),
                    $"Cook update{updates} native loops must retain source jump-sound order across holds and exact returns: runtime[{string.Join(',',sounds.Requests)}], native[{string.Join(',',native.Sounds.Skip(sourceSoundStart))}].");
                armed=false;
            });
            Step(40);_runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(40);
            _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(40);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(392);
            FailIf(returns<2,"Cook fixture must return from at least two complete six-jump loops.");
            _runtimeState.Changed-=Capture;_dialogue.Close();cooking.Cancel();_entities.Clear();_player.EndCutsceneControl();
        }
    }
}

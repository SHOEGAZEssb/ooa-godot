using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePatchCartPublicationRom()
    {
        // patch_subid02's independent four track values, special packed$15
        // override, and missing lookup key. Inputs declare a moving cart;
        // manager/broken-item creation has separate actual-game coverage.
        var targets=new (int Packed,int Tile,int Angle)[]{(0x44,0x5c,16),(0x44,0x5a,24),(0x44,0x5b,0),
            (0x44,0x59,8),(0x15,0xff,8),(0x44,0xff,-1)};
        foreach(var target in targets)
        foreach(int incoming in new[]{0,8,16,24})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_runtimeState.SetWramByte(0xcfd2,0);LoadValidationRoom(5,0xe8);
            var owner=_roomEvents.Get<PatchEvent>();var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record is{Id:0x94,SubId:2});
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(entity=>ReferenceEquals(entity.Node,actor)));
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,24,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x94;native[at+2]=2;native[at+11]=8;native[at+13]=104;native.AdvanceInteractions();
            for(int slot=0;slot<16;slot++){int other=0xd040+slot*256;if(other!=at)native[other]=0;}
            Vector2 center=new((target.Packed&15)*16+8,(target.Packed>>4)*16+8);
            Vector2 initial=center-(Vector2)OracleObjectMath.StrictCardinalVector(incoming);
            if(fractional)initial+=new Vector2(0.25f,0.5f);
            F1Set(owner,"_cartPosition",initial);actor.Position=initial.Floor();F1Set(owner,"_cartAngle",incoming);F1Set(owner,"_cartState",2);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native[at+4]=2;native[at+9]=(byte)incoming;
            _currentRoom.SetPositionTileAndCollision(center,(byte)target.Tile,null,0);native[0xcf00+target.Packed]=(byte)target.Tile;
            foreach(int address in new[]{0xcfd4,0xcfd5,0xcfd6}){_runtimeState.SetWramByte(address,(byte)(address==0xcfd4?1:0));native[address]=(byte)(address==0xcfd4?1:0);}
            int updates=0,movements=0,publications=0;bool observing=false;byte[] observed=new byte[4];
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;publications++;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool moving=native[at+4]==2&&native[0xcfd4]!=0&&native[0xcfd5]==0&&native[0xcba0]==0&&native[0xcd00]!=8&&(native[0xcc8a]&2)==0;
                native.AdvanceInteractions(_entities.FrameCounter);updates++;if(moving)movements++;
                FailIf(SomariaPrivate<Vector2>(owner,"_cartPosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||SomariaPrivate<int>(owner,"_cartState")!=native[at+4]||
                    SomariaPrivate<int>(owner,"_cartAngle")!=native[at+9]||actor.CurrentAnimationCounter!=native[at+0x20]||
                    !actor.Visible||moving&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||!moving&&!observing||
                    Enumerable.Range(1,3).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Patch cart tile${target.Tile:x2}/packed${target.Packed:x2} incoming${incoming:x2} update{updates}: native movement/turn/animation/gate/publication differs.");
                Seed();
            });
            _runtimeState.Changed+=Observe;
            try
            {
                Seed();Step(1);
                FailIf(native[at+9]!=(target.Angle<0?incoming:target.Angle),"Original Patch track lookup/packed$15 override changed.");
                Step(3);
                _dialogue.ShowMessage("Ordinary Patch cart hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
                if(!fractional)
                {
                    // Production initialization and SPEED_100 retain zero
                    // low bytes; only that reachable reset boundary is claimed.
                    _runtimeState.SetWramByte(0xcfd5,1);native[0xcfd5]=1;Step(3);
                    _runtimeState.SetWramByte(0xcfd6,1);native[0xcfd6]=1;Step(1);
                }
                else{_runtimeState.SetWramByte(0xcfd4,0);native[0xcfd4]=0;Step(1);}
                Step(1);
                _runtimeState.SetWramByte(0xcfd4,0);native[0xcfd4]=0;Step(2);
                FailIf(movements!=4||publications!=4||SomariaPrivate<int>(owner,"_cartState")!=1,
                    "Patch cart must publish four moves, then honor failure/fade or game stop and return to waiting.");
            }
            finally{_runtimeState.Changed-=Observe;}
            owner.Cancel();_entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled||owner.HasState,"Patch cart cancellation must release its owner.");
        }
    }
}

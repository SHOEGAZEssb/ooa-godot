using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayTheftMovementRom()
    {
        foreach(string mode in new[]{"plain","holds","late"})
        foreach(bool batched in new[]{false,true})
        {
            bool paused=mode=="holds",late=mode=="late";
            ReinitializeGameplayForValidation();
            _sound.AttachPlayRequestAudit();
            foreach(int treasure in new[]{0x05,0x15,0x11,0x2e,0x19,0x01,0x03,0x16,0x17})
                _inventory.GiveTreasure(treasure,treasure==3?0x10:1);
            LoadValidationRoom(1,0xaa);_inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,88));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            var owner=_roomEvents.Get<TokayTheftEvent>();
            var actors=_entities.Entities<NpcCharacter>().Where(actor=>actor.Record is{Id:0x48,SubId:<5}).OrderBy(actor=>actor.Record.SubId).ToArray();
            FailIf(actors.Length!=5,"Room$1:$aa must retain five ordered Tokay thief actors.");
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,40,88)
                {CompanionDispatchEnabled=true};
            var source=new (int X,int Y)[]{(0x18,0x48),(0x38,0x58),(0x28,0x58),(0x18,0x58),(0x38,0x48)};
            int[] slots=actors.Select(actor=>0xd040+_entities.InteractionSlot(actor)*256).ToArray();
            for(int sub=0;sub<5;sub++)
            {
                int at=slots[sub];native[at]=1;native[at+1]=0x48;native[at+2]=(byte)sub;
                native[at+11]=(byte)source[sub].Y;native[at+13]=(byte)source[sub].X;
            }
            // Declared original state0 initialization before compared script
            // updates. The native Link override is adopted on its next pass.
            native.AdvanceInteractions();owner.UpdateFrame();
            int[] soundIds=[SoundId.SndJump,SoundId.SndLand,SoundId.SndStrike,SoundId.SndUnknown5,SoundId.SndGetItem];
            var sounds=soundIds.ToDictionary(sound=>sound,sound=>(Port:_sound.PlayRequestsFor(sound),Rom:native.Sounds.Count(value=>value==sound)));
            var states=owner.Thieves.ToArray();
            var publication=new ScriptMovementPublicationTrace(_runtimeState,source=>
                source.Opcode=="applyspeed"&&owner.UpdatingThief is{} thief
                    ?(thief.Runner.ActorSpeed("Thief"),thief.Runner.ActorAngle("Thief"),thief.Position):null,
                lane:()=>owner.UpdatingThief!);
            _roomEvents.CommandTraceSink=publication;
            int updates=0,hold=0,holdKind=0,plan=0,lateHold=0,lateCount=0;
            bool lateWait=false,lateMove=false,lateExit=false,injecting=false;
            const int writer=0xd140;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                if(!late||lateHold!=0||native[0xcba0]!=0)return;
                bool inject=false;
                if(!lateWait&&native[slots[2]+6]==0x90){lateWait=true;inject=true;}
                if(!lateMove&&native[slots[0]+7]==8){lateMove=true;inject=true;}
                if(!lateExit&&native[slots[0]+5]==3){lateExit=true;inject=true;}
                if(!inject)return;
                native[writer]=1;native[writer+1]=0x3c;native[writer+2]=1;native[writer+4]=1;
                native.Word(writer+0x18,0xc180);native[0xc180]=0x98;native[0xc181]=0x57;native[0xc182]=3;
                _dialogue.ShowMessage("Earlier interaction opens text.",_player.Position.Y);
                injecting=true;lateHold=3;lateCount++;
            }));
            while((native[0xc8aa]&0x40)==0&&updates<1000)
                StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.UpdateGameplay(0,0,0xff,_entities.FrameCounter);updates++;
                    FailIf(SomariaPrivate<int>(_player,"_topDownAirZFixed")!=unchecked((short)native.Word(0xd00e)),
                        $"Tokay Link jump update{updates}: shared Link gravity/phase differs ({SomariaPrivate<int>(_player,"_topDownAirZFixed")}/{unchecked((short)native.Word(0xd00e))}).");
                    for(int sub=0;sub<5;sub++)
                    {
                        int at=slots[sub];
                        var state=states[sub];
                        int page=at-0x40;
                        int c1=state.Stage==0?state.Runner.NativeCounter1:state.Counter;
                        FailIf(actors[sub].Active!=(native[at]!=0)||actors[sub].Active&&(
                            state.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            state.Stage!=native[at+5]||c1!=native[at+6]||
                            state.Runner.MovementCounter("Thief")!=native[at+7]||
                            state.ZFixed!=unchecked((short)native.Word(at+14))||state.SpeedZ!=unchecked((short)native.Word(at+0x14))||
                            actors[sub].CurrentAnimationCounter!=native[at+0x20]||actors[sub].CurrentAnimationParameter!=native[at+0x21]),
                            $"Tokay thief$48:${sub:x2} update{updates}: original independent script/native continuation differs (XY={state.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state={state.Stage}/{native[at+5]}, counter={c1}/{native[at+6]}, animation={actors[sub].CurrentAnimationCounter}/{native[at+0x20]}).");
                        FailIf(new[]{0x20,0x21,0x22,0x23,0x30,0x31}.Any(offset=>
                            _runtimeState.ReadWramByte(page+offset)!=native[page+offset]),
                            $"Tokay thief$48:${sub:x2} update{updates}: specialObject animation ABI page-prefix writes differ.");
                    }
                    foreach(int sound in soundIds)
                        FailIf(_sound.PlayRequestsFor(sound)-sounds[sound].Port!=native.Sounds.Count(value=>value==sound)-sounds[sound].Rom,
                            $"Tokay update{updates}: original sound${sound:x2} order/cadence differs.");
                    if(SomariaPrivate<int>(owner,"_linkWait")!=0)
                        FailIf(owner.LinkFrameCounter!=native[0xd020],$"Link's independent washed-up animation clock differs at update{updates}: {owner.LinkFrameCounter}/{native[0xd020]}, ID/state=${native[0xd001]:x2}/${native[0xd004]:x2}, wait={SomariaPrivate<int>(owner,"_linkWait")}/{native[0xd006]}.");
                    if(injecting){native[writer]=0;injecting=false;}
                    if(lateHold!=0&&--lateHold==0){_dialogue.Close();native[0xcba0]=0;}
                    if(paused)
                    {
                        if(hold!=0)
                        {
                            if(--hold==0)
                            {
                                if(holdKind==0){_dialogue.Close();native[0xcba0]=0;}
                                else if(holdKind==1){_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;}
                                else {_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;}
                            }
                        }
                        else if(plan switch
                        {
                            0=>updates==50,1=>updates==90,2=>updates==120,
                            3=>unchecked((short)native.Word(0xd00e))<0,
                            4=>native[slots[0]+7] is >=8 and <=10,
                            5=>native[slots[0]+9]==8&&native[slots[0]+7] is >=8 and <=10,
                            6=>native[slots[0]+5]==3,_=>false
                        })
                        {
                            holdKind=plan%3;plan++;hold=3;
                            if(holdKind==0){_dialogue.ShowMessage("Tokay ordinary-handler pause.",_player.Position.Y);native[0xcba0]=1;}
                            else if(holdKind==1){_runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;}
                            else {_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
                        }
                    }
                });
            FailIf(updates>=1000||owner.HasState||publication.MotionVisits!=5*(15+31+31)||paused&&plan!=7||late&&lateCount!=3,
                $"Tokay theft must complete all fifteen original script movements and staggered native exits (updates={updates}, active={owner.HasState}, script publications={publication.MotionVisits}, flags=${native[0xc8aa]:x2}).");
            _roomEvents.CommandTraceSink=null;
            owner.Cancel();_entities.Clear();
        }
    }
}

using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerEndingOutdoorRom()=>ValidateBlackTowerEndingRom(palace:false);
    private void ValidateBlackTowerEndingPalaceRom()=>ValidateBlackTowerEndingRom(palace:true);
    private void ValidateBlackTowerEndingFarewellRom()=>ValidateBlackTowerEndingRom(palace:true,farewell:true);
    private void ValidateBlackTowerEndingRom(bool palace,bool farewell=false)
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0xd4);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,136));_transitions.ResetCamera();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,120,136)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom[0xc2ef]=9;rom[0xcc03]=1;
            for(int address=0xcbb3;address<0xcbc3;address++)rom[address]=0;
            rom[0xcd16]=0;rom[0xcd17]=0xd0;
            if(farewell)
            {
                // Independent state-$0f fixture: both restoration vignettes
                // have published completion; the next real gameplay update
                // loads the final palace actor set and enables normal Link.
                typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
                foreach(var pair in new[]{(0xcbc2,15),(0xcfde,10),(0xcfd0,10),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
                {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            }
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0,texts=0;
            int terminalState=farewell?0x11:palace?0x0d:9;
            int[] expectedTexts=farewell?[0x2e14,0x1710,0x2e15,0x1711,0x210d]:
                palace?[0x210a,0x210b,0x2e11,0x210c,0x2e12,0x170e,0x2e13,0x170f]:[0x210a,0x210b,0x2e11,0x210c];
            bool sawDebris=false,sawJump=false,sawInput=false;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_rooms.CurrentRoom.Id!=rom[0xcc30]||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible!=((rom[0xd01a]&128)!=0)||
                    _player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Black Tower ending outdoor update{update}, batch={batched}: state={owner.State}/{rom[0xcbc2]}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}/{rom[0xcc2d]:x}:{rom[0xcc30]:x2}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, visible={_player.Visible}/{rom[0xd01a]:x2}, keys={typeof(Input).GetField("_gameKeys",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)}/{rom[0xcc29]:x2}, counter={_runtimeState.ReadWramByte(0xcbc4)}/{rom[0xcbc4]}, mask={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{rom[WramAddress.wDisabledObjects]:x2}, native speed={rom[0xd010]:x2}, walls={rom[0xd022]:x2}; controls={SomariaPrivate<object>(_player,"_scriptControl")}/{SomariaPrivate<int>(_player,"_forcedState08Phase")}, disabled={SomariaPrivate<IPlayerWorld>(_player,"_world").MovementDisabled}, collideDown={_collision.Collides(_player.Position+Vector2.Down)}, nativeLinkState={rom[0xd004]}:{rom[0xd005]}.");
                foreach(int address in new[]{0xcbb3,0xcbb6,0xcfd0,0xcbc3,0xcbc4,0xcbc5,0xcbc9,WramAddress.wDisabledObjects,WramAddress.wMenuDisabled})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Ending byte ${address:x4} update{update}: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                foreach(var actor in _entities.EntityAdapters<NayruRescueActorRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.Id!=rom[at+1]||actor.SubId!=rom[at+2]||actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||actor.ZFixed!=unchecked((short)rom.Word(at+14))||
                        actor.Actor.CurrentAnimationCounter!=rom[at+0x20]||actor.Actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a])||
                        actor.Node.Visible!=((rom[at+0x1a]&128)!=0),
                        $"Ending companion ${actor.Id:x2}:{actor.SubId:x2} update{update}: port={actor.State}:{actor.Substate}/{actor.PrecisePosition}/{actor.ZFixed}/anim{actor.Actor.CurrentAnimationCounter}, ROM={rom[at+4]}:{rom[at+5]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{unchecked((short)rom.Word(at+14))}/anim{rom[at+0x20]}.");
                    sawJump|=actor.ZFixed!=0;
                }
                foreach(var debris in _entities.EntityAdapters<ExplosionWithDebrisRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(debris.Node)*256;
                    FailIf(rom[at+1]!=0x99||debris.SubId!=rom[at+2]||debris.Var03!=rom[at+3]||debris.State!=rom[at+4]||
                        debris.Angle!=rom[at+9]||debris.Speed!=rom[at+16]||debris.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        debris.ZFixed!=unchecked((short)rom.Word(at+14))||debris.SpeedZ!=unchecked((short)rom.Word(at+20))||debris.AnimationParameter!=rom[at+0x21],
                        $"Ending $99:{debris.SubId} slot${at:x4} update{update}: port={debris.State}/{debris.PrecisePosition}/{debris.ZFixed},{debris.SpeedZ}/{debris.Angle},{debris.Speed}/param{debris.AnimationParameter}; ROM={rom[at+4]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/{unchecked((short)rom.Word(at+14))},{unchecked((short)rom.Word(at+20))}/{rom[at+9]},{rom[at+16]}/param{rom[at+0x21]}.");
                    sawDebris|=debris.SubId==1;
                }
                foreach(var actor in _entities.EntityAdapters<BlackTowerEndingActor>())
                {
                    sawJump|=actor.ZFixed!=0;
                    int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
                    FailIf(actor.Id!=rom[at+1]||actor.SubId!=rom[at+2]||actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        actor.Node.Position!=new Vector2(rom[at+13],rom[at+11])||actor.ZFixed!=unchecked((short)rom.Word(at+14))||
                        actor.Actor.CurrentAnimationCounter!=rom[at+0x20]||actor.Actor.CurrentAnimationParameter!=rom[at+0x21]||
                        actor.Actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a])||actor.Node.Visible!=((rom[at+0x1a]&128)!=0)||
                        actor.Id==0x40&&actor.Direction!=rom[at+8],
                        $"Ending palace ${actor.Id:x2}:{actor.SubId:x2} update{update} slot${at:x4}: port={actor.State}:{actor.Substate}/{actor.Node.Position}/Z{actor.ZFixed}/anim{actor.Actor.CurrentAnimationCounter}/direction{actor.Direction}/priority{actor.Actor.ZIndex}, ROM={rom[at+4]}:{rom[at+5]}/{rom[at+13]},{rom[at+11]}/Z{unchecked((short)rom.Word(at+14))}/anim{rom[at+0x20]}/direction{rom[at+8]}/visible${rom[at+0x1a]:x2}.");
                }
                if(owner.State>=0x0a)
                    FailIf(_entities.EntityAdapters<BlackTowerEndingActor>().Count()!=6||Enumerable.Range(0,16).Any(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0x31),
                        "Normal-game palace actors must delete Impa $31:$09 while retaining Ambi and all five guards in source order.");
                int count=Enumerable.Range(0,16).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==0x99);
                FailIf(count!=_entities.EntityAdapters<ExplosionWithDebrisRoomEntity>().Count(),"Native explosion/debris allocations must retain every slot and deletion.");
                for(int packed=0;packed<128;packed++)
                {
                    if((packed&15)>=10)continue;Vector2 point=new((packed&15)*16+8,(packed&0xf0)+8);
                    FailIf(_currentRoom.GetMetatile(point)!=rom[0xcf00+packed],$"Ending room tile ${packed:x2} differs at update{update}.");
                }
                if(generation!=rom.TextGeneration)
                {
                    FailIf(texts>=expectedTexts.Length||rom.Word(0xcba2)!=expectedTexts[texts]||!_dialogue.IsOpen,
                        $"Ending outdoor dialogue {texts} differs: native TX${rom.Word(0xcba2):x4}.");
                    generation=rom.TextGeneration;texts++;hold=3;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                var random=_random.CaptureState();FailIf(random.Rng1!=rom[0xff94]||random.Rng2!=rom[0xff95]||random.Calls-seed.Calls!=rom.RandomCalls||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Ending outdoor RNG/SFX update{update}: calls={random.Calls-seed.Calls}/{rom.RandomCalls}, SFX={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                sawInput|=rom[0xcbc3]!=0;
            }
            bool sawTerminalInput=false;
            while(update<2600&&owner.State!=terminalState)
            {
                if(owner.State==8&&!sawTerminalInput)
                {
                    // Freeze only interactions while Link's real input stream
                    // finishes its 33-down/256-idle/$ffff tail. The scene waits
                    // on Ralph's frozen signal, so no extra progression runs.
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects,2);rom[WramAddress.wDisabledObjects]=2;
                    StepGameplayUpdates(340,Vector2.Zero,batched:batched,afterUpdate:Compare);
                    FailIf(rom[0xcbc3]!=255||rom.Word(0xcbc4)!=65535||rom[0xcc29]!=0,
                        "The terminal native input counter must retain idle keys without wrapping its cursor or counter.");
                    sawTerminalInput=true;_runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[WramAddress.wDisabledObjects]=0;
                }
                else StepGameplayUpdates(texts>=expectedTexts.Length?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            FailIf(owner.State!=terminalState||texts!=expectedTexts.Length||!sawInput||!sawJump||!farewell&&!sawDebris,
                $"Ending scene must reach its next native fade with ordered dialogue and actor effects: farewell={farewell}, palace={palace}, state=${owner.State:x2}, texts={texts}, input={sawInput}, jump={sawJump}, debris={sawDebris}, updates={update}.");
            owner.Cancel();LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(owner.HasState||_transitions.NativePaletteFadeActive||_entities.PlayerMenusDisabled||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_entities.EntityAdapters<BlackTowerEndingActor>().Any()||
                _entities.EntityAdapters<ExplosionWithDebrisRoomEntity>().Any(),
                "Cancelling the outdoor ending must retire actors/input/fade before another room can run.");
        }
    }
}

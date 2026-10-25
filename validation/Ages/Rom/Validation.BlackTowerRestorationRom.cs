using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerRestoredRabbitsRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x49);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x60,0x48));
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x60,0x48)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom[0xc2ef]=9;rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xcbc2,13),(0xcfde,9),(0xcfd0,10),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0;bool sawJump=false,sawClink=false,sawBounce=false,sawDeparture=false;
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=1||_rooms.CurrentRoom.Id!=0x84||rom[0xcc2d]!=1||rom[0xcc30]!=0x84||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||rom[0xd000]!=0,
                    $"Restored rabbits object-set $09 update{update}: state={owner.State}/{rom[0xcbc2]}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                foreach(int address in new[]{0xcfd0,0xcfd1,0xcfde,0xcfdf,0xcbc3,0xcbc4,0xcbc5,0xcbc9,AgesWramAddress.wScrollMode,AgesWramAddress.wLoadedTreeGfxIndex,AgesWramAddress.wInteractionIDToLoadExtraGfx})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Restored rabbits byte ${address:x4} update{update}: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                foreach(var actor in _entities.EntityAdapters<RestoredRabbitRoomEntity>())
                {
                    int slot=_entities.InteractionSlot(actor.Node),at=0xd040+slot*256;
                    FailIf(rom[at]==0||rom[at+1]!=0x4b||actor.SubId!=rom[at+2]||actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        (actor.SubId!=4||actor.Substate!=2)&&actor.Counter!=(actor.SubId==5&&actor.Substate==0?rom.Word(at+6):rom[at+6])||
                        actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        actor.ZFixed!=unchecked((short)rom.Word(at+14))||actor.SpeedZ!=unchecked((short)rom.Word(at+20))||
                        actor.Speed!=rom[at+16]||actor.Angle!=rom[at+9]||actor.CollisionCounter!=rom[at+0x38]||actor.Palette!=rom[at+0x1c]||
                        actor.Actor.CurrentAnimationCounter!=rom[at+0x20]||actor.Actor.CurrentAnimationParameter!=rom[at+0x21]||
                        actor.Actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                        $"Restored rabbit $4b:{actor.SubId:x2} slot${at:x4} update{update}: port={actor.State}:{actor.Substate}/{actor.Counter}/XY{actor.PrecisePosition}/Z{actor.ZFixed},{actor.SpeedZ}/speed{actor.Speed},angle{actor.Angle}/pal{actor.Palette}/hit{actor.CollisionCounter}/anim{actor.Actor.CurrentAnimationCounter},{actor.Actor.CurrentAnimationParameter}; ROM={rom[at+4]}:{rom[at+5]}/{rom.Word(at+6)}/XY{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/Z{unchecked((short)rom.Word(at+14))},{unchecked((short)rom.Word(at+20))}/speed{rom[at+16]},angle{rom[at+9]}/pal{rom[at+0x1c]}/hit{rom[at+0x38]}/anim{rom[at+0x20]},{rom[at+0x21]}.");
                    sawJump|=actor.ZFixed!=0;sawBounce|=actor.SubId==5&&actor.Substate==5;
                }
                int NativeCount(int id)=>Enumerable.Range(0,16).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==id);
                int rabbitCount=_entities.EntityAdapters<RestoredRabbitRoomEntity>().Count();sawDeparture|=rabbitCount<4;
                FailIf(NativeCount(0x4b)!=rabbitCount||NativeCount(7)!=_entities.EntityAdapters<SwordBeamClinkRoomEntity>().Count(),
                    "Restored rabbits must preserve source departures and checked native $07:$80 allocations.");
                foreach(var clink in _entities.EntityAdapters<SwordBeamClinkRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(clink.Node)*256;var effect=(ClinkEffect)clink.Node;
                    FailIf(rom[at+2]!=0x80||effect.Position!=new Vector2(rom[at+13],rom[at+11])||unchecked((byte)effect.ZHigh)!=rom[at+15]||
                        effect.Visible!=((rom[at+0x1a]&128)!=0)||effect.AnimationParameter!=rom[at+0x21],
                        $"Rabbit impact $07:$80 update{update}: port={effect.Position}/Z{effect.ZHigh}/param{effect.AnimationParameter}, ROM={rom[at+13]},{rom[at+11]}/Z{rom[at+15]}/param{rom[at+0x21]}.");sawClink=true;
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds),$"Restored rabbits sound/RNG update{update}: {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
            while(update<800&&_runtimeState.ReadWramByte(0xcfdf)!=255)
            {
                var rabbit=_entities.EntityAdapters<RestoredRabbitRoomEntity>().FirstOrDefault(actor=>actor.SubId==5);
                StepGameplayUpdates(rabbit is {Substate:5,Counter:<=4}?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            FailIf(!sawJump||!sawClink||!sawBounce||!sawDeparture||rom[0xcfdf]!=255,
                "The native rabbit restoration must reach its checked impact, bounces, departures and final cfdf=$ff signal.");
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(owner.State!=15||_runtimeState.ReadWramByte(0xcfde)!=10,"The final restoration must select object set $0a before the return to Ambi's palace.");
            while(owner.State==15&&update<900)
                StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
                {
                    rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                    rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                    FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=rom[0xcc2d]||_rooms.CurrentRoom.Id!=rom[0xcc30]||
                        _runtimeState.ReadWramByte(0xcfd0)!=rom[0xcfd0]||_runtimeState.ReadWramByte(0xcfd1)!=rom[0xcfd1]||
                        _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                        $"Restored rabbit/palace handoff update{update}: native09 state{owner.State}/{rom[0xcbc2]} differs.");
                });
            FailIf(owner.State!=16||_rooms.ActiveGroup!=1||_rooms.CurrentRoom.Id!=0x65||_player.Position!=new Vector2(0x60,0x48)||!_player.Visible||
                _runtimeState.ReadWramByte(0xcfd0)!=11||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Count()!=2||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Single(actor=>actor.Id==0x36).Node.Position!=new Vector2(0x40,0x48)||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Single(actor=>actor.Id==0x37).Node.Position!=new Vector2(0x50,0x48)||
                _entities.EntityAdapters<BlackTowerEndingActor>().Count()!=6||_entities.EntityAdapters<RestoredRabbitRoomEntity>().Any(),
                "After restoration the source room/object-set $0a must initialize both farewell scripts, delete Impa, and initialize Link once in room $1:65.");
            owner.Cancel();LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(owner.HasState||_entities.EntityAdapters<RestoredRabbitRoomEntity>().Any(),"Cancellation must retire all restoration rabbits.");
        }
    }
    private void ValidateBlackTowerRestoredFamilyRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x60,0x48));
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x60,0x48)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom[0xc2ef]=9;rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xcbc2,13),(0xcfde,8),(0xcfd0,10),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            int update=0;bool sawJump=false,sawAccessory=false,sawBall=false,sawRestoration=false;
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateGameplay(0,0,255,++update);
                rom.AdvanceTileGraphics();rom.AdvanceScreenShake();rom.AdvanceCameraPosition();
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=1||_rooms.CurrentRoom.Id!=0x49||rom[0xcc2d]!=1||rom[0xcc30]!=0x49||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||rom[0xd000]!=0,
                    $"Restored family native object-set $08 update{update}: state={owner.State}/{rom[0xcbc2]}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}, Linkvisible={_player.Visible}.");
                foreach(int address in new[]{0xcfd0,0xcfd1,0xcfd3,0xcfde,0xcfdf,0xcbc3,0xcbc4,0xcbc5,0xcbc9,AgesWramAddress.wScrollMode})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Restored family byte ${address:x4} update{update}: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                foreach(var actor in _entities.EntityAdapters<RestoredFamilyRoomEntity>())
                {
                    int slot=_entities.InteractionSlot(actor.Node),at=0xd040+slot*256;
                    FailIf(slot!=(actor.Id switch {0x3c=>2,0x3a=>3,0x43=>4,_=>-1})||rom[at]==0||actor.Id!=rom[at+1]||actor.SubId!=rom[at+2]||
                        actor.State!=rom[at+4]||actor.Substate!=rom[at+5]||
                        (actor.Id==0x43||actor.Id==0x3a&&actor.Substate<2||actor.Id==0x3c&&actor.Substate<5)&&actor.Counter!=(actor.Id==0x43?rom.Word(at+6):rom[at+6])||
                        actor.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        actor.ZFixed!=unchecked((short)rom.Word(at+14))||actor.SpeedZ!=unchecked((short)rom.Word(at+20))||
                        actor.Var38!=rom[at+0x38]||actor.Var39!=rom[at+0x39]||actor.Palette!=rom[at+0x1c]||
                        actor.Actor.CurrentAnimationCounter!=rom[at+0x20]||actor.Actor.CurrentAnimationParameter!=rom[at+0x21]||
                        actor.Actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                        $"Restored family ${actor.Id:x2}:{actor.SubId:x2} slot${at:x4} update{update}: port={actor.State}:{actor.Substate}/{actor.Counter}/XY{actor.PrecisePosition}/Z{actor.ZFixed},{actor.SpeedZ}/pal{actor.Palette}/var{actor.Var38},{actor.Var39}/anim{actor.Actor.CurrentAnimationCounter},{actor.Actor.CurrentAnimationParameter}; ROM={rom[at+4]}:{rom[at+5]}/{rom.Word(at+6)}/XY{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/Z{unchecked((short)rom.Word(at+14))},{unchecked((short)rom.Word(at+20))}/pal{rom[at+0x1c]}/var{rom[at+0x38]},{rom[at+0x39]}/anim{rom[at+0x20]},{rom[at+0x21]}.");
                    sawJump|=actor.ZFixed!=0;sawRestoration|=actor.Id==0x3a&&actor.Palette==1;
                }
                foreach(var accessory in _entities.EntityAdapters<AccessoryRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(accessory.Node)*256;var actor=(NpcCharacter)accessory.Node;
                    FailIf(rom[at]==0||rom[at+1]!=0x63||rom[at+2]!=0x3f||accessory.State!=rom[at+4]||
                        actor.Position!=new Vector2(rom[at+13],rom[at+11])||actor.CurrentAnimationCounter!=rom[at+0x20]||
                        actor.CurrentAnimationParameter!=rom[at+0x21]||actor.Visible!=((rom[at+0x1a]&128)!=0)||actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[at+0x1a]),
                        $"Restored carried ball $63:$3f update{update}: port={actor.Position}/{actor.CurrentAnimationCounter},{actor.CurrentAnimationParameter}/visible{actor.Visible}; ROM={rom[at+13]},{rom[at+11]}/{rom[at+0x20]},{rom[at+0x21]}/visible${rom[at+0x1a]:x2}.");
                    sawAccessory=true;
                }
                foreach(var adapter in _entities.EntityAdapters<NativeBallRoomEntity>())
                {
                    int at=0xd040+_entities.InteractionSlot(adapter.Node)*256;var ball=(Room149Ball)adapter.Node;
                    FailIf(rom[at]==0||rom[at+1]!=0x95||ball.SubId!=rom[at+2]||adapter.State!=rom[at+4]||
                        (ball.Stopped?2:ball.Idle?0:1)!=rom[at+5]||ball.Position!=new Vector2(rom[at+13],rom[at+11])||
                        ball.ZFixed!=unchecked((short)rom.Word(at+14))||ball.SpeedZ!=unchecked((short)rom.Word(at+20)),
                        $"Restored thrown ball $95 update{update}: port={ball.SubId}/{ball.Idle}/{ball.Position}/Z{ball.ZFixed},{ball.SpeedZ}; ROM={rom[at+2]}:{rom[at+5]}/{rom[at+13]},{rom[at+11]}/Z{unchecked((short)rom.Word(at+14))},{unchecked((short)rom.Word(at+20))}.");
                    sawBall=true;
                }
                int NativeCount(int id)=>Enumerable.Range(0,16).Count(slot=>rom[0xd040+slot*256]!=0&&rom[0xd041+slot*256]==id);
                FailIf(NativeCount(0x63)!=_entities.EntityAdapters<AccessoryRoomEntity>().Count()||NativeCount(0x95)!=_entities.EntityAdapters<NativeBallRoomEntity>().Count()||
                    NativeCount(0x9f)!=_entities.EntityAdapters<ExclamationMarkRoomEntity>().Count(),"Restored family must preserve checked accessory/ball/exclamation allocation and retirement.");
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls)||
                    !sounds.Requests.SequenceEqual(rom.Sounds),$"Restored family sound/RNG update{update}: {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
            while(update<1000&&_runtimeState.ReadWramByte(0xcfdf)!=255)
            {
                var observer=_entities.EntityAdapters<RestoredFamilyRoomEntity>().FirstOrDefault(actor=>actor.Id==0x43);
                StepGameplayUpdates(observer is {Substate:2,Counter:<=4}?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            FailIf(!sawJump||!sawAccessory||!sawBall||!sawRestoration||rom[0xcfdf]!=255||_runtimeState.ReadWramByte(0xcfdf)!=255,
                "Native restoration must complete its palette/jump/carry/throw sequence before observer $43:$05 publishes cfdf=$ff.");
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:Compare);
            FailIf(owner.State!=13||_runtimeState.ReadWramByte(0xcfde)!=9,"Restoration completion must select the next object set and begin its fade before loading it.");
            owner.Cancel();LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(owner.HasState||_entities.EntityAdapters<RestoredFamilyRoomEntity>().Any()||_entities.EntityAdapters<NativeBallRoomEntity>().Any()||_entities.EntityAdapters<AccessoryRoomEntity>().Any(),
                "Cancelling restoration must retire native family actors and both accessory types before the replacement room.");
        }
    }
}

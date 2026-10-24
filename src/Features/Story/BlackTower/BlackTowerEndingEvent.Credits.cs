using System;

namespace oracleofages;

internal sealed partial class BlackTowerEndingEvent
{
    private int? _healthBeforeCredits;
    private int _ringBeforeCredits;
    private CreditsHorizontalDatabase? _horizontalCredits;
    private CreditsVerticalDatabase? _verticalCredits;
    private void LoadCreditsMakuPast()
    {
        // pickaxeWorker.s: forceLoadRoom retains every live object/script.
        var room=context.Rooms.LoadCutsceneBackground(1,0x38);context.Entities.RebindCutsceneBackground(room);
        context.RoomView.SetRoom(room.Texture);context.Sound.SelectRoomMusic(1,0x38,context.Rooms.SaveData);
        Screens.ApplyPalette(0x30,context.Rooms.World.BackgroundPalettes,context.Entities);
        Screen.Load("credits-maku-past",room,context.RoomView);Write(WramAddress.wTilesetAnimation,255);
    }
    private void SetCreditsScrollY(int value)
    {Write(0xc486,value);context.RoomView.SetNativeScrollY(value);}
    private void BeginCreditsSword(Godot.Vector2 position)
    {
        context.Player.EndCutsceneControl();context.Player.WarpTo(position,recordSafe:false);
        context.Player.Face(Godot.Vector2I.Down);context.Player.Visible=true;context.Player.RequestNativeLinkInitialization();
        context.Inventory.SetScriptedEquippedItems(TreasureId.Sword,0);Write(WramAddress.wLinkObjectIndex,0xd0);
        Simulated.Install(Data.Input(5));Write(WramAddress.wDisabledObjects,0);_linkEnabled=true;
    }
    private bool DecrementWord(int address)
    {
        int value=Read(address)|(Read(address+1)<<8);
        if(value!=0)value--;
        Write(address,value);Write(address+1,value>>8);return value==0;
    }
    private void AdvanceCredits()
    {
        if(Stage==1){AdvanceCreditsScenes();return;}
        if(Stage==2){AdvanceCreditsScroll();return;}
        if(Stage==3){AdvanceCreditsFinale();return;}
        if(Stage!=0)throw new NotSupportedException($"endgameCutscenes.s: CUTSCENE_CREDITS $0a stage ${Stage:x2} continuation is not represented.");
        switch(State)
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                // endgameCutscenes.s:func_60e0. Inventory remains the owner
                // of health, equipped items and the disabled-ring bit.
                _healthBeforeCredits=context.Inventory.HealthQuarters;
                _ringBeforeCredits=context.Inventory.ActiveRing;
                context.Inventory.SetScriptedHealth(4);
                Write(0xcde3,context.Inventory.EquippedB);Write(0xcde4,context.Inventory.EquippedA);
                context.Inventory.SetActiveRingEnabled(false);
                State=1;Screen.SetOam(null);Screen.UseTitleMap();Write(0xcbb3,180);Write(0xcbb4,0);
                context.Sound.PlaySound(SoundId.MusCredits2);return;
            case 1:
                if(!DecrementWord(0xcbb3))return;
                State=2;Write(0xcbb3,0x48);Write(0xcbb4,3);
                Screens.ApplyPalette(4,context.Rooms.World.BackgroundPalettes,context.Entities);
                context.Transitions.BeginNativeBlackFade(false,delay:6);return;
            case 2:
                // This counter runs concurrently with the fade, unlike the
                // fade-gated byte counter in substate 0.
                if(!DecrementWord(0xcbb3))return;
                Write(0xcbc1,1);State=0;Write(0xcfde,context.Rooms.SaveData.IsLinkedGame?4:0);Write(0xcfdf,0);
                context.Transitions.BeginNativeWhiteFade(true);return;
            default:throw new NotSupportedException($"endgameCutscenes.s: CUTSCENE_CREDITS $0a stage0 state ${State:x2}.");
        }
    }
    private void AdvanceCreditsScroll()
    {
        if(context.Rooms.SaveData.IsLinkedGame&&State>=2){AdvanceLinkedStaffRoll();return;}
        // bank10.s:agesFunc_10_70f6. The normal staff roll's terminal hold
        // runs independently of fades; only the $af objects gate on palette.
        switch(State)
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=1;context.Entities.ClearDynamicInteractions();Write(0xcfde,0);
                Profile("credits-scroll",0xa0);Screen.SetScrollY(0x70);Write(0xc486,0x70);
                context.Transitions.BeginNativeWhiteFade(false);
                if(context.Entities.InteractionSlotAvailable)
                    context.Entities.AddEntity(new CreditsVerticalRoomEntity(context,_verticalCredits??=new(),Screen,position:new(0x50,0xe8)));
                return;
            case 1:
                if(Read(0xcfdf)==0)return;
                State=2;Write(0xcbb3,0xe0);Write(0xcbb4,1);return;
            case 2:
                if(!DecrementWord(0xcbb3))return;
                ClearTemporaryState();Write(0xcbc1,3);context.Transitions.BeginNativeWhiteFade(true,delay:4);return;
            default:throw new NotSupportedException($"bank10.s: normal credits staff roll substate ${State:x2}.");
        }
    }
    private void AdvanceCreditsScenes()
    {
        int index=Read(0xcfde);
        switch(State)
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=1;
                for(int address=0xcfc0;address<0xcfe0;address++)Write(address,0);
                Write(0xcfde,index);
                if(index>=4){LoadLinkedCreditsScene(index);context.Transitions.BeginNativeWhiteFade(false);return;}
                var row=Data.CreditsRoom(index);
                // forceLoadRoom calls loadTilesetGraphics/initializeAnimations
                // even if the preceding screen used the same animation group.
                Load(row.Group,row.Room,restartAnimations:true);
                Screens.ApplyPalette(0x0f,context.Rooms.World.BackgroundPalettes,context.Entities);
                Screens.ApplyPalette(row.Palette,context.Rooms.World.BackgroundPalettes,context.Entities);
                Screen.Load(index switch {0=>"credits-maku",1=>"credits-performance",2=>"credits-sword",_=>"credits-construction"},context.Rooms.CurrentRoom,context.RoomView,row.Uncmp);
                Write(WramAddress.wTilesetAnimation,index==0?255:1);
                if(index!=0)Screen.BeginNativeAnimation(context.AnimationTick());
                foreach(var actor in Data.Placements(row.Scene))
                {
                    switch(actor.Id)
                    {
                        case 0x31:context.Entities.AddEntity(new ImpaCreditsRoomEntity(context,Data,actor.Position,()=>flash(2)));break;
                        case 0x36:case 0x37:case 0x87:
                            context.Entities.AddEntity(new NayruRescueActorRoomEntity(context,_companions??=new(),actor.Id,actor.SubId,actor.Position));break;
                        case 0xa8:
                            if(actor.SubId==3){context.Entities.AddEntity(new CreditsCompanionLoaderRoomEntity(context,actor.Position));break;}
                            if(actor.SubId==4){context.Entities.AddEntity(new CreditsLinkLoaderRoomEntity(context.Player,this,actor.Position,BeginCreditsSword));break;}
                            if(actor.SubId!=0x64)throw new NotSupportedException($"Credits SPECIALOBJECT loader $a8:${actor.SubId:x2}.");
                            // This interaction writes the special-object slot and
                            // deletes itself when the interaction pass reaches it.
                            context.Entities.AddEntity(new CreditsLinkLoaderRoomEntity(context.Player,this,actor.Position));break;
                        case 0x6b:
                            if(actor.SubId==0x10)
                            {
                                context.Entities.AddEntity(new MakuStatueConstructionRoomEntity(context,Data,actor.Position,
                                    ()=>Screens.ApplyPalette(0xc8,context.Rooms.World.BackgroundPalettes,context.Entities),SetCreditsScrollY));break;
                            }
                            context.Entities.AddEntity(new MakuLinkStatueRoomEntity(Data.Actor(actor.Id,actor.SubId,actor.Position),new MakuSproutRoomDatabase(),
                                context.Rooms.CurrentRoom,context.AnimationTick,()=>
                                {
                                    Screens.ApplyPalette((context.Rooms.CurrentRoom.TilesetFlags&(int)TilesetFlags.Past)!=0?0xc7:0xc6,context.Rooms.World.BackgroundPalettes,context.Entities);
                                    return context.Entities.NativeObjectPalettes![6];
                                }));break;
                        case 0x39:context.Entities.AddEntity(new NayruRescueActorRoomEntity(context,_companions??=new(),actor.Id,actor.SubId,actor.Position));break;
                        case 0x4b:context.Entities.AddEntity(new RestoredRabbitRoomEntity(context,Data,_companions??=new(),actor.SubId,actor.Position));break;
                        case 0x3c:context.Entities.AddEntity(new RestoredFamilyRoomEntity(context,Data,_companions??=new(),actor.Id,actor.SubId,actor.Position));break;
                        case 0x4c:case 0x5d:context.Entities.AddEntity(new ForestAudienceRoomEntity(Data.Actor(actor.Id,actor.SubId,actor.Position),Data.Animation(actor.Id,0)));break;
                        case 0x4d:context.Entities.AddEntity(new BlackTowerEndingActor(context,Data,actor.Id,actor.SubId,actor.Position));break;
                        case 0x57:context.Entities.AddEntity(new PickaxeWorkerCreditsRoomEntity(context,Data,actor.SubId,actor.Position,LoadCreditsMakuPast));break;
                        case 0x88:context.Entities.AddEntity(new MakuSproutRoomEntity(Data.Actor(actor.Id,actor.SubId,actor.Position),new(),context.Rooms.SaveData,context.Entities.RuntimeState));break;
                        default:throw new NotSupportedException($"Normal credits actor ${actor.Id:x2}:${actor.SubId:x2}.");
                    }
                }
                context.Transitions.BeginNativeWhiteFade(false);return;
            case 1:
                if(context.Transitions.NativePaletteFadeActive||Read(0xcfdf)==0)return;
                State=2;Write(WramAddress.wTilesetAnimation,255);context.Transitions.BeginNativeWhiteFade(true);return;
            case 2:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=3;context.Entities.Clear();context.Player.ClearNativeItemParents();context.Player.CancelCreditsLink();
                context.Player.EndCutsceneControl();context.Player.WarpTo(Godot.Vector2.Zero,recordSafe:false);context.Player.Visible=false;_linkEnabled=false;
                Profile($"credits-image{index}",0xa1);Screen.SetScrollX((index&1)==0?0:0xd0);
                context.Transitions.SetCutsceneCameraOrigin(new(0x10,0));Write(0xc486,0);
                Write(0xcbb3,90);Write(0xcfdf,0);context.Transitions.BeginNativeWhiteFade(false);return;
            case 3:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=4;
                if(context.Entities.InteractionSlotAvailable)
                    context.Entities.AddEntity(new CreditsHorizontalRoomEntity(context,_horizontalCredits??=new(),Screen,index));
                return;
            case 4:
                if(context.Transitions.NativePaletteFadeActive||Read(0xcfdf)==0)return;
                if(index<(context.Rooms.SaveData.IsLinkedGame?7:3)){Write(0xcfde,index+1);State=0;}
                else
                {
                    ClearTemporaryState();for(int address=0xcfc0;address<0xcfe0;address++)Write(address,0);
                    Write(0xcbc1,2);
                }
                context.Transitions.BeginNativeWhiteFade(true);return;
            default:throw new NotSupportedException($"endgameCutscenes.s: credits stage1 state ${State:x2} continuation is not represented.");
        }
    }
    private void ReleaseCreditsInventory()
    {
        if(_healthBeforeCredits is not int health)return;
        context.Inventory.SetScriptedHealth(health);
        context.Inventory.SetScriptedEquippedItems(Read(0xcde3),Read(0xcde4));
        context.Inventory.SetScriptedActiveRing(_ringBeforeCredits);_healthBeforeCredits=null;
    }
}

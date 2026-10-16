using Godot;
using System;
using System.Linq;

namespace oracleofages;

// CUTSCENE_BLACK_TOWER_ESCAPE $09 runs before updateAllObjects. Link reads
// the same imported simulated-input stream as the original special-object pass.
internal sealed partial class BlackTowerEndingEvent(RoomEventContext context,Action releasePreviousFade,Func<bool> flash) : IRoomEvent,IRoomEventDialogueContext
{
    private BlackTowerEndingDatabase? _data;
    private NayruRescueWarpDatabase? _companions;
    private ExplosionWithDebrisDatabase? _explosions;
    private BlackTowerEndingDatabase Data=>_data??=new();
    private readonly RoomEventResources _resources=new(context,new object());
    private NativeSimulatedInput? _input;
    private NativeSimulatedInput Simulated=>_input??=new(context.Entities.RuntimeState,context.Player.ClearNativePegasusCounter);
    private bool _initialize,_linkEnabled;
    public bool HasState {get;private set;}
    internal int State {get=>_initialize?0:Read(0xcbc2);private set=>Write(0xcbc2,value);}
    // Link's enabled byte and the source masks control his normal handler.
    // Simulated input still uses that handler while the cutscene owns menus.
    public bool BlocksGameplay=>false;
    public bool OwnsGameLogic=>HasState;
    public bool MenusDisabled=>HasState;
    public bool SuppressesStatusBar=>HasState&&(CreditsPending||Stage==1);
    public bool SuppressesNormalMenuDispatch=>HasState;
    public bool DisablesLink=>HasState&&!_linkEnabled;
    public bool AllScreenTransitionsDisabled=>HasState;
    public bool OwnsUpdatesOf(IRoomEvent other)=>other is PalacePossessionEvent;
    private byte Read(int address)=>context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address,int value)=>context.Entities.RuntimeState.SetWramByte(address,unchecked((byte)value));
    private bool Decrement(int address){Write(address,Read(address)-1);return Read(address)==0;}
    internal void Begin()
    {
        releasePreviousFade();HasState=true;_initialize=true;_linkEnabled=false;Write(0xc2ef,9);_resources.CaptureFullScreenFade();
    }
    private void Load(int group,int id,bool restartAnimations=false)
    {
        context.Player.CancelCreditsLink();
        _restoreTerrain?.Invoke();_restoreTerrain=null;
        var room=context.Rooms.LoadCutsceneRoom(group,id,restartAnimations);
        context.Entities.LoadCutsceneRoom(group,room,includeTimePortals:false);context.RoomView.SetRoom(room.Texture);
        context.Sound.SelectRoomMusic(group,id,context.Rooms.SaveData);
        context.Player.EndCutsceneControl();context.Player.WarpTo(Vector2.Zero,recordSafe:false);context.Player.Visible=false;
        _linkEnabled=false;context.Transitions.ResetCamera();Write(WramAddress.wScrollMode,1);
        Write(WramAddress.wLoadedTreeGfxIndex,255);Write(WramAddress.wInteractionIDToLoadExtraGfx,0);
    }
    private void Companions(string scene)
    {
        foreach(var row in Data.Placements(scene))
            if(row.Id is 0x36 or 0x37)context.Entities.AddEntity(new NayruRescueActorRoomEntity(context,_companions??=new(),row.Id,row.SubId,row.Position));
            else if(row.Id is 0x3c or 0x3a or 0x43)context.Entities.AddEntity(new RestoredFamilyRoomEntity(context,Data,_companions??=new(),row.Id,row.SubId,row.Position));
            else if(row.Id==0x4b)context.Entities.AddEntity(new RestoredRabbitRoomEntity(context,Data,_companions??=new(),row.SubId,row.Position));
            else context.Entities.AddEntity(new BlackTowerEndingActor(context,Data,row.Id,row.SubId,row.Position));
    }
    private void RoomObjectSet()
    {
        var row=Data.RoomSet(Read(0xcfde));Load(row.Group,row.Room);Companions(row.Scene);Write(0xcfd1,0);
        context.Transitions.BeginNativeWhiteFade(false);
    }
    private void Tiles(string table)
    {
        var values=Data.Table(table);var room=context.Rooms.CurrentRoom;
        for(int i=0;i<values[0];i++)
        {
            int packed=values[i*2+1];Vector2 point=new((packed&15)*16+8,(packed&0xf0)+8);
            room.ReplaceMetatile(point,room.GetMetatile(point),values[i*2+2],context.AnimationTick());
        }
    }
    private bool Rumble()
    {
        if(!Decrement(0xcbb6))return false;
        Write(0xcbb6,16);context.Sound.PlaySound(SoundId.SndExplosion);context.Entities.BeginScreenShake(8);return true;
    }
    private void Explosion()
    {
        if(!Rumble()||!context.Entities.InteractionSlotAvailable)return;
        context.Entities.AddEntity(new ExplosionWithDebrisRoomEntity(context.Entities,_explosions??=new(),
            Data.Table("explosion-positions"),Data.Table("debris-speeds"),0,1,Vector2.Zero));Write(0xcfd0,1);
    }
    internal void PublishGameKeys()
    {
        if(HasState&&Simulated.Enabled)Input.OverrideGameKeys(Simulated.Sample(context.Transitions.NativePaletteThreadMode));
    }
    internal void AdvanceBeforeObjects()
    {
        if(!HasState)return;
        if(_initialize)
        {
            _initialize=false;for(int address=0xcbb3;address<0xcbc3;address++)Write(address,0);
        }
        context.Transitions.AdvanceNativeWhiteFade();
        if(CreditsPending){AdvanceCredits();return;}
        if(Stage==1){AdvanceTwinrova();return;}
        switch(State)
        {
            case 0:
                if(context.Transitions.NativePaletteFadeActive)return;
                for(int address=0xcfc0;address<0xcfe0;address++)Write(address,0);
                State=1;Load(1,0x76);context.Sound.PlaySound(SoundId.SndCtrlFastFadeOut);
                context.Player.ClearNativeItemParents();context.Player.DropHeldItemsForScript();
                Companions("objectData_blackTowerEscape_nayruAndRalph");Write(0xcbb3,60);
                Tiles("blackTowerEscapeCutscene_doorBlockReplacement");context.Transitions.BeginNativeWhiteFade(false);return;
            case 1:
                if(!context.Transitions.NativePaletteFadeActive&&Decrement(0xcbb3))
                {Write(0xcbb3,120);Write(0xcbb6,16);State=2;}return;
            case 2:
                if(Decrement(0xcbb3)){Write(0xcbb3,60);State=3;}else Rumble();return;
            case 3:
                if(!Decrement(0xcbb3))return;
                Write(0xcbb3,30);context.ShowDialogue(Data.Text(0x1d0a),sourceTextId:0x1d0a);State=4;return;
            case 4:
                if(!context.DialogueOpen&&Decrement(0xcbb3)){Write(0xcbb3,120);Write(0xcbb6,16);State=5;}return;
            case 5:
                if(!Decrement(0xcbb3)){Explosion();return;}
                Write(0xcbb3,40);State=6;_linkEnabled=true;
                context.Player.WarpTo(new(0x50,0x48),recordSafe:false);context.Player.Face(Vector2I.Down);context.Player.Visible=true;
                context.Player.RequestNativeLinkInitialization();
                Simulated.Install(Data.Input(1));Tiles("blackTowerEscapeCutscene_doorOpenReplacement");return;
            case 6:if(Decrement(0xcbb3))State=7;else Explosion();return;
            case 7:
                if(Read(0xcfd0)!=4)return;
                State=8;Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wScrollMode,0);Simulated.Install(Data.Input(2));return;
            case 8:
                if(Read(0xcfd0)!=5)return;
                State=9;context.Transitions.BeginNativeWhiteFade(true);return;
            case 9:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=0x0a;Load(1,0x65);context.Sound.PlaySound(SoundId.MusDisaster);
                Companions("objectData_blackTowerEscape_ambiAndGuards");Write(0xcbb3,30);
                context.Transitions.BeginNativeWhiteFade(false);return;
            case 0x0a:
                if(context.Transitions.NativePaletteFadeActive||!Decrement(0xcbb3))return;
                State=0x0b;_linkEnabled=true;context.Player.WarpTo(new(0x50,0x88),recordSafe:false);
                context.Player.Face(Vector2I.Up);context.Player.Visible=true;context.Player.RequestNativeLinkInitialization();
                Simulated.Install(Data.Input(3));Write(WramAddress.wScrollMode,0);return;
            case 0x0b:
                if(Read(0xcfd0)!=6)return;
                State=0x0c;Simulated.Install(Data.Input(4));return;
            case 0x0c:
                if(Read(0xcfd0)!=0x0a)return;
                State=0x0d;Write(0xcfde,8);Write(0xcfdf,0);context.Transitions.BeginNativeWhiteFade(true);return;
            case 0x0d:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=0x0e;RoomObjectSet();Write(0xcfd1,0);Write(0xcfdf,0);return;
            case 0x0e:
                if(context.Transitions.NativePaletteFadeActive||Read(0xcfdf)!=255)return;
                Write(0xcfdf,0);Write(0xcfde,Read(0xcfde)+1);State=Read(0xcfde)==10?0x0f:0x0d;
                context.Transitions.BeginNativeWhiteFade(true);return;
            case 0x0f:
                if(context.Transitions.NativePaletteFadeActive)return;
                State=0x10;RoomObjectSet();_linkEnabled=true;
                context.Player.WarpTo(new(0x60,0x48),recordSafe:false);context.Player.Face(Vector2I.Up);context.Player.Visible=true;
                context.Player.RequestNativeLinkInitialization();Write(0xcfd0,0x0b);return;
            case 0x10:
                if(context.Rooms.SaveData.IsLinkedGame)throw new NotSupportedException("CUTSCENE $09 stage0 state $10 linked ending continuation is not represented.");
                if(Read(0xcfd0)!=0x10)return;
                State=0x11;context.Transitions.BeginNativeWhiteFade(true);return;
            case 0x11:case 0x12:case 0x13:AdvanceOracle();return;
            default:throw new NotSupportedException($"endgameCutscenes.s:CUTSCENE $09 stage0 state ${State:x2} continuation is not represented.");
        }
    }
    public void UpdateFrame()
    {
        if(HasState&&CreditsPending&&Stage==1&&Read(0xcfde) is >=1 and <=3&&State==1&&Read(WramAddress.wTilesetAnimation)==1)
            Screen.UpdateNativeAnimation(context.AnimationTick());
    }
    public void Cancel()
    {
        if(!HasState)return;HasState=false;_initialize=false;Simulated.SetEnabled(0);Write(0xc2ef,0);
        ReleaseCreditsInventory();ReleaseScreen();
        context.Player.ClearNativeItemParents();context.Player.BeginCutsceneControl(owner:this);
        context.Player.CancelCreditsLink();
        foreach(var actor in context.Entities.EntityAdapters<NayruRescueActorRoomEntity>().Where(a=>a.Id==0x36&&a.SubId is 3 or 4||a.Id==0x37&&a.SubId is 5 or 6).ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<BlackTowerEndingActor>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<RestoredFamilyRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<RestoredRabbitRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var accessory in context.Entities.EntityAdapters<AccessoryRoomEntity>().ToArray())context.Entities.RetireEntity(accessory);
        foreach(var ball in context.Entities.EntityAdapters<NativeBallRoomEntity>().ToArray())context.Entities.RetireEntity(ball);
        foreach(var debris in context.Entities.EntityAdapters<ExplosionWithDebrisRoomEntity>().ToArray())context.Entities.RetireEntity(debris);
        foreach(var actor in context.Entities.EntityAdapters<ImpaCreditsRoomEntity>().ToArray()){actor.Cancel();context.Entities.RetireEntity(actor);}
        foreach(var actor in context.Entities.EntityAdapters<NayruRescueActorRoomEntity>().Where(a=>a.Id==0x36&&a.SubId==5||a.Id==0x37&&a.SubId==7||a.Id==0x87&&a.SubId==4).ToArray())context.Entities.RetireEntity(actor);
        foreach(var flower in context.Entities.EntityAdapters<NayruRescueFlowerRoomEntity>().ToArray())context.Entities.RetireEntity(flower);
        foreach(var actor in context.Entities.EntityAdapters<CreditsLinkLoaderRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<CreditsHorizontalRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<CreditsVerticalRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<GameCompleteDialogRoomEntity>().ToArray())
        {context.CloseDialogue();context.Entities.RetireEntity(actor);}
        foreach(var actor in context.Entities.EntityAdapters<CreditsCompanionLoaderRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<MapleCreditsRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<ForestAudienceRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<NayruRescueActorRoomEntity>().Where(a=>a.Id==0x36&&a.SubId==6||a.Id==0x39&&a.SubId==6).ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<NayruRescueActorRoomEntity>().Where(a=>a.Id==0x37&&a.SubId==8).ToArray())context.Entities.RetireEntity(actor);
        foreach(var sword in context.Entities.EntityAdapters<LinkedSwordRoomEntity>().ToArray())context.Entities.RetireEntity(sword);
        foreach(var actor in context.Entities.EntityAdapters<PickaxeWorkerCreditsRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<MakuStatueConstructionRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var actor in context.Entities.EntityAdapters<MakuSproutRoomEntity>().Where(a=>a.Npc.Record.SubId==2).ToArray())context.Entities.RetireEntity(actor);
        SetCreditsScrollY(0);
        foreach(var actor in context.Entities.EntityAdapters<NayruRescueMusicNoteRoomEntity>().ToArray())context.Entities.RetireEntity(actor);
        foreach(var statue in context.Entities.EntityAdapters<MakuLinkStatueRoomEntity>().Where(a=>a.Node is NpcCharacter {Record.SubId:0x0e}).ToArray())context.Entities.RetireEntity(statue);
        context.Transitions.CancelNativeWhiteFade();_resources.ReleaseFullScreenFade(restoreColor:false);context.Fade.Color=new(1,1,1,0);
        context.Player.Visible=true;context.Player.EndCutsceneControl();
        Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);Write(WramAddress.wScrollMode,0);
    }
}

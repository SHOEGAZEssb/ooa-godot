namespace Oracles;

// INTERAC_MAKU_CUTSCENES $22:$00, the first Essence's exit advice.
internal sealed class SeasonsRemoteMakuRoomEntity(SeasonsOpeningSession session,SeasonsDungeonEssenceDatabase data,
    NativeObjectInstruction source) : CutsceneCommandHost,INativeInteractionRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private NativeInteractionState _state=null!;
    private CutsceneCommandRunner _runner=null!;
    private readonly NativeScriptInputControl _input=new(session.Memory);
    private Color[,]? _background;
    private IReadOnlyDictionary<int,Color[]>? _sprites;
    private readonly OracleRoomData _room=session.Room;
    private IReadOnlyDictionary<int,Color[]>? _previousSprites;
    private bool _fading,_hiddenHud;
    internal bool Active=>!Finished&&_state[0x44]!=0;
    public Node2D? PresentationNode=>null;
    public bool Finished {get;private set;}
    public bool NativeInitializationPending=>!Finished&&_state[0x44]==0;
    public override bool DialogueOpen=>session.Dialogue.IsOpen;
    public override bool ScriptExecutionBlocked=>DialogueOpen&&!session.Dialogue.AllowsNativeScriptCommands;
    public void BindNativeInteractionSlot(int slot)
    {
        _state=new(session.Memory,slot);_state.Clear();_state[0x40]=1;_state[0x41]=0x22;_state[0x42]=source.SubId;
        _state.Position=new(source.X,source.Y);_runner=new(this,_state);
    }
    public void ReleaseNativeInteractionSlot()
    {
        _runner.Clear();_input.Cancel();
        if(_fading)session.Transitions.CancelNativeWhiteFade(clearPresentation:true);
        if(ReferenceEquals(session.Room,_room)&&_background is not null)session.Rooms.BackgroundPalettes.Restore(_background);
        if(ReferenceEquals(session.Room,_room)&&_sprites is not null)session.Entities.SetNativeObjectPalette(_previousSprites);
        if(_hiddenHud)session.Hud.ShowStatusBar();
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    // This controller has no OAM; its native scroll gate defers the script.
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)=>ScreenTransitionPresentation.Hidden;
    internal void AdvancePaletteBeforeObjects()
    {
        if(!_fading||Finished)return;
        session.Transitions.AdvanceNativeWhiteFade();
        if(_hiddenHud)session.Hud.SetHiddenStatusBarFade(session.Rooms.BackgroundPalettes.Resolve(0,2),1);
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_state[0x44]==0)
        {
            if(session.Save.HasRoomFlag(session.Rooms.ActiveGroup,session.RoomId,0x40)||(session.Inventory.Essences&1)==0)
            {Finished=true;return;}
            _state[0x7e]=session.Inventory.Essences;
            session.Memory.SetWramByte(0xc6e5,(byte)System.Numerics.BitOperations.PopCount((uint)session.Inventory.Essences));
            if((session.Memory.ReadField("wScrollMode")&1)==0)return;
            _state[0x44]=1;_runner.Start(data.MakuScript);_runner.AdvanceFrame();_runner.AdvanceFrame();
            int stage=session.Save.HasRoomFlag(1,0x2a,0x40)?9:1;
            if(session.Inventory.Essences!=1)throw new NotSupportedException($"{source.Source}: INTERAC$22:$00 requires the first-Essence Maku stage; mask${session.Inventory.Essences:x2}.");
            session.Memory.SetWramByte(0xcc39,(byte)stage);session.Save.SetMakuMapTextPresent(data.Table("maku-map-text")[stage]);
        }
        _runner.AdvanceFrame();
    }
    public override void SetInputEnabled(bool enabled)=>_input.SetEnabled(session.Link,enabled);
    public override void OrRoomFlag(int flag)=>session.Save.SetRoomFlag(session.Rooms.ActiveGroup,session.RoomId,(byte)flag);
    public override void SetMusic(int music)
    {int selected=music==0xff?session.Sound.NativeRoomMusic:music;session.Sound.SetNativeActiveMusic(selected);session.Sound.PlayNativeSound(selected);}
    public override bool UpdateNativeHandler(string handler,CutsceneActorId? actor,int update,int frames,string payload)=>
        handler=="PaletteDone"?!session.Transitions.NativePaletteFadeActive:throw UnsupportedCommand(handler);
    public override void RunNativeHandler(string handler)
    {
        if(handler.StartsWith("ShowMakuText\0",StringComparison.Ordinal))
        {
            session.Dialogue.ShowMessage(handler[13..],DialogueScreenContext.Gameplay(session.Link.Position.Y,
                -session.Transitions.WorldToGameplayScreen(Vector2.Zero).Y),null,session.Memory.ReadField("wTextboxFlags"),0x1704);return;
        }
        switch(handler)
        {
            case "TextboxFlags":session.Memory.SetField("wTextboxFlags",4);return;
            case "hideStatusBar":session.Hud.HideStatusBar();_hiddenHud=true;return;
            case "showStatusBar":session.Hud.ShowStatusBar();_hiddenHud=false;return;
            case "scriptHelp.seasonsFunc_15_571a, $02":
                _background=session.Rooms.BackgroundPalettes.Capture();
                _previousSprites=session.Entities.NativeObjectPalettes;
                _sprites=Enumerable.Range(0,8).ToDictionary(index=>index,index=>Enumerable.Range(0,4).Select(shade=>session.Data.World.SpritePalettes[index,shade]).ToArray());
                session.Transitions.BeginNativeMaskedFade(Colors.Black,true,0xff,0xfe,_background,_sprites,delay:2);_fading=true;return;
            case "SpawnLeaves":
                if(session.Entities.InteractionSlotAvailable)session.Entities.AddEntity(new SeasonsMakuLeafRoomEntity(session.Entities,data,0));return;
            case "clearFadingPalettes":
                session.Transitions.CancelNativeWhiteFade();
                session.Rooms.BackgroundPalettes.Restore(_background??throw new InvalidOperationException("Remote Maku has no source palette snapshot."));
                session.Entities.SetNativeObjectPalette(_previousSprites);return;
            case "fadeinFromWhiteWithDelay, $02":session.Transitions.BeginNativeWhiteFade(false,delay:2);_fading=true;return;
            case "scriptHelp.seasonsFunc_15_576c":return; // Native helper returns immediately for subid$00.
            default:throw UnsupportedCommand(handler);
        }
    }
    public override void ScriptEnded(){_fading=false;Finished=true;}
}

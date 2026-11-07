namespace Oracles;

// gnarledKeyhole.s: INTERAC$21 owns the script and rising entrance in its native slot.
internal sealed class SeasonsGnarledKeyholeRoomEntity(SeasonsOpeningSession session,
    SeasonsTalkNpcDatabase data, NativeObjectInstruction source) : CutsceneCommandHost,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity
{
    private NativeInteractionState _state = null!;
    private CutsceneCommandRunner _runner = null!;
    internal NativeScriptInputControl Input { get; } = new(session.Memory);
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => !Finished && _state[0x44] == 0;
    public override bool DialogueOpen => session.Dialogue.IsOpen;
    public override bool ScriptExecutionBlocked => DialogueOpen && !session.Dialogue.AllowsNativeScriptCommands;
    internal bool CanTrigger => !Finished && _state[0x44] == 1;

    public void BindNativeInteractionSlot(int slot)
    {
        _state = new(session.Memory,slot);
        _state.Clear(); _state[0x40]=1; _state[0x41]=source.Id; _state[0x42]=source.SubId;
        _state.Position=new(source.X,source.Y);
        _runner=new(this,_state);
    }
    public void ReleaseNativeInteractionSlot()
    {
        Input.Cancel();
        if (_state[0x44] != 0 && !Finished) session.Memory.SetField("wDisableWarpTiles",0);
        _runner.Clear(); _state.Clear(); Finished=true;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Initialize(); return ScreenTransitionPresentation.Hidden; }
    private void Initialize()
    {
        if (!NativeInitializationPending) return;
        if (session.Save.HasRoomFlag(session.Rooms.ActiveGroup,session.RoomId,0x80)) { Finished=true; return; }
        _state[0x44]=1;
        session.Memory.SetField("wDisableWarpTiles",1);
        SetCollisions("gnarled-closed-collisions");
        _runner.Start(data.Program("gnarledKeyholeScript"));
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (NativeInitializationPending) { Initialize(); return; }
        switch (_state[0x44])
        {
            case 1: _runner.AdvanceFrame(); return;
            case 2:
                _state[0x44]=3; _state[0x46]=30;
                session.Memory.SetField("wLinkForceState",8); session.Memory.SetField("wcc50",0);
                Input.SetDisabledObjects((byte)(session.DisabledObjects|0x80));
                session.Entities.BeginHorizontalScreenShake(15);
                goto case 3;
            case 3:
                SpawnStone();
                _state[0x46]--;
                if (_state[0x46]!=0) { session.Entities.BeginHorizontalScreenShake(15); return; }
                int step=_state[0x47];
                if (step<4)
                {
                    _state[0x47]=++step;
                    session.Data.World.ApplyUniqueGraphics(session.Room,data.NativeTable("gnarled-graphics")[step]);
                    session.Sound.PlaySound(0x82);
                    var counters=data.NativeTable("gnarled-counters");
                    _state[0x46]=counters[step*2];
                    if (counters[step*2+1]!=0) _state[0x44]++;
                }
                else _state[0x44]++;
                session.Entities.SetVerticalScreenShake(4);
                return;
            case 4:
                SetCollisions("gnarled-open-collisions");
                session.Memory.SetField("wDisableWarpTiles",0);
                Input.SetDisabledObjects(0); Input.SetMenuDisabled(0);
                session.Sound.PlaySound(0x4d);
                int music=session.Sound.NativeRoomMusic; // gnarledKeyhole.s reads raw Seasons $cc62 (wActiveMusic2).
                session.Sound.SetNativeActiveMusic(music); session.Sound.PlayNativeSound(music);
                Finished=true;
                return;
            default: throw new NotSupportedException($"{source.Source}: INTERAC$21 state ${_state[0x44]:x2}.");
        }
    }
    private void SetCollisions(string table)
    {
        var positions=data.NativeTable("gnarled-positions"); var collisions=data.NativeTable(table);
        for(int i=0;i<positions.Length;i++) session.Room.SetPackedTileCollision(positions[i],collisions[i]);
    }
    private void SpawnStone()
    {
        int counter=session.FrameCounter;
        if((counter&1)!=0)return;
        // RNG precedes allocation even when every interaction slot is occupied.
        int random=session.Random.Next().Value;
        int subid=data.NativeTable("gnarled-stone-subids")[(counter&6)>>1];
        var position=new Vector2(0x48+((random&0xf8)>>3)-0x10,0x48+(random&7)-4);
        session.Entities.TryAllocateInteraction(()=>new SeasonsGnarledStoneRoomEntity(session,data,
            source with { Id=0x4b,SubId=subid,X=(int)position.X,Y=(int)position.Y },session.Room));
    }
    public override bool MemoryEquals(string binding,int value) => binding=="cfc0bit0"
        ? (session.Memory.ReadField("wTmpcfc0")&1)==value : throw UnsupportedCommand($"read {binding}");
    public override void SetInputEnabled(bool enabled) => Input.SetEnabled(session.Link,enabled);
    public override void SetMusic(int music)
    { session.Sound.SetNativeActiveMusic(music); session.Sound.PlayNativeSound(music); }
    public override void OrRoomFlag(int flag) => session.Save.SetRoomFlag(session.Rooms.ActiveGroup,session.RoomId,(byte)flag);
    public override void RunNativeHandler(string handler)
    {
        if(handler=="incstate")_state[0x44]++;
        else if(handler=="ShakeScreen120")session.Entities.BeginHorizontalScreenShake(120);
        else throw UnsupportedCommand(handler);
    }
    public override void ScriptEnded() { }
}

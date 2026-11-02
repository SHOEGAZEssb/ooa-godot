
namespace Oracles;

internal class NativeNpcRoomEntity : NpcRoomEntity, INativeInteractionRoomEntity
{
    private readonly OracleRuntimeState _memory;
    private readonly Player _player;
    private readonly Func<int, string> _animation, _text;
    private readonly Action<NativeNpcRoomEntity, Player>? _initialize;
    private NativeInteractionState _state = null!;
    protected override bool Initialized
    {
        get => _state is not null && _state[0x44] != 0;
        set => _state[0x44] = value ? 1 : 0;
    }
    internal NativeNpcRoomEntity(NpcCharacter npc, OracleRuntimeState memory, Player player,
        Func<int, string> animation, Func<int, string> text, Action<NativeNpcRoomEntity, Player>? initialize = null)
        : base(npc)
    { _memory = memory; _player = player; _animation = animation; _text = text; _initialize = initialize; }
    public bool NativeInitializationPending => !Finished && !Initialized;
    public override bool RefreshStoryState => false;
    public void BindNativeInteractionSlot(int slot)
    {
        _state = new(_memory, slot);
        _state.Clear();
        var source = Entity.Record;
        _state[0x40] = 1; _state[0x41] = source.Id; _state[0x42] = source.SubId; _state[0x43] = source.Var03;
        _state.Position = Entity.Position;
        _state.SetWord(0x72, source.TextId);
        _state[0x66] = 6; _state[0x67] = 6;
        Entity.BindNativeState(_state);
    }
    public void ReleaseNativeInteractionSlot() { Script?.Cancel(); Delete(); Entity.BindNativeState(null); }
    internal int ReadByte(int offset) => _state[offset];
    internal void WriteByte(int offset, int value)
    {
        _state[offset] = value;
        if (offset == 0x5c) Entity.SetBasePalette(value & 7);
        ObjectByteWritten(offset, value);
        Entity.ProjectNativePosition();
    }
    protected virtual void ObjectByteWritten(int offset, int value) { }
    internal void SetAnimation(int index) => Entity.SetScriptAnimation(_animation(index));
    internal string Text(int id) => _text(id);
    internal void AddTextId(int value)
    { int id = (Entity.TextId & 0xff00) | ((Entity.TextId + value) & 255); Entity.SetDialogue(id, Text(id), Entity.Record.CanFace); }
    internal void Move(int speed, int angle) => Entity.Position = NativeObjectMovement.ApplySpeed(_memory,
        OracleObjectPosition.FromPixels(Entity.Position), speed, angle).PrecisePosition;
    internal virtual bool TryNativeTalk(NpcInteractionTarget target) => false;
    internal void Delete()
    {
        if (Finished) return;
        Finished = true; Entity.BindNativeState(null); _state.Clear(); Entity.SetActive(false);
    }
    protected override bool InitializeNpc(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { _initialize!(this, frame.Player); return false; }
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Initialized) UpdateFrame(new(_player, _memory.FrameCounter, false, null), spawns);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public override bool BlocksLink(Vector2 center) => Initialized && !Finished && base.BlocksLink(center);
    public override NpcCharacter? FindTalkTarget(Player player) => Initialized && !Finished && ReadByte(0x71) == 0
        ? base.FindTalkTarget(player) : null;
}

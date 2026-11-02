
namespace Oracles;

// object_code/ages/interactions/raft.s: INTERAC $e6 is distinct from D1.
internal sealed partial class RaftInteractionRoomEntity : TransitionOffsetNode2D,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IPlayerRideableRoomEntity,
    IScreenTransitionPreloadRoomEntity
{
    private readonly RaftInteractionSpawn _spawn;
    private readonly OracleRuntimeState _memory;
    private readonly OracleSaveData _save;
    private readonly RaftBehavior _behavior;
    private readonly RoomEntityManager _owner;
    private readonly EnemyAnimationPlayer _animation;
    private int _address;
    private bool _finished;

    public Node2D Node => this;
    public bool Finished
    {
        get
        {
            if (!_finished && _address != 0 && Read(0) == 0) _finished = true;
            return _finished;
        }
    }
    public bool NativeInitializationPending => !Finished && (_address == 0 || State == 0);
    public int? NativeReconstructionSlot => _spawn.NativeReconstructionSlot;
    public bool LinkRiding => !Finished && _address != 0 &&
        _memory.ReadField("wLinkRidingObject") == _address >> 8;
    internal int State => Read(4);
    internal int Direction => Read(8);
    internal Vector2 PrecisePosition => _address == 0 ? _spawn.Position.Floor() :
        new(Read(0x0d) + Read(0x0c) / 256f, Read(0x0b) + Read(0x0a) / 256f);
    internal int AnimationIndex => _animation.AnimationIndex;

    internal RaftInteractionRoomEntity(RaftInteractionSpawn spawn, OracleRoomData room,
        RaftBehavior behavior, OracleSaveData save, OracleRuntimeState memory, RoomEntityManager owner)
    {
        if (spawn.SubId is < 0 or > 2)
            throw new NotSupportedException($"INTERAC $e6:${spawn.SubId:x2}: unsupported raft subid.");
        AssetRoot = room.AssetRoot;
        _spawn = spawn; _behavior = behavior; _save = save; _memory = memory; _owner = owner;
        _animation = new EnemyAnimationPlayer(this, 2);
        _animation.Load(OracleGraphicsCache.LoadImage(room.AssetRoot + $"gfx/{behavior.Sprite}.png"),
            behavior.WaitingAnimations, behavior.WaitingTileBase, behavior.WaitingPalette, positionedOam: true);
        Position = spawn.Position.Floor();
        ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        Visible = false;
        Name = $"RaftInteraction_{spawn.Group:x1}_{spawn.Room:x2}";
    }

    public void BindNativeInteractionSlot(int slot)
    {
        if (_address != 0) throw new InvalidOperationException("INTERAC $e6 already owns a native slot.");
        _address = ((0xd0 + slot) << 8) | 0x40;
        if (_spawn.NativeReconstructionSlot is { } restored)
        {
            if (restored != slot || !CanReconstruct(_memory, slot, _behavior))
                throw new NotSupportedException($"INTERAC $e6 slot${_address:x4}: unsupported native debug binding.");
            if (State != 0) RestoreAnimation();
            Position = PrecisePosition.Floor();
            Visible = (Read(0x1a) & 0x80) != 0;
            return;
        }
        // getFreeInteractionSlot does not clear retained bytes. Each producer
        // writes only its header, high XYZ and explicit direction copy.
        Write(0, 1); Write(1, _behavior.InteractionId); Write(2, _spawn.SubId);
        Write(0x0b, OracleObjectPosition.HighByte(_spawn.Position.Y));
        Write(0x0d, OracleObjectPosition.HighByte(_spawn.Position.X));
        Write(0x0f, _spawn.ZHigh);
        if (_spawn.Direction is { } direction) Write(8, direction);
    }

    public void ReleaseNativeInteractionSlot()
    {
        if (_address == 0 || Finished) return;
        Delete();
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        switch (State)
        {
            case 0:
                Initialize();
                if (Finished) return;
                break;
            case 1:
                RestoreAnimation();
                _animation.Advance();
                Write(0x26, _behavior.MountRadius); Write(0x27, _behavior.MountRadius);
                Player player = frame.Player;
                int dx = Math.Abs(OracleObjectPosition.HighByte(player.PrecisePosition.X) - Read(0x0d));
                int dy = Math.Abs(((OracleObjectPosition.HighByte(player.PrecisePosition.Y) + 5) & 0xff) - Read(0x0b));
                if (dx >= _behavior.MountRadius || dy >= _behavior.MountRadius ||
                    player.TopDownAirborne && ((player.TopDownAirZ & 0xff) < 0xfd || player.TopDownAirSpeedZ < 0)) break;
                _memory.SetField("wLinkRidingObject", (byte)(_address >> 8));
                player.DisableInstruments(_behavior.InstrumentLockFrames);
                Write(0x26, _behavior.InnerMountRadius); Write(0x27, _behavior.InnerMountRadius);
                if (dx >= _behavior.InnerMountRadius || dy >= _behavior.InnerMountRadius) break;
                player.RequestNormalLinkOverride();
                _memory.SetWramByte(0xd100, 3); _memory.SetWramByte(0xd101, (byte)_behavior.SpecialObjectId);
                _memory.SetWramByte(0xd108, (byte)Direction);
                foreach (int offset in new[] { 0x0b, 0x0d, 0x0f })
                    _memory.SetWramByte(0xd100 + offset, (byte)Read(offset));
                _owner.BindNativeSpecialObjectPresentation(_behavior.SpecialObjectId, new RaftSpawn(
                    CompanionRuntimeState.Read(_memory).Position, Direction, _spawn.Group, _spawn.Room));
                Write(4, 2);
                break;
            case 2:
                Delete();
                return;
            default:
                throw new NotSupportedException($"INTERAC $e6:${Read(2):x2} slot${_address:x4}: unsupported state${State:x2}.");
        }
        Position = PrecisePosition.Floor();
        PublishAnimation();
        QueueRedraw();
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (NativeInitializationPending) Initialize();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    private void Initialize()
    {
        int subid = Read(2);
        if (subid == 0 && (_save.ReadWramByte(_behavior.DimitriStateAddress) & _behavior.DimitriMask) == 0 ||
            subid < 2 && (!_save.HasGlobalFlag(_behavior.ChangedRoomsFlag) ||
                _memory.ReadWramByte(0xd101) == _behavior.SpecialObjectId ||
                _owner.FindNativeInteractionWithId(_behavior.InteractionId) != _owner.NativeInteractionSlot(this)))
        { Delete(); return; }
        Write(4, 1);
        _animation.SetAnimation(Direction & 1);
        Write(0x1a, 0x83);
        PublishAnimation();
        Visible = true;
    }

    internal static bool CanReconstruct(OracleRuntimeState memory, int slot, RaftBehavior behavior)
    {
        int at = 0xd040 + slot * 0x100;
        if (memory.ReadWramByte(at) == 0 || memory.ReadWramByte(at + 1) != 0xe6 ||
            memory.ReadWramByte(at + 2) > 2 || memory.ReadWramByte(at + 4) > 2 || memory.ReadWramByte(at + 8) > 3) return false;
        if (memory.ReadWramByte(at + 4) == 0) return true;
        int cursor = memory.ReadWramByte(at + 0x22) | memory.ReadWramByte(at + 0x23) << 8;
        foreach (var frame in behavior.WaitingNativeFrames[memory.ReadWramByte(at + 8) & 1])
            if (frame.NextPointer == cursor) return true;
        return false;
    }

    private void RestoreAnimation()
    {
        int cursor = Read(0x22) | Read(0x23) << 8;
        int animation = Direction & 1;
        var frames = _behavior.WaitingNativeFrames[animation];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            if (frames[frame].NextPointer != cursor) continue;
            _animation.SetAnimation(animation);
            _animation.RestoreFrame(frame, Read(0x20));
            return;
        }
        throw new NotSupportedException($"INTERAC $e6 slot${_address:x4}: unbound animation cursor${cursor:x4}.");
    }

    private void PublishAnimation()
    {
        var frame = _behavior.WaitingNativeFrames[_animation.AnimationIndex][_animation.FrameIndex];
        Write(0x20, _animation.RemainingUpdates); Write(0x21, _animation.CurrentParameter);
        Write(0x22, frame.NextPointer & 0xff); Write(0x23, frame.NextPointer >> 8);
        Write(0x1e, frame.OamPointer & 0xff); Write(0x1f, frame.OamPointer >> 8);
    }

    private int Read(int offset) => _address != 0 ? _memory.ReadWramByte(_address + offset) :
        throw new InvalidOperationException("INTERAC $e6 requires its bound native slot.");
    private void Write(int offset, int value) => _memory.SetWramByte(_address + offset, checked((byte)value));
    private void Delete()
    {
        for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        _finished = true; Visible = false;
    }

    public override void _Draw() => DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + SourceOamDrawOffset);
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}

internal sealed record RaftInteractionSpawn(Vector2 Position, int Group, int Room,
    int SubId = 2, int? Direction = null, int ZHigh = 0, int? NativeReconstructionSlot = null) : RoomEntitySpawn;

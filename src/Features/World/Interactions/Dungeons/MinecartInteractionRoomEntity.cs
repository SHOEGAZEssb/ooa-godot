using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// common/interactions/minecart.s: this allocation never becomes SPECIALOBJECT $0a.
internal sealed partial class MinecartInteractionRoomEntity : DungeonInteractionVisualEntity,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IRoomBlocker, IScreenTransitionPreloadRoomEntity
{
    private readonly MinecartInteractionSpawn _spawn;
    private readonly OracleRoomData _room;
    private readonly MinecartData _data;
    private readonly OracleRuntimeState _memory;
    private readonly RoomEntityManager _owner;
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
    internal int State => Read(4);
    internal int Direction => Read(8);
    internal int Angle => Read(9);
    internal int PushCounter => Read(6);
    internal int CurrentAnimationIndex => AnimationIndex;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal bool Mounting => !Finished && State == 2;
    internal Vector2 PrecisePosition => _address == 0 ? _spawn.Position :
        new((Read(0x0c) | Read(0x0d) << 8) / 256f, (Read(0x0a) | Read(0x0b) << 8) / 256f);

    internal MinecartInteractionRoomEntity(MinecartInteractionSpawn spawn, OracleRoomData room,
        OracleRuntimeState memory, DungeonInteractionVisual visual,
        RoomEntityManager owner)
    {
        _spawn = spawn; _room = room; _memory = memory; _owner = owner;
        _data = MinecartData.For(room.AssetRoot);
        InitializeVisual(visual, spawn.Position);
        Visible = false;
        Name = $"MinecartInteraction_{room.Group:x1}_{room.Id:x2}";
    }

    public void BindNativeInteractionSlot(int slot)
    {
        if (_address != 0) throw new InvalidOperationException("INTERAC $16 already owns its native slot.");
        _address = ((0xd0 + slot) << 8) | 0x40;
        if (_spawn.NativeReconstructionSlot is { } restored)
        {
            if (restored != slot || !CanReconstruct(_memory, slot, _data))
                throw new NotSupportedException($"INTERAC $16 slot${_address:x4}: unsupported debug reconstruction.");
            if (State != 0) SetAnimation(Direction & 1);
            Position = PrecisePosition.Floor();
            Visible = (Read(0x1a) & 0x80) != 0;
            ZIndex = ObjectDrawPriority.FromVisible(Read(0x1a));
            return;
        }
        Write(0, _spawn.Enabled); Write(1, 0x16); Write(2, _spawn.SubId);
        Write(0x0b, OracleObjectPosition.HighByte(_spawn.Position.Y));
        Write(0x0d, OracleObjectPosition.HighByte(_spawn.Position.X));
        if (_spawn.StaticSlot is { } staticSlot)
        {
            int related = StaticObjectState.Address(_memory, staticSlot);
            Write(0x16, related & 0xff); Write(0x17, related >> 8);
        }
        if (_spawn.CopyPositionFromSpecialObject)
            for (int offset = 0x0b; offset <= 0x0f; offset += 2) Write(offset, _memory.ReadWramByte(0xd100 + offset));
    }

    public void ReleaseNativeInteractionSlot()
    {
        if (_address != 0 && !Finished) DeleteForScript();
    }

    internal void DeleteForScript()
    {
        for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        _finished = true; Visible = false;
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (Finished || _address == 0 || State != 1) return false;
        Vector2 delta = linkCenter - Position;
        return Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("INTERAC $16 state-zero fallthrough requires the live Link owner.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (NativeInitializationPending)
            UpdateFrame(new RoomEntityFrame(player ?? throw new InvalidOperationException(
                "INTERAC $16 state-zero fallthrough requires the live Link owner."), _owner.FrameCounter, false, null), spawns);
        return Visible && !Finished ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        switch (State)
        {
            case 0:
                Initialize();
                goto case 1; // Native state zero falls through to the collision/push handler.
            case 1:
                Player player = frame.Player;
                int priority = ObjectDrawPriority.RelativeToLink(PrecisePosition.Y,
                    player.MinecartMainObjectPosition.Y, Read(0x0f));
                Write(0x1a, 0x80 | ObjectDrawPriority.ToVisibleBucket(priority));
                ZIndex = priority; Visible = true;
                if (!player.MinecartJumpActive && !player.CompanionJumpActive &&
                    !LinkObjectBlocking.PreventPassing(player, Position, 6, 6)) break;
                Vector2 delta = Position - player.Position;
                if (!player.IsAttemptingMinecartPush || Math.Abs(delta.X) > 4 && Math.Abs(delta.Y) > 4)
                { Write(6, _data.Constant("minecart-mount-push")); break; }
                _memory.SetField("wForceLinkPushAnimation", 1);
                Write(6, (Read(6) - 1) & 0xff);
                if (Read(6) != 0) break;
                Write(4, 2);
                int boarding = OracleObjectMovement.Shared.RelativeAngle(Position, player.PrecisePosition) ^ ObjectAngle.HalfTurn;
                player.BeginMinecartJump(player.PrecisePosition, boarding, 0);
                break;
            case 2:
                if (!frame.Player.MinecartJumpReadyToRide) break;
                Write(4, 3);
                _memory.SetWramByte(0xd100, 3); _memory.SetWramByte(0xd101, SpecialObjectId.Minecart);
                _memory.SetWramByte(0xd108, (byte)Direction); _memory.SetWramByte(0xd109, (byte)Angle);
                // objectCopyPosition retains all destination fractional bytes.
                for (int offset = 0x0b; offset <= 0x0f; offset += 2)
                    _memory.SetWramByte(0xd100 + offset, (byte)Read(offset));
                StaticObjectState.DeleteRelated(_memory, _address);
                _owner.BindNativeSpecialObjectPresentation(SpecialObjectId.Minecart, new MinecartBodySpawn());
                break;
            case 3:
                DeleteForScript();
                return;
            default:
                throw new NotSupportedException($"INTERAC $16:${Read(2):x2} slot${_address:x4}: unsupported state${State:x2}.");
        }
        Position = PrecisePosition.Floor();
        QueueRedraw();
    }

    private void Initialize()
    {
        Write(4, 1); Write(0x26, 6); Write(0x27, 6);
        Write(6, _data.Constant("minecart-mount-push"));
        int packed = _room.GetPackedPosition(PrecisePosition);
        int[] offsets = [-0x10, 1, 0x10, -1];
        int adjacent = 0xff;
        for (int direction = 0; direction < offsets.Length; direction++)
            if (_room.ReadNativeLayoutByte((byte)((packed + offsets[direction]) & 0xff), _memory) == _data.Constant("minecart-platform"))
            { adjacent = direction; break; }
        int facing = adjacent ^ 2;
        Write(8, facing);
        int swapped = ((facing << 4) | (facing >> 4)) & 0xff;
        Write(9, (swapped >> 1) | ((swapped & 1) << 7));
        SetAnimation(facing & 1);
        var native = _data.Waiting[facing & 1][0];
        Write(0x20, native.Duration); Write(0x21, native.Parameter);
        Write(0x22, native.NextPointer & 0xff); Write(0x23, native.NextPointer >> 8);
        Write(0x1e, native.OamPointer & 0xff); Write(0x1f, native.OamPointer >> 8);
        StaticObjectState.DeleteRelated(_memory, _address);
        StaticObjectState.SaveActor(_memory, _address, 3, _owner.ActiveRoom.Id);
    }

    internal static bool CanReconstruct(OracleRuntimeState memory, int slot, MinecartData frames)
    {
        int at = 0xd040 + slot * 0x100;
        int state = memory.ReadWramByte(at + 4), direction = memory.ReadWramByte(at + 8);
        if (memory.ReadWramByte(at) == 0 || memory.ReadWramByte(at + 1) != 0x16 || state is not (0 or 1 or 3)) return false;
        if (state == 0) return true;
        var native = frames.Waiting[direction & 1][0];
        return (memory.ReadWramByte(at + 0x22) | memory.ReadWramByte(at + 0x23) << 8) == native.NextPointer &&
            memory.ReadWramByte(at + 0x20) == native.Duration;
    }

    private int Read(int offset) => _address != 0 ? _memory.ReadWramByte(_address + offset) :
        throw new InvalidOperationException("INTERAC $16 requires its bound native slot.");
    private void Write(int offset, int value) => _memory.SetWramByte(_address + offset, checked((byte)value));
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}

internal sealed record MinecartInteractionSpawn(Vector2 Position, int? StaticSlot = null, int SubId = 0,
    int Enabled = 1, bool CopyPositionFromSpecialObject = false, int? NativeReconstructionSlot = null) : RoomEntitySpawn;
internal sealed record MinecartBodySpawn : RoomEntitySpawn;

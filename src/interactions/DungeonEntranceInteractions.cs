using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Invisible INTERAC_DUNGEON_STUFF $12:$00.</summary>
internal sealed class DungeonEntranceRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonEntranceInteractionDatabase.EntryRecord _record;
    private readonly DungeonEntranceInteractionDatabase _data;
    private readonly OracleRuntimeState _runtimeState;
    private readonly Action<int, string> _triggered;
    private readonly bool _whiteoutEntry;
    private bool _initialized;

    internal DungeonEntranceRoomEntity(
        Vector2 position,
        DungeonEntranceInteractionDatabase.EntryRecord record,
        DungeonEntranceInteractionDatabase data,
        OracleRuntimeState runtimeState,
        bool whiteoutEntry,
        Action<int, string> triggered)
        : base(new Node2D { Name = "DungeonEntrance", Visible = false }, static _ => { })
    {
        Entity.Position = position;
        _record = record;
        _data = data;
        _runtimeState = runtimeState;
        _whiteoutEntry = whiteoutEntry;
        _triggered = triggered;
    }

    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            if (!_whiteoutEntry || frame.Player.Position.Y < _data.EntryMinimumY)
            {
                Finished = true;
                return;
            }

            // initializeDungeonStuff clears these three session-persistent
            // dungeon fields before the Ages table supplies wSpinnerState.
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0);
            _runtimeState.SetWramByte(
                OracleRuntimeState.SpinnerStateAddress, (byte)_record.SpinnerState);
        }

        Vector2 delta = frame.Player.Position - Entity.Position;
        float radius = _data.EntryRadius + NpcCharacter.LinkCollisionRadius;
        if (Mathf.Abs(delta.X) >= radius || Mathf.Abs(delta.Y) >= radius)
            return;
        Finished = true;
        _triggered(_record.TextId, _record.Message);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>One-update INTERAC_STATUE_EYEBALL $e2:$01 room-layout scanner.</summary>
internal sealed class StatueEyeballSpawnerRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly OracleRoomData _room;
    private readonly DungeonEntranceInteractionDatabase _data;

    internal StatueEyeballSpawnerRoomEntity(
        OracleRoomData room,
        DungeonEntranceInteractionDatabase data)
        : base(new Node2D { Name = "StatueEyeballSpawner", Visible = false }, static _ => { })
    {
        _room = room;
        _data = data;
    }

    public bool Finished { get; private set; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"INTERAC_STATUE_EYEBALL $e2:$01 requires a large room, got " +
                $"{_room.Group:x1}:{_room.Id:x2}.");
        }

        // The source starts at packed position $ae and decrements C through
        // $01, so child slot/first-update order is descending room position.
        for (int packed = 0xae; packed >= 1; packed--)
        {
            if (_room.Layout[packed] != _data.EyeStatueTile)
                continue;
            Vector2 position = new(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8 +
                    _data.EyeInitialYOffset);
            spawns.Add(new StatueEyeballSpawn(position));
        }
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

internal sealed partial class StatueEyeball : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private DungeonEntranceInteractionDatabase _data = null!;
    private bool _initialized;
    private int _direction = 4;

    internal int Direction => _direction;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal bool Initialized => _initialized;
    internal ulong PixelHash => _animation is not null && _animation.HasFrames
        ? OracleGraphicsCache.PixelHash(_animation.CurrentTexture.GetImage())
        : 0;

    internal void Initialize(
        Vector2 position,
        DungeonEntranceInteractionDatabase data)
    {
        Name = "StatueEyeball";
        Position = position;
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = false;
        _data = data;
        _animation = new EnemyAnimationPlayer(this, data.EyeVisuals.Count);
        var animations = new string[data.EyeVisuals.Count];
        for (int index = 0; index < data.EyeVisuals.Count; index++)
            animations[index] = data.EyeVisuals[index].Animation;
        DungeonEntranceInteractionDatabase.VisualRecord visual = data.EyeVisuals[0];
        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        _animation.Load(image, animations, visual.TileBase, visual.Palette);
        _animation.SetAnimation(4);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            return;
        }

        Vector2 tileCenter = new(
            Mathf.Floor(Position.X / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8,
            Mathf.Floor(Position.Y / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8);
        int angle = OracleObjectMath.AngleToward(tileCenter, player.Position);
        int low = angle & 0x07;
        if (low is not (0 or 1 or 7))
            angle = (angle & 0xfc) | 0x04;
        int direction = (angle >> 2) & 0x07;
        _direction = direction;
        DungeonEntranceInteractionDatabase.VisualRecord visual =
            _data.EyeVisuals[direction];
        Position = new Vector2(
            Mathf.Floor(tileCenter.X / 16.0f) * 16.0f + visual.LowX,
            Mathf.Floor(tileCenter.Y / 16.0f) * 16.0f + visual.LowY);

        // Subid $02 keeps interactionInitGraphics' default animation $04.
        // Direction is represented solely by moving that fixed eye around
        // the tile; selecting animations $00-$07 double-applies the OAM
        // offset and is only correct for subid $00.
    }

    public override void _Draw()
    {
        if (_animation is not null && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}

internal sealed class StatueEyeballRoomEntity(StatueEyeball eye)
    : RoomEntityAdapter<StatueEyeball>(eye, eye.SetTransitionDrawOffset),
        IFixedRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);
}

internal sealed partial class MinibossPortal : TransitionOffsetNode2D
{
    private EnemyAnimationPlayer _animation = null!;

    internal int AnimationFrame => _animation.FrameIndex;

    internal void Initialize(DungeonEntranceInteractionDatabase data)
    {
        Name = "MinibossPortal";
        int packed = data.PortalPosition;
        Position = new Vector2(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = false;

        DungeonEntranceInteractionDatabase.VisualRecord visual = data.PortalVisual;
        Image image = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.Sprite}.png");
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            image, new[] { visual.Animation }, visual.TileBase, visual.Palette);
        _animation.SetAnimation(0);
    }

    internal void AdvanceAnimation() => _animation.Advance();

    public override void _Draw()
    {
        if (Visible && _animation is not null && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}

/// <summary>INTERAC_MINIBOSS_PORTAL $7e:$00.</summary>
internal sealed class MinibossPortalRoomEntity :
    RoomEntityAdapter<MinibossPortal>, IFixedRoomEntity, IRoomEntityLifetime
{
    internal enum PortalState
    {
        Initialize,
        Ready,
        WaitForLinkToLeave,
        Spinning,
        WarpRequested
    }

    private readonly DungeonEntranceInteractionDatabase.PlacementRecord _placement;
    private readonly DungeonEntranceInteractionDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly Action<WarpDatabase.Warp> _warpRequested;
    private readonly Action<int> _soundRequested;
    private PortalState _state;
    private int _counter;

    internal MinibossPortalRoomEntity(
        MinibossPortal portal,
        DungeonEntranceInteractionDatabase.PlacementRecord placement,
        DungeonEntranceInteractionDatabase data,
        OracleSaveData? save,
        Action<WarpDatabase.Warp> warpRequested,
        Action<int> soundRequested)
        : base(portal, portal.SetTransitionDrawOffset)
    {
        _placement = placement;
        _data = data;
        _save = save;
        _warpRequested = warpRequested;
        _soundRequested = soundRequested;
    }

    public bool Finished { get; private set; }
    internal PortalState State => _state;
    internal int Counter => _counter;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || _state == PortalState.WarpRequested)
            return;

        if (_state == PortalState.Initialize)
        {
            DungeonEntranceInteractionDatabase.PortalPair pair =
                _data.PortalPairFor(_placement.Dungeon);
            if (_save?.HasRoomFlag(
                    _placement.Group, pair.MinibossRoom,
                    OracleSaveData.RoomFlag80) != true)
            {
                Finished = true;
                return;
            }
            Entity.Visible = true;
            _state = Touching(frame.Player.Position)
                ? PortalState.WaitForLinkToLeave
                : PortalState.Ready;
            return;
        }

        Entity.AdvanceAnimation();
        if (_state == PortalState.WaitForLinkToLeave)
        {
            if (!Touching(frame.Player.Position))
                _state = PortalState.Ready;
            return;
        }
        if (_state == PortalState.Ready)
        {
            if (!frame.Player.CutsceneControlled && Touching(frame.Player.Position))
                BeginSpin(frame.Player);
            return;
        }

        frame.Player.SetScriptedPosition(Entity.Position);
        frame.Player.ResetEnemyInvincibility();
        if ((frame.Counter & 0x03) == 0)
            frame.Player.Face(NextClockwise(frame.Player.FacingVector));
        _counter--;
        if (_counter != 0)
            return;

        DungeonEntranceInteractionDatabase.PortalPair destination =
            _data.PortalPairFor(_placement.Dungeon);
        int destinationRoom = _placement.Room == destination.MinibossRoom
            ? destination.EntranceRoom
            : destination.MinibossRoom;
        _state = PortalState.WarpRequested;
        _warpRequested(new WarpDatabase.Warp(
            _placement.Group,
            _placement.Room,
            _data.PortalPosition,
            0,
            _data.PortalSourceTransition,
            _placement.Group,
            destinationRoom,
            _data.PortalPosition,
            _data.PortalDestinationParameter,
            _data.PortalDestinationTransition));
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private void BeginSpin(Player player)
    {
        player.WarpTo(Entity.Position, recordSafe: false);
        player.BeginCutsceneControl();
        _state = PortalState.Spinning;
        _counter = _data.PortalSpinUpdates;
        _soundRequested(_data.PortalSound);
    }

    private bool Touching(Vector2 linkPosition)
    {
        Vector2 delta = linkPosition - Entity.Position;
        float radius = _data.PortalRadius + NpcCharacter.LinkCollisionRadius;
        return Mathf.Abs(delta.X) < radius && Mathf.Abs(delta.Y) < radius;
    }

    private static Vector2I NextClockwise(Vector2I direction) =>
        direction == Vector2I.Up ? Vector2I.Right
        : direction == Vector2I.Right ? Vector2I.Down
        : direction == Vector2I.Down ? Vector2I.Left
        : Vector2I.Up;
}

using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MINIBOSS_PORTAL $7e:$00.</summary>
internal sealed class MinibossPortalRoomEntity :
    RoomEntityAdapter<MinibossPortal>, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity, IPlayerRestriction, IUpdatesDuringDialogueRoomEntity
{

    private readonly PlacementRecord _placement;
    private readonly DungeonEntranceInteractionDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly Action<Warp> _warpRequested;
    private readonly Action<int> _soundRequested;
    private PortalState _state;
    private int _counter;
    private Player? _controlledPlayer;

    internal MinibossPortalRoomEntity(
        MinibossPortal portal,
        PlacementRecord placement,
        DungeonEntranceInteractionDatabase data,
        OracleSaveData? save,
        Action<Warp> warpRequested,
        Action<int> soundRequested)
        : base(portal, portal.SetTransitionDrawOffset)
    {
        _placement = placement;
        _data = data;
        _save = save;
        _warpRequested = warpRequested;
        _soundRequested = soundRequested;
        portal.TreeExiting += ReleaseControl;
    }

    public bool Finished { get; private set; }
    private bool ControlsLink => _state is PortalState.Spinning or PortalState.WarpRequested;
    public bool DisablesSword => ControlsLink;
    public bool DisablesItems => ControlsLink;
    public bool DisablesMovement => ControlsLink;
    public bool DisablesMenus => ControlsLink;
    public bool DisablesPlayerContact => ControlsLink;
    public bool PassesNpcs => ControlsLink;
    public bool UpdatesDuringDialogue => _state == PortalState.Initialize;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || _state == PortalState.WarpRequested)
            return;

        if (_state == PortalState.Initialize)
        {
            if (!PreparePresentation())
                return;
            _state = Touching(frame.Player)
                ? PortalState.WaitForLinkToLeave
                : PortalState.Ready;
            return;
        }

        Entity.AdvanceAnimation();
        if (_state == PortalState.WaitForLinkToLeave)
        {
            if (!Touching(frame.Player))
                _state = PortalState.Ready;
            return;
        }
        if (_state == PortalState.Ready)
        {
            if (frame.Player.CanEnterMinibossPortal && Touching(frame.Player))
                BeginSpin(frame.Player);
            return;
        }

        frame.Player.CopyPortalPosition(Entity.Position);
        frame.Player.ResetPortalDamageState();
        if (frame.Player.IsDying) return;
        if ((frame.Counter & 0x03) == 0)
            frame.Player.Face(NextClockwise(frame.Player.FacingVector));
        _counter--;
        if (_counter != 0)
            return;

        PortalPair destination =
            _data.PortalPairFor(_placement.Dungeon);
        int destinationRoom = _placement.Room == destination.MinibossRoom
            ? destination.EntranceRoom
            : destination.MinibossRoom;
        _state = PortalState.WarpRequested;
        _warpRequested(new Warp(
            _placement.Group,
            _placement.Room,
            _data.PortalPosition,
            0,
            _data.PortalSourceTransition,
            _placement.Group,
            destinationRoom,
            _data.PortalPosition,
            _data.PortalDestinationParameter,
            _data.PortalDestinationTransition,
            // Writes wWarpTransition2=$03 directly after SND_TELEPORT;
            // Link never runs warpTransition2's SND_ENTERCAVE.
            DirectFadeOut: true));
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        PreparePresentation()
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;

    private bool PreparePresentation()
    {
        if (Finished)
            return false;
        PortalPair pair = _data.PortalPairFor(_placement.Dungeon);
        if (_save?.HasRoomFlag(
                _placement.Group, pair.MinibossRoom,
                OracleSaveData.RoomFlag80) != true)
        {
            Finished = true;
            return false;
        }
        Entity.Visible = true;
        Entity.QueueRedraw();
        return true;
    }


    private void BeginSpin(Player player)
    {
        player.ResetPortalDamageState();
        player.CopyPortalPosition(Entity.Position);
        player.RequestState08Control(this);
        _controlledPlayer = player;
        _state = PortalState.Spinning;
        _counter = _data.PortalSpinUpdates;
        _soundRequested(_data.PortalSound);
    }

    private bool Touching(Player player) => player.OverlapsMinibossPortal(
        new Rect2(Entity.Position - Vector2.One * _data.PortalRadius, Vector2.One * (_data.PortalRadius * 2)));

    private void ReleaseControl()
    {
        if (_controlledPlayer?.IsCutsceneControlOwner(this) == true)
        {
            _controlledPlayer.SetScriptedLinkAnimationMode(null);
            _controlledPlayer.EndCutsceneControl(this);
        }
        _controlledPlayer = null;
    }

    private static Vector2I NextClockwise(Vector2I direction) =>
        direction == Vector2I.Up ? Vector2I.Right
        : direction == Vector2I.Right ? Vector2I.Down
        : direction == Vector2I.Down ? Vector2I.Left
        : Vector2I.Up;
}

internal enum PortalState
{
    Initialize,
    Ready,
    WaitForLinkToLeave,
    Spinning,
    WarpRequested
}

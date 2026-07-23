using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MINIBOSS_PORTAL $7e:$00.</summary>
internal sealed class MinibossPortalRoomEntity :
    RoomEntityAdapter<MinibossPortal>, IFixedRoomEntity, IRoomEntityLifetime
{

    private readonly PlacementRecord _placement;
    private readonly DungeonEntranceInteractionDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly Action<Warp> _warpRequested;
    private readonly Action<int> _soundRequested;
    private PortalState _state;
    private int _counter;

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
            PortalPair pair =
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

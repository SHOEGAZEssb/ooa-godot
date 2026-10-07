using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_TOGGLE_FLOOR $15:$01, a state-zero pending landing write.</summary>
internal sealed partial class ToggleFloorTileRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly byte _tilePosition;
    private readonly byte _takeoffPosition;
    private readonly int _firstTile;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly Func<byte,byte,bool> _setTile;
    private readonly Action<int> _playSound;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal ToggleFloorTileRoomEntity(byte tilePosition,byte takeoffPosition,int firstTile,
        Func<OracleRoomData> activeRoom,Func<byte,byte,bool> setTile,Action<int> playSound)
    {
        _tilePosition = tilePosition;
        _takeoffPosition = takeoffPosition;
        _firstTile = firstTile;
        _activeRoom = activeRoom;
        _setTile = setTile;
        _playSound = playSound;
        Name = $"ToggleFloorTile_{tilePosition:x2}";
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player);
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance(frame.Player);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("INTERAC$15:$01 pending landing preload requires its live Link owner.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        Advance(player ?? throw new InvalidOperationException("INTERAC$15:$01 preload lost its live Link owner."));
        return ScreenTransitionPresentation.Hidden;
    }

    private void Advance(Player player)
    {
        if (Finished || ToggleFloorRoomEntity.LinkInAir(player)) return;
        if (ToggleFloorRoomEntity.LinkTilePosition(player) != _takeoffPosition)
        {
            // No enabled02 or repeat color check: a retained outgoing child
            // reads/writes the current room, including a replaced source tile.
            OracleRoomData room = _activeRoom();
            byte tile = unchecked((byte)(room.GetPackedStorageMetatile(_tilePosition)+1));
            if (tile >= _firstTile+3) tile = (byte)_firstTile;
            _setTile(_tilePosition,tile);
            // setTileInRoomLayoutBuffer is unconditional even if setTile's
            // changed-graphics queue rejected the preceding logical write.
            room.SetUnderlyingStorageMetatile(_tilePosition,tile);
            _playSound(SoundId.SndGetSeed);
        }
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}

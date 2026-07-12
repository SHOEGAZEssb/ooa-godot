using Godot;
using System;

namespace oracleofages;

public sealed class RoomTransitionController
{
    private enum WarpPhase { None, FadeOut, LeaveScreen, FadeIn }

    public const float WarpFadeFrames = 32.0f;
    public const float WarpLeaveFrames = 16.0f;
    public const float WarpEnterFrames = 28.0f;

    private readonly RoomSession _rooms;
    private readonly WarpDatabase _warps;
    private readonly RoomView _roomView;
    private readonly Player _player;
    private readonly Camera2D _camera;
    private readonly ColorRect _warpFade;
    private readonly Hud _hud;
    private readonly DialogueBox _dialogue;
    private readonly RoomEntityManager _entities;
    private readonly Func<Vector2, bool> _collides;

    private bool _scrollActive;
    private Vector2I _scrollDirection;
    private Vector2 _scrollLinkStart;
    private Vector2 _scrollLinkStep;
    private Vector2 _scrollFinishOffset;
    private float _scrollDistance;
    private float _scrollFrame;
    private int _scrollFrames;

    private int _deactivatedWarpGroup = -1;
    private int _deactivatedWarpRoom = -1;
    private int _deactivatedWarpPosition = -1;
    private bool _warpActive;
    private WarpPhase _warpPhase;
    private WarpDatabase.Warp _pendingWarp;
    private float _warpFrame;
    private Vector2 _warpWalkStart;
    private Vector2 _warpWalkEnd;
    private bool _destinationWalk;

    public bool IsTransitioning => _warpActive || _scrollActive || _roomView.IsTransitioning;
    public bool ScrollActive => _scrollActive;
    public Vector2I ScrollDirection => _scrollDirection;
    public float ScrollDistance => _scrollDistance;
    public int ScrollFrames => _scrollFrames;

    public RoomTransitionController(
        RoomSession rooms,
        WarpDatabase warps,
        RoomView roomView,
        Player player,
        Camera2D camera,
        ColorRect warpFade,
        Hud hud,
        DialogueBox dialogue,
        RoomEntityManager entities,
        Func<Vector2, bool> collides)
    {
        _rooms = rooms;
        _warps = warps;
        _roomView = roomView;
        _player = player;
        _camera = camera;
        _warpFade = warpFade;
        _hud = hud;
        _dialogue = dialogue;
        _entities = entities;
        _collides = collides;
    }

    public void Update(double delta)
    {
        UpdateWarp(delta);
        UpdateScroll(delta);
        UpdateCamera();
    }

    public bool CheckTileWarp(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        int position = room.GetPackedPosition(player.Position);
        if (_deactivatedWarpGroup == _rooms.ActiveGroup &&
            _deactivatedWarpRoom == room.Id && _deactivatedWarpPosition == position)
            return false;

        if (_deactivatedWarpGroup >= 0)
            ClearDeactivatedWarp();

        byte tile = room.GetMetatile(player.Position);
        if (!_warps.TryGetTileWarp(_rooms.ActiveGroup, room.Id, position, tile, out WarpDatabase.Warp warp))
            return false;
        ApplyWarp(player, warp);
        return true;
    }

    public void CheckRoomExit(Player player)
    {
        if (IsTransitioning)
            return;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 position = player.Position;
        Vector2I direction = position.Y <= 5 ? Vector2I.Up
            : position.Y > room.Height - 7 ? Vector2I.Down
            : position.X <= 5 ? Vector2I.Left
            : position.X > room.Width - 6 ? Vector2I.Right
            : Vector2I.Zero;
        if (direction == Vector2I.Zero)
            return;

        if (_warps.TryGetEdgeWarp(
            _rooms.ActiveGroup, room.Id, direction, position,
            new Vector2(room.Width, room.Height), out WarpDatabase.Warp warp))
        {
            ApplyWarp(player, warp);
            return;
        }
        if (!_rooms.TryGetNeighbor(direction, out int targetId) ||
            !_rooms.World.HasRoom(_rooms.ActiveGroup, targetId))
            return;

        BeginScroll(player, direction, targetId);
        _hud.Refresh();
    }

    public bool HasNeighborFor(Vector2 point)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2I direction = point.X < 0 ? Vector2I.Left
            : point.X >= room.Width ? Vector2I.Right
            : point.Y < 0 ? Vector2I.Up
            : Vector2I.Down;
        return _warps.HasEdgeWarp(_rooms.ActiveGroup, room.Id, direction) ||
            (_rooms.TryGetNeighbor(direction, out int id) && _rooms.World.HasRoom(_rooms.ActiveGroup, id));
    }

    public void BeginScroll(Player player, Vector2I direction, int targetId)
    {
        OracleRoomData source = _rooms.CurrentRoom;
        OracleRoomData target = _rooms.World.LoadRoom(_rooms.ActiveGroup, targetId);
        UpdateCamera();
        Vector2 sourceCameraOrigin = CurrentCameraOrigin;
        Vector2 start = player.Position;
        if (direction == Vector2I.Up) start.Y = 6;
        if (direction == Vector2I.Down) start.Y = source.Height - 7;
        if (direction == Vector2I.Left) start.X = 6;
        if (direction == Vector2I.Right) start.X = source.Width - 6;

        _scrollActive = true;
        _entities.Clear();
        _scrollDirection = direction;
        _scrollLinkStart = start;
        _scrollDistance = direction.X != 0 ? OracleRoomData.ViewportWidth : OracleRoomData.ViewportHeight;
        _scrollFrame = 0.0f;
        _scrollFrames = Mathf.Max(1, Mathf.RoundToInt(_scrollDistance / 4.0f));
        _scrollLinkStep = direction == Vector2I.Up ? new Vector2(0.0f, -0.5f)
            : direction == Vector2I.Right ? new Vector2(0.375f, 0.0f)
            : direction == Vector2I.Down ? new Vector2(0.0f, 0.5f)
            : new Vector2(-0.375f, 0.0f);
        _scrollFinishOffset = direction == Vector2I.Up ? new Vector2(0.0f, source.Height)
            : direction == Vector2I.Right ? new Vector2(-source.Width, 0.0f)
            : direction == Vector2I.Down ? new Vector2(0.0f, -source.Height)
            : new Vector2(source.Width, 0.0f);

        Vector2 transitionEnd = start + _scrollLinkStep * _scrollFrames + _scrollFinishOffset;
        Vector2 destinationCameraOrigin = GetCameraOrigin(target, transitionEnd);
        _rooms.SetLoadedRoom(_rooms.ActiveGroup, target);
        _roomView.StartScreenTransition(
            target.Texture, direction, _scrollDistance, sourceCameraOrigin, destinationCameraOrigin);
        player.BeginScrollingTransition(start, direction);
    }

    public void UpdateScroll(double delta)
    {
        if (!_scrollActive)
            return;
        _scrollFrame = Mathf.Min(_scrollFrames, _scrollFrame + (float)delta * 60.0f);
        Vector2 linkPosition = _scrollLinkStart + _scrollLinkStep * _scrollFrame;
        float scrollPixels = Mathf.Min(Mathf.Floor(_scrollFrame) * 4.0f, _scrollDistance);
        Vector2 screenScroll = new(_scrollDirection.X * scrollPixels, _scrollDirection.Y * scrollPixels);
        _roomView.SetTransitionFrame(_scrollFrame);
        _player.SetScrollingTransitionPosition(linkPosition, screenScroll);
        if (_scrollFrame < _scrollFrames)
            return;

        _scrollActive = false;
        _roomView.FinishTransition();
        _player.FinishScrollingTransition(linkPosition + _scrollFinishOffset);
        _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
        UpdateCamera();
    }

    public void ApplyWarp(Player player, WarpDatabase.Warp warp)
    {
        if (_warpActive || !_rooms.World.HasRoom(warp.DestinationGroup, warp.DestinationRoom))
            return;
        _dialogue.Close();
        _pendingWarp = warp;
        _warpActive = true;
        _warpFrame = 0.0f;
        _destinationWalk = false;
        player.BeginRoomWarpTransition();
        switch (warp.SourceTransition)
        {
            case 2:
                _warpPhase = WarpPhase.FadeOut;
                SetFade(0.0f);
                break;
            case 3:
                _warpPhase = WarpPhase.LeaveScreen;
                _warpWalkStart = player.Position;
                _warpWalkEnd = player.Position + (Vector2)player.FacingVector * WarpLeaveFrames;
                player.BeginRoomWarpWalk(player.Position, player.FacingVector);
                break;
            default:
                SetFade(1.0f);
                LoadWarpDestination();
                break;
        }
    }

    public void UpdateWarp(double delta)
    {
        if (!_warpActive)
            return;
        _warpFrame += (float)delta * 60.0f;
        switch (_warpPhase)
        {
            case WarpPhase.FadeOut:
                SetFade(_warpFrame / WarpFadeFrames);
                if (_warpFrame >= WarpFadeFrames)
                {
                    SetFade(1.0f);
                    LoadWarpDestination();
                }
                break;
            case WarpPhase.LeaveScreen:
                float leaveFrame = Mathf.Min(_warpFrame, WarpLeaveFrames);
                _player.SetRoomWarpWalkPosition(
                    _warpWalkStart.Lerp(_warpWalkEnd, leaveFrame / WarpLeaveFrames), delta);
                if (_warpFrame >= WarpLeaveFrames)
                {
                    SetFade(1.0f);
                    LoadWarpDestination();
                }
                break;
            case WarpPhase.FadeIn:
                if (_destinationWalk)
                {
                    float enterFrame = Mathf.Min(_warpFrame, WarpEnterFrames);
                    _player.SetRoomWarpWalkPosition(
                        _warpWalkStart.Lerp(_warpWalkEnd, enterFrame / WarpEnterFrames), delta);
                }
                SetFade(1.0f - _warpFrame / WarpFadeFrames);
                if (_warpFrame >= WarpFadeFrames)
                    FinishWarp();
                break;
        }
    }

    private void LoadWarpDestination()
    {
        WarpDatabase.Warp warp = _pendingWarp;
        OracleRoomData room = _rooms.Load(warp.DestinationGroup, warp.DestinationRoom);
        _roomView.SetRoom(room.Texture);
        _entities.LoadRoom(_rooms.ActiveGroup, room);

        Vector2 spawn;
        if (warp.DestinationTransition == 3)
        {
            Vector2I direction = (warp.DestinationParameter & 0x04) != 0 ? Vector2I.Down : Vector2I.Up;
            if (warp.DestinationPosition == 0xff)
            {
                float middleX = _rooms.ActiveGroup >= 4 ? 0x78 : 0x50;
                spawn = new Vector2(middleX, direction == Vector2I.Up ? room.Height : -16.0f);
            }
            else if ((warp.DestinationPosition & 0xf0) == 0xf0)
            {
                float x = (warp.DestinationPosition & 0x0f) * OracleRoomData.MetatileSize;
                if (_rooms.ActiveGroup >= 4) x += 8.0f;
                spawn = new Vector2(x, direction == Vector2I.Up ? room.Height : -16.0f);
            }
            else
            {
                int tileX = warp.DestinationPosition & 0x0f;
                int tileY = (warp.DestinationPosition >> 4) & 0x0f;
                spawn = new Vector2(tileX * OracleRoomData.MetatileSize + 8, tileY * OracleRoomData.MetatileSize + 8);
            }
            _warpWalkStart = spawn;
            _warpWalkEnd = spawn + (Vector2)direction * WarpEnterFrames;
            _destinationWalk = true;
            _player.BeginRoomWarpWalk(spawn, direction);
            ClearDeactivatedWarp();
        }
        else
        {
            int tileX = warp.DestinationPosition & 0x0f;
            int tileY = (warp.DestinationPosition >> 4) & 0x0f;
            spawn = new Vector2(tileX * OracleRoomData.MetatileSize + 8, tileY * OracleRoomData.MetatileSize + 8);
            byte destinationTile = room.GetMetatile(spawn);
            if (ShouldStepOut(warp, destinationTile, tileX, tileY))
            {
                spawn.Y += OracleRoomData.MetatileSize;
                ClearDeactivatedWarp();
                _player.Face(Vector2I.Down);
            }
            else
            {
                _deactivatedWarpGroup = _rooms.ActiveGroup;
                _deactivatedWarpRoom = room.Id;
                _deactivatedWarpPosition = warp.DestinationPosition;
                FaceForDestinationTile(_player, destinationTile);
            }
            _player.WarpTo(spawn);
        }

        _warpPhase = WarpPhase.FadeIn;
        _warpFrame = 0.0f;
        UpdateCamera();
        _hud.Refresh();
    }

    private void FinishWarp()
    {
        _player.FinishRoomWarpTransition(_destinationWalk ? _warpWalkEnd : _player.Position);
        _destinationWalk = false;
        _warpActive = false;
        _warpPhase = WarpPhase.None;
        SetFade(0.0f);
    }

    public void ClearDeactivatedWarp()
    {
        _deactivatedWarpGroup = -1;
        _deactivatedWarpRoom = -1;
        _deactivatedWarpPosition = -1;
    }

    public void UpdateCamera()
    {
        if (_scrollActive)
            return;
        Vector2 origin = GetCameraOrigin(_rooms.CurrentRoom, _player.Position);
        _camera.Position = origin + new Vector2(OracleRoomData.ViewportWidth / 2.0f, 72.0f);
    }

    public Vector2 WorldToScreen(Vector2 worldPosition) => worldPosition - CurrentCameraOrigin;

    private Vector2 CurrentCameraOrigin => _camera.Position -
        new Vector2(OracleRoomData.ViewportWidth / 2.0f, 72.0f);

    private static Vector2 GetCameraOrigin(OracleRoomData room, Vector2 playerPosition)
    {
        float maxX = Mathf.Max(0.0f, room.Width - OracleRoomData.ViewportWidth);
        float maxY = Mathf.Max(0.0f, room.Height - OracleRoomData.ViewportHeight);
        return new Vector2(
            Mathf.Clamp(playerPosition.X - OracleRoomData.ViewportWidth / 2.0f, 0.0f, maxX),
            Mathf.Clamp(playerPosition.Y - OracleRoomData.ViewportHeight / 2.0f, 0.0f, maxY));
    }

    private void SetFade(float alpha) =>
        _warpFade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));

    private bool ShouldStepOut(WarpDatabase.Warp warp, byte destinationTile, int tileX, int tileY)
    {
        if (warp.SourceTransition != 3 || _rooms.ActiveGroup is not (0 or 1) ||
            destinationTile is not (0xdc or 0xdd or 0xde or 0xdf or 0xed or 0xee or 0xef))
            return false;
        Vector2 steppedOut = new(
            tileX * OracleRoomData.MetatileSize + 8,
            (tileY + 1) * OracleRoomData.MetatileSize + 8);
        return steppedOut.Y < _rooms.CurrentRoom.Height && !_collides(steppedOut);
    }

    private static void FaceForDestinationTile(Player player, byte tile)
    {
        if (tile == 0x36) player.Face(Vector2I.Up);
        else if (tile == 0x44) player.Face(Vector2I.Left);
        else if (tile == 0x45) player.Face(Vector2I.Right);
    }
}

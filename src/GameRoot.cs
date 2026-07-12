using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class GameRoot : Node2D
{
    private enum RoomWarpPhase { None, FadeOut, LeaveScreen, FadeIn }

    private const float WarpFadeFrames = 32.0f;
    private const float WarpLeaveFrames = 16.0f;
    private const float WarpEnterFrames = 28.0f;
    private OracleWorldData _world = null!;
    private OracleRoomData _currentRoom = null!;
    private RoomView _roomView = null!;
    private Player _player = null!;
    private Camera2D _roomCamera = null!;
    private CanvasLayer _uiLayer = null!;
    private Hud _hud = null!;
    private Label _roomDebug = null!;
    private ColorRect _warpFade = null!;
    private SignDatabase _signs = null!;
    private NpcDatabase _npcs = null!;
    private WarpDatabase _warps = null!;
    private DungeonMapDatabase _dungeonMaps = null!;
    private DialogueBox _dialogue = null!;
    private readonly List<NpcCharacter> _npcNodes = new();
    private double _animationTicks;
    private int _activeGroup;
    private bool _scrollTransitionActive;
    private Vector2I _scrollTransitionDirection;
    private Vector2 _scrollTransitionLinkStart;
    private Vector2 _scrollTransitionLinkStep;
    private Vector2 _scrollTransitionFinishOffset;
    private float _scrollTransitionDistance;
    private float _scrollTransitionFrame;
    private int _scrollTransitionFrames;
    private int _deactivatedWarpGroup = -1;
    private int _deactivatedWarpRoom = -1;
    private int _deactivatedWarpPosition = -1;
    private bool _roomWarpTransitionActive;
    private RoomWarpPhase _roomWarpPhase;
    private WarpDatabase.Warp _pendingWarp;
    private float _roomWarpFrame;
    private Vector2 _roomWarpWalkStart;
    private Vector2 _roomWarpWalkEnd;
    private bool _roomWarpDestinationWalk;

    public bool IsTransitioning => _roomWarpTransitionActive || _scrollTransitionActive || _roomView.IsTransitioning;
    public bool DialogueOpen => _dialogue.IsOpen;

    public readonly record struct ActiveTerrainInfo(
        OracleRoomData.TerrainInfo Terrain,
        Vector2 SamplePoint,
        Vector2 TileCenter,
        int PackedPosition);

    public override void _Ready()
    {
        _world = new OracleWorldData();
        _signs = new SignDatabase();
        _npcs = new NpcDatabase();
        _warps = new WarpDatabase();
        _dungeonMaps = new DungeonMapDatabase();
        _activeGroup = GetStartingGroup();
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-world"))
            _world.ValidateRepresentativeRooms();
        _currentRoom = _world.LoadRoom(_activeGroup, GetStartingRoom());

        _roomView = new RoomView { Name = "RoomView", ZIndex = 0 };
        AddChild(_roomView);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        _player = new Player { Name = "Link", ZIndex = 10 };
        AddChild(_player);
        _player.Initialize(this, FindSpawn());
        _player.HealthChanged += SyncHudToPlayer;

        _roomCamera = new Camera2D
        {
            Name = "RoomCamera",
            Enabled = true,
            PositionSmoothingEnabled = false
        };
        AddChild(_roomCamera);
        UpdateRoomCamera();

        // The original status bar and textbox use the window, not room,
        // coordinates. Keep them outside the large-room camera transform.
        _uiLayer = new CanvasLayer { Name = "Interface", Layer = 10 };
        AddChild(_uiLayer);

        _hud = new Hud { Name = "Hud", Position = new Vector2(0, 128), ZIndex = 20 };
        _uiLayer.AddChild(_hud);
        SyncHudToPlayer();

        // Room palette fades do not affect the status bar in the original engine.
        _warpFade = new ColorRect
        {
            Name = "RoomWarpFade",
            Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ViewportHeight),
            Color = new Color(1, 1, 1, 0),
            ZIndex = 15,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _uiLayer.AddChild(_warpFade);

        _dialogue = new DialogueBox { Name = "Dialogue", ZIndex = 30, Visible = false };
        _uiLayer.AddChild(_dialogue);

        _roomDebug = new Label
        {
            Name = "RoomDebug",
            Position = new Vector2(2, 0),
            ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _roomDebug.AddThemeFontSizeOverride("font_size", 8);
        _roomDebug.AddThemeColorOverride("font_color", Color.Color8(255, 248, 207));
        _roomDebug.AddThemeColorOverride("font_outline_color", Color.Color8(20, 24, 20));
        _roomDebug.AddThemeConstantOverride("outline_size", 1);
        _uiLayer.AddChild(_roomDebug);

        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-transition"))
            CallDeferred(MethodName.ValidateStartupTransition);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-symmetry-transition"))
            CallDeferred(MethodName.ValidateSymmetryTransition);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-signs"))
            CallDeferred(MethodName.ValidateSigns);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-npcs"))
            CallDeferred(MethodName.ValidateNpcs);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-animations"))
            CallDeferred(MethodName.ValidateAnimations);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-sword-bush"))
            CallDeferred(MethodName.ValidateSwordBush);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-house-warp"))
            CallDeferred(MethodName.ValidateHouseWarp);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-cave-warps"))
            CallDeferred(MethodName.ValidateCaveWarps);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-terrain"))
            CallDeferred(MethodName.ValidateTerrain);
        if (Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--validate-health"))
            CallDeferred(MethodName.ValidateHealth);
    }

    public override void _Process(double delta)
    {
        UpdateRoomWarpTransition(delta);
        UpdateScrollingTransition(delta);
        UpdateRoomCamera();
        string roomText = $"{_activeGroup:x1}:{_currentRoom.Id:x2}";
        if (_roomDebug.Text != roomText)
            _roomDebug.Text = roomText;
        _animationTicks += delta * 60.0;
        if (_currentRoom.UpdateAnimation((long)_animationTicks))
            _roomView.QueueRedraw();
        if (_player != null)
        {
            foreach (NpcCharacter npc in _npcNodes)
                npc.UpdateNpc(delta, _player.Position);
        }
        if (Input.IsActionJustPressed("debug_sign"))
            WarpToSignTest();
        if (Input.IsActionJustPressed("debug_animation"))
            WarpToAnimationTest();
        if (Input.IsActionJustPressed("debug_bush"))
            WarpToBushTest();
        if (Input.IsActionJustPressed("debug_house"))
            WarpToHouseTest();
    }

    private void SyncHudToPlayer()
    {
        if (_hud == null || _player == null)
            return;

        if (_hud.HealthQuarters == _player.HealthQuarters &&
            _hud.MaxHealthQuarters == _player.MaxHealthQuarters)
            return;

        _hud.HealthQuarters = _player.HealthQuarters;
        _hud.MaxHealthQuarters = _player.MaxHealthQuarters;
        _hud.Refresh();
    }

    public bool ApplySwordHit(Player player, Rect2 hitbox)
    {
        Vector2 offset = player.FacingVector == Vector2I.Up ? new Vector2(0, -14)
            : player.FacingVector == Vector2I.Right ? new Vector2(13, 0)
            : player.FacingVector == Vector2I.Down ? new Vector2(0, 13)
            : new Vector2(-14, 0);
        Vector2 point = player.Position + offset;
        int tileX = Mathf.FloorToInt(point.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(point.Y / OracleRoomData.MetatileSize);
        Rect2 tileBounds = new(
            tileX * OracleRoomData.MetatileSize,
            tileY * OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize,
            OracleRoomData.MetatileSize);
        if (!hitbox.Intersects(tileBounds) ||
            !_currentRoom.ReplaceMetatile(point, 0xc5, 0x3a, (long)_animationTicks))
            return false;

        var effect = new BushCutEffect
        {
            Position = tileBounds.GetCenter(),
            ZIndex = 12
        };
        AddChild(effect);
        _roomView.QueueRedraw();
        return true;
    }

    public bool TryInteract(Player player)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (!npc.CanTalkTo(player))
                continue;

            npc.FaceToward(player.Position);
            _dialogue.ShowMessage(npc.Message, WorldToScreen(player.Position).Y);
            return true;
        }

        Vector2 signPoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        if (_currentRoom.GetMetatile(signPoint) != 0xf2)
            return false;

        // The original accepts A beside a sign from any side, but only reveals
        // its message when Link is below it and facing up.
        string message;
        if (player.FacingVector != Vector2I.Up)
        {
            message = "You can't read it\nfrom this side!"; // TX_510e
        }
        else if (!_signs.TryGetMessage(
            _activeGroup, _currentRoom.Id, _currentRoom.GetPackedPosition(signPoint), out message!))
        {
            message = "Nothing is written\nhere."; // TX_0901 fallback
        }

        _dialogue.ShowMessage(message, WorldToScreen(player.Position).Y);
        return true;
    }

    public bool Collides(Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= _currentRoom.Width ||
                sample.Y < 0 || sample.Y >= _currentRoom.Height)
            {
                if (!HasNeighborFor(sample))
                    return true;
                continue;
            }

            if (_currentRoom.IsSolid(sample))
                return true;
        }

        return NpcBlocksLinkCenter(playerPosition);
    }

    private bool NpcBlocksLinkCenter(Vector2 linkCenter)
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            if (npc.BlocksLinkCenter(linkCenter))
                return true;
        }
        return false;
    }

    public OracleRoomData.TerrainInfo GetTerrainInfo(Vector2 playerPosition)
    {
        return _currentRoom.GetTerrainInfo(playerPosition + new Vector2(0, 5));
    }

    public ActiveTerrainInfo GetActiveTerrain(Vector2 playerPosition)
    {
        Vector2 sample = playerPosition + new Vector2(0, 5);
        int tileX = Mathf.FloorToInt(sample.X / OracleRoomData.MetatileSize);
        int tileY = Mathf.FloorToInt(sample.Y / OracleRoomData.MetatileSize);
        Vector2 center = new(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8);
        return new ActiveTerrainInfo(
            _currentRoom.GetTerrainInfo(sample),
            sample,
            center,
            (tileY << 4) | tileX);
    }

    public Vector2 GetTerrainPush(Vector2 playerPosition)
    {
        OracleRoomData.TerrainType terrain = GetTerrainInfo(playerPosition).Type;
        const float pushSpeed = 32.0f;
        return terrain switch
        {
            OracleRoomData.TerrainType.UpCurrent or OracleRoomData.TerrainType.UpConveyor => new Vector2(0, -pushSpeed),
            OracleRoomData.TerrainType.RightCurrent or OracleRoomData.TerrainType.RightConveyor => new Vector2(pushSpeed, 0),
            OracleRoomData.TerrainType.DownCurrent or OracleRoomData.TerrainType.DownConveyor => new Vector2(0, pushSpeed),
            OracleRoomData.TerrainType.LeftCurrent or OracleRoomData.TerrainType.LeftConveyor => new Vector2(-pushSpeed, 0),
            _ => Vector2.Zero
        };
    }

    public bool TryStartLedgeHop(Player player, Vector2 from, Vector2 attemptedMovement)
    {
        Vector2I direction;
        if (Mathf.Abs(attemptedMovement.X) > Mathf.Abs(attemptedMovement.Y))
            direction = attemptedMovement.X > 0 ? Vector2I.Right : Vector2I.Left;
        else
            direction = attemptedMovement.Y > 0 ? Vector2I.Down : Vector2I.Up;

        if (player.FacingVector != direction)
            return false;

        Vector2 ledgePoint = from + (Vector2)direction * 12.0f;
        if (!IsCliffTile(_currentRoom.GetMetatile(ledgePoint), direction))
            return false;

        Vector2 landing = from + (Vector2)direction * (OracleRoomData.MetatileSize * 2);
        if (landing.X < 0 || landing.X >= _currentRoom.Width ||
            landing.Y < 0 || landing.Y >= _currentRoom.Height ||
            Collides(landing))
            return false;

        player.StartLedgeHop(landing);
        return true;
    }

    public void SpawnTerrainEffect(Vector2 position, OracleRoomData.HazardType hazard)
    {
        var effect = new TerrainEffect
        {
            Position = position,
            ZIndex = 11
        };
        effect.Initialize(hazard);
        AddChild(effect);
    }

    public bool CheckTileWarp(Player player)
    {
        int position = _currentRoom.GetPackedPosition(player.Position);
        if (_deactivatedWarpGroup == _activeGroup &&
            _deactivatedWarpRoom == _currentRoom.Id &&
            _deactivatedWarpPosition == position)
            return false;

        if (_deactivatedWarpGroup >= 0)
            ClearDeactivatedWarp();

        byte tile = _currentRoom.GetMetatile(player.Position);
        if (!_warps.TryGetTileWarp(_activeGroup, _currentRoom.Id, position, tile, out WarpDatabase.Warp warp))
            return false;

        ApplyWarp(player, warp);
        return true;
    }

    public void CheckRoomExit(Player player)
    {
        if (IsTransitioning)
            return;

        Vector2 position = player.Position;
        Vector2I direction = position.Y <= 5 ? Vector2I.Up
            : position.Y > _currentRoom.Height - 7 ? Vector2I.Down
            : position.X <= 5 ? Vector2I.Left
            : position.X > _currentRoom.Width - 6 ? Vector2I.Right
            : Vector2I.Zero;

        if (direction == Vector2I.Zero)
            return;

        if (_warps.TryGetEdgeWarp(
            _activeGroup, _currentRoom.Id, direction, position,
            new Vector2(_currentRoom.Width, _currentRoom.Height), out WarpDatabase.Warp warp))
        {
            ApplyWarp(player, warp);
            return;
        }

        if (!TryGetNeighborId(direction, out int targetId) || !_world.HasRoom(_activeGroup, targetId))
            return;

        BeginScrollingTransition(player, direction, targetId);
        _hud.Refresh();
    }

    private void BeginScrollingTransition(Player player, Vector2I direction, int targetId)
    {
        OracleRoomData source = _currentRoom;
        OracleRoomData target = _world.LoadRoom(_activeGroup, targetId);
        UpdateRoomCamera();
        Vector2 sourceCameraOrigin = GetCurrentCameraOrigin();
        Vector2 start = player.Position;
        if (direction == Vector2I.Up) start.Y = 6;
        if (direction == Vector2I.Down) start.Y = source.Height - 7;
        if (direction == Vector2I.Left) start.X = 6;
        if (direction == Vector2I.Right) start.X = source.Width - 6;

        _scrollTransitionActive = true;
        ClearRoomObjects();
        _scrollTransitionDirection = direction;
        _scrollTransitionLinkStart = start;
        _scrollTransitionDistance = direction.X != 0
            ? OracleRoomData.ViewportWidth
            : OracleRoomData.ViewportHeight;
        _scrollTransitionFrame = 0.0f;
        _scrollTransitionFrames = Mathf.Max(1, Mathf.RoundToInt(_scrollTransitionDistance / 4.0f));
        _scrollTransitionLinkStep = direction == Vector2I.Up ? new Vector2(0.0f, -0.5f)
            : direction == Vector2I.Right ? new Vector2(0.375f, 0.0f)
            : direction == Vector2I.Down ? new Vector2(0.0f, 0.5f)
            : new Vector2(-0.375f, 0.0f);
        _scrollTransitionFinishOffset = direction == Vector2I.Up ? new Vector2(0.0f, source.Height)
            : direction == Vector2I.Right ? new Vector2(-source.Width, 0.0f)
            : direction == Vector2I.Down ? new Vector2(0.0f, -source.Height)
            : new Vector2(source.Width, 0.0f);

        Vector2 transitionEnd = start + _scrollTransitionLinkStep * _scrollTransitionFrames +
            _scrollTransitionFinishOffset;
        Vector2 destinationCameraOrigin = GetRoomCameraOrigin(target, transitionEnd);

        _currentRoom = target;
        _roomView.StartScreenTransition(
            target.Texture, direction, _scrollTransitionDistance,
            sourceCameraOrigin, destinationCameraOrigin);
        player.BeginScrollingTransition(start, direction);
    }

    private void UpdateScrollingTransition(double delta)
    {
        if (!_scrollTransitionActive)
            return;

        _scrollTransitionFrame = Mathf.Min(
            _scrollTransitionFrames,
            _scrollTransitionFrame + (float)delta * 60.0f);
        Vector2 linkPosition = _scrollTransitionLinkStart + _scrollTransitionLinkStep * _scrollTransitionFrame;
        float scrollPixels = Mathf.Min(Mathf.Floor(_scrollTransitionFrame) * 4.0f, _scrollTransitionDistance);
        Vector2 screenScroll = new(
            _scrollTransitionDirection.X * scrollPixels,
            _scrollTransitionDirection.Y * scrollPixels);
        _roomView.SetTransitionFrame(_scrollTransitionFrame);
        _player.SetScrollingTransitionPosition(linkPosition, screenScroll);

        if (_scrollTransitionFrame < _scrollTransitionFrames)
            return;

        _scrollTransitionActive = false;
        _roomView.FinishTransition();
        _player.FinishScrollingTransition(linkPosition + _scrollTransitionFinishOffset);
        RefreshRoomObjects();
        UpdateRoomCamera();
    }

    private bool HasNeighborFor(Vector2 point)
    {
        Vector2I direction = point.X < 0 ? Vector2I.Left
            : point.X >= _currentRoom.Width ? Vector2I.Right
            : point.Y < 0 ? Vector2I.Up
            : Vector2I.Down;
        return _warps.HasEdgeWarp(_activeGroup, _currentRoom.Id, direction) ||
            (TryGetNeighborId(direction, out int id) && _world.HasRoom(_activeGroup, id));
    }

    private void ApplyWarp(Player player, WarpDatabase.Warp warp)
    {
        if (_roomWarpTransitionActive || !_world.HasRoom(warp.DestinationGroup, warp.DestinationRoom))
            return;

        _dialogue.Close();
        _pendingWarp = warp;
        _roomWarpTransitionActive = true;
        _roomWarpFrame = 0.0f;
        _roomWarpDestinationWalk = false;
        player.BeginRoomWarpTransition();

        switch (warp.SourceTransition)
        {
            case 2: // TRANSITION_SRC_FADEOUT
                _roomWarpPhase = RoomWarpPhase.FadeOut;
                SetRoomWarpFade(0.0f);
                break;
            case 3: // TRANSITION_SRC_LEAVESCREEN
                _roomWarpPhase = RoomWarpPhase.LeaveScreen;
                _roomWarpWalkStart = player.Position;
                _roomWarpWalkEnd = player.Position + (Vector2)player.FacingVector * WarpLeaveFrames;
                player.BeginRoomWarpWalk(player.Position, player.FacingVector);
                break;
            case 4: // TRANSITION_SRC_INSTANT: immediately make the room white
                SetRoomWarpFade(1.0f);
                LoadRoomWarpDestination();
                break;
            default:
                SetRoomWarpFade(1.0f);
                LoadRoomWarpDestination();
                break;
        }
    }

    private void UpdateRoomWarpTransition(double delta)
    {
        if (!_roomWarpTransitionActive)
            return;

        _roomWarpFrame += (float)delta * 60.0f;
        switch (_roomWarpPhase)
        {
            case RoomWarpPhase.FadeOut:
                SetRoomWarpFade(_roomWarpFrame / WarpFadeFrames);
                if (_roomWarpFrame >= WarpFadeFrames)
                {
                    SetRoomWarpFade(1.0f);
                    LoadRoomWarpDestination();
                }
                break;

            case RoomWarpPhase.LeaveScreen:
            {
                float frame = Mathf.Min(_roomWarpFrame, WarpLeaveFrames);
                Vector2 position = _roomWarpWalkStart.Lerp(
                    _roomWarpWalkEnd, frame / WarpLeaveFrames);
                _player.SetRoomWarpWalkPosition(position, delta);
                if (_roomWarpFrame >= WarpLeaveFrames)
                {
                    SetRoomWarpFade(1.0f);
                    LoadRoomWarpDestination();
                }
                break;
            }

            case RoomWarpPhase.FadeIn:
            {
                if (_roomWarpDestinationWalk)
                {
                    float frame = Mathf.Min(_roomWarpFrame, WarpEnterFrames);
                    Vector2 position = _roomWarpWalkStart.Lerp(
                        _roomWarpWalkEnd, frame / WarpEnterFrames);
                    _player.SetRoomWarpWalkPosition(position, delta);
                }
                SetRoomWarpFade(1.0f - _roomWarpFrame / WarpFadeFrames);
                if (_roomWarpFrame >= WarpFadeFrames)
                    FinishRoomWarpTransition();
                break;
            }
        }
    }

    private void LoadRoomWarpDestination()
    {
        WarpDatabase.Warp warp = _pendingWarp;
        _activeGroup = warp.DestinationGroup;
        _currentRoom = _world.LoadRoom(_activeGroup, warp.DestinationRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();

        Vector2 spawn;
        if (warp.DestinationTransition == 3)
        {
            // Bit 2 of the destination parameter becomes bit 6 of
            // wWarpTransition and selects down rather than up.
            Vector2I direction = (warp.DestinationParameter & 0x04) != 0
                ? Vector2I.Down
                : Vector2I.Up;
            if (warp.DestinationPosition == 0xff)
            {
                float middleX = _activeGroup >= 4 ? 0x78 : 0x50;
                spawn = new Vector2(
                    middleX,
                    direction == Vector2I.Up ? _currentRoom.Height : -16.0f);
            }
            else if ((warp.DestinationPosition & 0xf0) == 0xf0)
            {
                float x = (warp.DestinationPosition & 0x0f) * OracleRoomData.MetatileSize;
                if (_activeGroup >= 4)
                    x += 8.0f;
                spawn = new Vector2(x, direction == Vector2I.Up ? _currentRoom.Height : -16.0f);
            }
            else
            {
                int tileX = warp.DestinationPosition & 0x0f;
                int tileY = (warp.DestinationPosition >> 4) & 0x0f;
                spawn = new Vector2(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
            }
            _roomWarpWalkStart = spawn;
            _roomWarpWalkEnd = spawn + (Vector2)direction * WarpEnterFrames;
            _roomWarpDestinationWalk = true;
            _player.BeginRoomWarpWalk(spawn, direction);
            ClearDeactivatedWarp();
        }
        else
        {
            int tileX = warp.DestinationPosition & 0x0f;
            int tileY = (warp.DestinationPosition >> 4) & 0x0f;
            spawn = new Vector2(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            byte destinationTile = _currentRoom.GetMetatile(spawn);
            if (ShouldStepOutFromExteriorEntrance(warp, destinationTile, tileX, tileY))
            {
                spawn.Y += OracleRoomData.MetatileSize;
                ClearDeactivatedWarp();
                _player.Face(Vector2I.Down);
            }
            else
            {
                _deactivatedWarpGroup = _activeGroup;
                _deactivatedWarpRoom = _currentRoom.Id;
                _deactivatedWarpPosition = warp.DestinationPosition;
                FaceForDestinationTile(_player, destinationTile);
            }
            _player.WarpTo(spawn);
        }

        _roomWarpPhase = RoomWarpPhase.FadeIn;
        _roomWarpFrame = 0.0f;
        UpdateRoomCamera();
        _hud.Refresh();
    }

    private void FinishRoomWarpTransition()
    {
        if (_roomWarpDestinationWalk)
            _player.FinishRoomWarpTransition(_roomWarpWalkEnd);
        else
            _player.FinishRoomWarpTransition(_player.Position);
        _roomWarpDestinationWalk = false;
        _roomWarpTransitionActive = false;
        _roomWarpPhase = RoomWarpPhase.None;
        SetRoomWarpFade(0.0f);
    }

    private void SetRoomWarpFade(float alpha)
    {
        _warpFade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));
    }

    private void UpdateRoomCamera()
    {
        if (_roomCamera == null || _player == null || _currentRoom == null)
            return;
        if (_scrollTransitionActive)
            return;

        Vector2 origin = GetRoomCameraOrigin(_currentRoom, _player.Position);

        // Camera2D anchors to the full 160x144 viewport. Adding its centre
        // preserves (0,0) as the playfield origin while the HUD masks y=128+.
        _roomCamera.Position = origin + new Vector2(
            OracleRoomData.ViewportWidth / 2.0f,
            144.0f / 2.0f);
    }

    private static Vector2 GetRoomCameraOrigin(OracleRoomData room, Vector2 playerPosition)
    {
        float maxX = Mathf.Max(0.0f, room.Width - OracleRoomData.ViewportWidth);
        float maxY = Mathf.Max(0.0f, room.Height - OracleRoomData.ViewportHeight);
        return new Vector2(
            Mathf.Clamp(playerPosition.X - OracleRoomData.ViewportWidth / 2.0f, 0.0f, maxX),
            Mathf.Clamp(playerPosition.Y - OracleRoomData.ViewportHeight / 2.0f, 0.0f, maxY));
    }

    private Vector2 GetCurrentCameraOrigin()
    {
        return _roomCamera.Position - new Vector2(
            OracleRoomData.ViewportWidth / 2.0f,
            144.0f / 2.0f);
    }

    private Vector2 WorldToScreen(Vector2 worldPosition)
    {
        return worldPosition - GetCurrentCameraOrigin();
    }

    private static void FaceForDestinationTile(Player player, byte tile)
    {
        if (tile == 0x36)
            player.Face(Vector2I.Up);
        else if (tile == 0x44)
            player.Face(Vector2I.Left);
        else if (tile == 0x45)
            player.Face(Vector2I.Right);
    }

    private bool ShouldStepOutFromExteriorEntrance(
        WarpDatabase.Warp warp,
        byte destinationTile,
        int tileX,
        int tileY)
    {
        if (warp.SourceTransition != 3 || _activeGroup is not (0 or 1) ||
            !IsOverworldWarpTile(destinationTile))
            return false;

        Vector2 steppedOut = new(
            tileX * OracleRoomData.MetatileSize + 8,
            (tileY + 1) * OracleRoomData.MetatileSize + 8);
        return steppedOut.Y < _currentRoom.Height && !Collides(steppedOut);
    }

    private static bool IsOverworldWarpTile(byte tile)
    {
        return tile is 0xdc or 0xdd or 0xde or 0xdf or 0xed or 0xee or 0xef;
    }

    private static bool IsCliffTile(byte tile, Vector2I direction)
    {
        if (direction == Vector2I.Down && tile is 0x05 or 0x06 or 0x07 or 0x64 or 0xff or 0xb0 or 0xc1)
            return true;
        if (direction == Vector2I.Left && tile is 0x0a or 0xb1 or 0xc2)
            return true;
        if (direction == Vector2I.Up && tile is 0xb2 or 0xc3)
            return true;
        if (direction == Vector2I.Right && tile is 0x0b or 0xb3 or 0xc4)
            return true;
        return false;
    }

    private void ClearDeactivatedWarp()
    {
        _deactivatedWarpGroup = -1;
        _deactivatedWarpRoom = -1;
        _deactivatedWarpPosition = -1;
    }

    private void RefreshRoomObjects()
    {
        ClearRoomObjects();
        foreach (NpcDatabase.NpcRecord record in _npcs.GetRoomNpcs(_activeGroup, _currentRoom.Id))
        {
            var npc = new NpcCharacter
            {
                Name = $"Npc_{record.Id:x2}_{record.SubId:x2}",
                ZIndex = 9
            };
            npc.Initialize(record);
            _npcNodes.Add(npc);
            AddChild(npc);
        }
    }

    private void ClearRoomObjects()
    {
        foreach (NpcCharacter npc in _npcNodes)
        {
            RemoveChild(npc);
            npc.QueueFree();
        }
        _npcNodes.Clear();
    }

    private bool TryGetNeighborId(Vector2I direction, out int id)
    {
        int dungeon = _world.GetDungeonIndex(_activeGroup, _currentRoom.Id);
        if (dungeon >= 0)
            return _dungeonMaps.TryGetNeighbor(dungeon, _currentRoom.Id, direction, out id);

        int x = _currentRoom.Id & 0x0f;
        int y = (_currentRoom.Id >> 4) & 0x0f;
        x += direction.X;
        y += direction.Y;
        if (x < 0 || x > 15 || y < 0 || y > 15)
        {
            id = -1;
            return false;
        }
        id = (y << 4) | x;
        return true;
    }

    private Vector2 FindSpawn()
    {
        Vector2 center = new(80, 64);
        Vector2 best = center;
        float bestDistance = float.MaxValue;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 10; x++)
        {
            Vector2 candidate = new(x * 16 + 8, y * 16 + 8);
            if (Collides(candidate))
                continue;
            float distance = candidate.DistanceSquaredTo(center);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void WarpToSignTest()
    {
        _dialogue.Close();
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x2a);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 70));
        _player.Face(Vector2I.Up);
        _hud.Refresh();
    }

    private void WarpToAnimationTest()
    {
        _dialogue.Close();
        _activeGroup = 0;
        ClearDeactivatedWarp();
        int targetRoom = _currentRoom.Id == 0xb8 ? 0x03 : 0xb8;
        _currentRoom = _world.LoadRoom(_activeGroup, targetRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
        _hud.Refresh();
    }

    private void WarpToBushTest()
    {
        _dialogue.Close();
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x69);
        Vector2 bushPoint = new(1 * OracleRoomData.MetatileSize + 8, 3 * OracleRoomData.MetatileSize + 8);
        _currentRoom.ReplaceMetatile(bushPoint, 0x3a, 0xc5, (long)_animationTicks);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        _player.WarpTo(new Vector2(bushPoint.X, 70));
        _player.Face(Vector2I.Up);
        _hud.Refresh();
    }

    private void WarpToHouseTest()
    {
        _dialogue.Close();
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x47);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 54));
        _player.Face(Vector2I.Up);
        _hud.Refresh();
    }

    private void WarpToNpcTest()
    {
        _dialogue.Close();
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x48);
        _roomView.SetRoom(_currentRoom.Texture);
        RefreshRoomObjects();
        _player.WarpTo(new Vector2(0x38, 0x58));
        _player.Face(Vector2I.Up);
        _hud.Refresh();
    }

    private void ValidateHouseWarp()
    {
        WarpToHouseTest();
        for (float y = 54; y >= 47; y--)
        {
            if (Collides(new Vector2(88, y)))
                throw new InvalidOperationException($"The path into exterior door $25 is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(88, 47));
        if (!CheckTileWarp(_player) || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected exterior 0:47/$25 to enter house 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height))
            throw new InvalidOperationException("House entry did not begin at the bottom edge of the interior.");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        if (!IsTransitioning || !Mathf.IsEqualApprox(_player.Position.Y, _currentRoom.Height - WarpEnterFrames))
            throw new InvalidOperationException("Link did not perform the 28-frame interior entry walk.");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("The 32-frame room fade did not finish after entering the house.");

        for (float y = _player.Position.Y; y <= _currentRoom.Height + 2; y++)
        {
            if (Collides(new Vector2(_currentRoom.Width / 2.0f, y)))
                throw new InvalidOperationException($"The house's bottom exit is blocked at y={y}.");
        }
        _player.WarpTo(new Vector2(_currentRoom.Width / 2.0f, _currentRoom.Height + 2));
        CheckRoomExit(_player);
        if (!IsTransitioning || _activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException("The house exit did not begin with its scripted walk offscreen.");
        UpdateRoomWarpTransition(WarpLeaveFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 || !IsTransitioning)
            throw new InvalidOperationException("The exterior was not loaded after the 16-frame exit walk.");
        UpdateRoomWarpTransition(WarpFadeFrames / 60.0);
        if (_activeGroup != 0 || _currentRoom.Id != 0x47 ||
            _currentRoom.GetPackedPosition(_player.Position) != 0x35)
            throw new InvalidOperationException(
                $"Expected house 2:ea bottom exit to step out below 0:47/$25, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}/${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        if (Collides(_player.Position + Vector2.Down))
            throw new InvalidOperationException("The exterior landing spot below 0:47/$25 is blocked.");

        _activeGroup = 2;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0xeb);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(new Vector2(-2, _currentRoom.Height / 2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 2 || _currentRoom.Id != 0xea)
            throw new InvalidOperationException(
                $"Expected room 2:eb left edge to scroll to 2:ea, got {_activeGroup}:{_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x49)
            throw new InvalidOperationException(
                $"Expected Link to finish 2:eb -> 2:ea near the right edge, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");

        GD.Print("Validated original house entry/exit fades, scripted walks, and 2:eb -> 2:ea screen transition.");
    }

    private void ValidateCaveWarps()
    {
        ValidateLargeRoomCaveWarp(0x21, 0x04);
        ValidateLargeDungeonTopTransition();
        ValidateLargeRoomCaveWarp(0x28, 0xce);
        GD.Print("Validated 0:48 cave entries and dungeon00 room 4:04 -> 4:03 top transition.");
    }

    private void ValidateLargeDungeonTopTransition()
    {
        float exitX = -1.0f;
        for (float x = 8.0f; x < _currentRoom.Width; x++)
        {
            if (!Collides(new Vector2(x, -2.0f)))
            {
                exitX = x;
                break;
            }
        }
        if (exitX < 0.0f)
            throw new InvalidOperationException("Could not find 4:04's open northern dungeon exit.");

        _player.WarpTo(new Vector2(exitX, -2.0f));
        CheckRoomExit(_player);
        if (_activeGroup != 4 || _currentRoom.Id != 0x03 || !_scrollTransitionActive)
            throw new InvalidOperationException(
                $"Expected dungeon00 room 4:04 north to lead to 4:03, got {_activeGroup}:{_currentRoom.Id:x2}.");
        if (_scrollTransitionFrames != 32 || !Mathf.IsEqualApprox(_scrollTransitionDistance, 128.0f))
            throw new InvalidOperationException("Large-room vertical scrolling did not use the 128px playfield distance.");

        FinishActiveScrollingTransitionForValidation();
        if (Mathf.Abs(WorldToScreen(_player.Position).Y - 118.0f) > 0.01f)
            throw new InvalidOperationException("Link did not finish 4:04 -> 4:03 at the lower playfield edge.");
    }

    private void ValidateLargeRoomCaveWarp(int sourcePosition, int destinationRoom)
    {
        WarpToNpcTest();
        int tileX = sourcePosition & 0x0f;
        int tileY = (sourcePosition >> 4) & 0x0f;
        _player.WarpTo(new Vector2(
            tileX * OracleRoomData.MetatileSize + 8,
            tileY * OracleRoomData.MetatileSize + 8));
        if (!CheckTileWarp(_player) || _activeGroup != 4 || _currentRoom.Id != destinationRoom)
            throw new InvalidOperationException(
                $"Expected 0:48/${sourcePosition:x2} to enter 4:{destinationRoom:x2}, got " +
                $"{_activeGroup}:{_currentRoom.Id:x2}.");
        if (_currentRoom.Width != 256 || _currentRoom.Height != 176)
            throw new InvalidOperationException(
                $"Expected 4:{destinationRoom:x2} to use the original 256x176 large-room dimensions.");
        if (_player.Position != new Vector2(0x78, 0xb0))
            throw new InvalidOperationException(
                $"Expected the original large-room entry coordinate $b0/$78, got {_player.Position}.");

        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 128)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not begin the 4:{destinationRoom:x2} cave entry at screen position (80,128).");
        UpdateRoomWarpTransition(WarpEnterFrames / 60.0);
        UpdateRoomCamera();
        if (WorldToScreen(_player.Position).DistanceSquaredTo(new Vector2(80, 100)) > 0.01f)
            throw new InvalidOperationException(
                $"Link did not finish the 28-frame 4:{destinationRoom:x2} cave entry at screen position (80,100).");
        UpdateRoomWarpTransition((WarpFadeFrames - WarpEnterFrames) / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException($"The 4:{destinationRoom:x2} cave fade did not finish.");
    }

    private void ValidateSwordBush()
    {
        WarpToBushTest();
        Vector2 bushPoint = new(24, 56);
        if (_currentRoom.GetMetatile(bushPoint) != 0xc5)
            throw new InvalidOperationException("Expected overworld bush $c5 in room 69 at $31.");
        _player.StartSwordAttack();
        _player._Process(7.0 / 60.0);
        if (_currentRoom.GetMetatile(bushPoint) != 0x3a)
            throw new InvalidOperationException("The level-1 sword did not replace bush $c5 with ground $3a.");
        if (_currentRoom.IsSolid(bushPoint))
            throw new InvalidOperationException("The cut bush's replacement tile remained solid.");
        GD.Print("Validated level-1 sword hit and bush substitution c5 -> 3a in room 69.");
    }

    private void ValidateTerrain()
    {
        _dialogue.Close();
        _player.RefillHealth();
        _activeGroup = 0;
        ClearDeactivatedWarp();

        _currentRoom = _world.LoadRoom(_activeGroup, 0xb8);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 waterSafe = new(40, 8);
        _player.WarpTo(waterSafe);
        _player.WarpTo(new Vector2(8, 8), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Water)
            throw new InvalidOperationException("Expected room b8/$00 to be water terrain.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException("Water terrain did not trigger Link's respawn state.");
        _player._PhysicsProcess(0.5);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(waterSafe) > 1.0f)
            throw new InvalidOperationException("Water terrain did not return Link to the last safe tile.");

        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 lavaSafe = new(56, 8);
        _player.WarpTo(lavaSafe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);
        if (GetTerrainInfo(_player.Position).Hazard != OracleRoomData.HazardType.Lava)
            throw new InvalidOperationException("Expected room 03/$10 to be lava terrain.");
        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Visible)
            throw new InvalidOperationException("Lava terrain did not trigger Link's respawn state.");
        _player._PhysicsProcess(0.5);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(lavaSafe) > 1.0f)
            throw new InvalidOperationException("Lava terrain did not return Link to the last safe tile.");

        if (!TryFindTerrainSample(
            OracleRoomData.HazardType.Hole,
            out int holeGroup,
            out int holeRoom,
            out Vector2 holeCenter,
            out Vector2 holeSafe))
        {
            throw new InvalidOperationException("Could not find a testable hole terrain tile.");
        }

        _player.RefillHealth();
        _activeGroup = holeGroup;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, holeRoom);
        _roomView.SetRoom(_currentRoom.Texture);
        _player.WarpTo(holeSafe);
        Vector2 offCenterHoleEntry = holeCenter + new Vector2(3, -2);
        int beforeHoleHealth = _player.HealthQuarters;
        _player.WarpTo(offCenterHoleEntry, recordSafe: false);
        Vector2 expectedHoleCenter = GetActiveTerrain(_player.Position).TileCenter;
        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                $"Room {holeGroup:x1}:{holeRoom:x2} hole terrain did not start Link's pull-in state.");
        if (_player.HealthQuarters != beforeHoleHealth)
            throw new InvalidOperationException("Hole damage was applied before the pull/fall animation finished.");

        AdvanceHolePullUntilFall(expectedHoleCenter);
        AdvanceHoleFallUntilRespawn(holeSafe);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(holeSafe) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the last safe tile.");
        if (_player.HealthQuarters != beforeHoleHealth - 2)
            throw new InvalidOperationException("Hole terrain did not apply half-heart damage after respawn.");

        ValidateRoom01HoleBoundaryCase();

        _activeGroup = 0;
        _currentRoom = _world.LoadRoom(_activeGroup, 0x11);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 ledgeStart = new(24, 56);
        _player.WarpTo(ledgeStart);
        _player.Face(Vector2I.Down);
        if (!TryStartLedgeHop(_player, _player.Position, Vector2.Down))
            throw new InvalidOperationException("Room 11's south cliff did not start a ledge hop.");
        for (int i = 0; i < 30; i++)
            _player._PhysicsProcess(1.0 / 60.0);
        if (_player.Position.Y <= ledgeStart.Y + OracleRoomData.MetatileSize)
            throw new InvalidOperationException("The ledge hop did not carry Link down across the cliff.");

        GD.Print("Validated terrain hazards, hole fall animation/respawn, and south-facing ledge hop.");
    }

    private void ValidateRoom01HoleBoundaryCase()
    {
        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x01);
        _roomView.SetRoom(_currentRoom.Texture);

        if (!TryFindHoleWithSafeNeighbor(_currentRoom, out Vector2 holeCenter, out Vector2 safePosition))
            throw new InvalidOperationException("Room 0:01 did not have a testable hole with a safe neighbor.");

        _player.RefillHealth();
        _player.WarpTo(safePosition);
        int beforeHealth = _player.HealthQuarters;

        float tileTop = Mathf.FloorToInt(holeCenter.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize;
        Vector2 boundaryEntry = new(holeCenter.X, tileTop - 5.0f + 0.6f);
        _player.WarpTo(boundaryEntry, recordSafe: false);
        Vector2 expectedCenter = GetActiveTerrain(_player.Position).TileCenter;
        if (expectedCenter.DistanceSquaredTo(holeCenter) > 1.0f)
            throw new InvalidOperationException("Room 0:01 boundary setup did not sample the hole tile.");

        _player._PhysicsProcess(1.0 / 60.0);
        if (!_player.IsPullingIntoHole && !_player.IsFallingInHole)
            throw new InvalidOperationException(
                "Room 0:01 boundary hole entry did not start the pull-in state.");

        AdvanceHolePullUntilFall(holeCenter);
        AdvanceHoleFallUntilRespawn(safePosition);
        if (_player.IsFallingInHole)
            throw new InvalidOperationException("Room 0:01 boundary hole fall did not complete.");
        if (_player.Position.DistanceSquaredTo(safePosition) > 1.0f)
            throw new InvalidOperationException("Room 0:01 hole respawn did not return to the room entry anchor.");
        if (_player.HealthQuarters != beforeHealth - 2)
            throw new InvalidOperationException("Room 0:01 hole fall did not apply half-heart damage.");
    }

    private void AdvanceHolePullUntilFall(Vector2 expectedCenter)
    {
        for (int i = 0; i < 120 && !_player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (!_player.IsFallingInHole)
            throw new InvalidOperationException("Hole pull-in did not transition to the fall animation.");
        if (_player.Position.DistanceSquaredTo(expectedCenter) > 1.0f)
            throw new InvalidOperationException("Hole pull-in did not center Link on the sampled hole tile.");
    }

    private void AdvanceHoleFallUntilRespawn(Vector2 expectedRespawn)
    {
        for (int i = 0; i < 80 && _player.IsFallingInHole; i++)
            _player._PhysicsProcess(1.0 / 60.0);

        if (_player.IsFallingInHole)
            throw new InvalidOperationException("The falling-in-hole animation did not finish.");
        if (_player.Position.DistanceSquaredTo(expectedRespawn) > 1.0f)
            throw new InvalidOperationException("Hole terrain did not return Link to the stored respawn anchor.");
    }

    private static bool TryFindHoleWithSafeNeighbor(
        OracleRoomData room,
        out Vector2 holeCenter,
        out Vector2 safePosition)
    {
        for (int tileY = 1; tileY < room.HeightInTiles; tileY++)
        for (int tileX = 0; tileX < room.WidthInTiles; tileX++)
        {
            Vector2 center = new(
                tileX * OracleRoomData.MetatileSize + 8,
                tileY * OracleRoomData.MetatileSize + 8);
            OracleRoomData.TerrainInfo terrain = room.GetTerrainInfo(center);
            if (terrain.Hazard != OracleRoomData.HazardType.Hole)
                continue;

            foreach (Vector2I direction in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                Vector2 candidate = center + (Vector2)direction * OracleRoomData.MetatileSize;
                if (candidate.X < 0 || candidate.X >= room.Width ||
                    candidate.Y < 0 || candidate.Y >= room.Height)
                {
                    continue;
                }

                OracleRoomData.TerrainInfo safeTerrain = room.GetTerrainInfo(candidate);
                if (safeTerrain.Hazard == OracleRoomData.HazardType.None &&
                    !RoomCollides(room, candidate))
                {
                    holeCenter = center;
                    safePosition = candidate;
                    return true;
                }
            }
        }

        holeCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private bool TryFindTerrainSample(
        OracleRoomData.HazardType hazard,
        out int group,
        out int room,
        out Vector2 hazardCenter,
        out Vector2 safePosition)
    {
        for (int candidateGroup = 0; candidateGroup <= 5; candidateGroup++)
        for (int candidateRoom = 0; candidateRoom <= 0xff; candidateRoom++)
        {
            if (!_world.HasRoom(candidateGroup, candidateRoom))
                continue;

            OracleRoomData data = _world.LoadRoom(candidateGroup, candidateRoom);
            Vector2? safe = null;
            Vector2? target = null;

            for (int tileY = 0; tileY < data.HeightInTiles; tileY++)
            for (int tileX = 0; tileX < data.WidthInTiles; tileX++)
            {
                Vector2 center = new(
                    tileX * OracleRoomData.MetatileSize + 8,
                    tileY * OracleRoomData.MetatileSize + 8);
                OracleRoomData.TerrainInfo terrain = data.GetTerrainInfo(center);

                if (terrain.Hazard == OracleRoomData.HazardType.None &&
                    safe == null &&
                    !RoomCollides(data, center))
                {
                    safe = center;
                }
                if (terrain.Hazard == hazard &&
                    terrain.Type == OracleRoomData.TerrainType.Hole &&
                    !RoomCollides(data, center))
                {
                    target = center;
                }
            }

            if (safe != null && target != null)
            {
                group = candidateGroup;
                room = candidateRoom;
                hazardCenter = target.Value;
                safePosition = safe.Value;
                return true;
            }
        }

        group = -1;
        room = -1;
        hazardCenter = Vector2.Zero;
        safePosition = Vector2.Zero;
        return false;
    }

    private static bool RoomCollides(OracleRoomData room, Vector2 playerPosition)
    {
        foreach (Vector2 offset in new[]
        {
            new Vector2(-5, -2), new Vector2(5, -2),
            new Vector2(-5, 5), new Vector2(5, 5)
        })
        {
            Vector2 sample = playerPosition + offset;
            if (sample.X < 0 || sample.X >= room.Width ||
                sample.Y < 0 || sample.Y >= room.Height ||
                room.IsSolid(sample))
            {
                return true;
            }
        }
        return false;
    }

    private void ValidateHealth()
    {
        _dialogue.Close();
        _player.RefillHealth();
        SyncHudToPlayer();

        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12 ||
            _hud.MaxHealthQuarters != _player.MaxHealthQuarters)
            throw new InvalidOperationException("Expected Link and the HUD to start with three full hearts.");

        _player.ApplyDamage(1);
        if (_player.HealthQuarters != 11 || _hud.HealthQuarters != 11)
            throw new InvalidOperationException("Direct quarter-heart damage did not synchronize to the HUD.");

        _player.Heal(1);
        if (_player.HealthQuarters != 12 || _hud.HealthQuarters != 12)
            throw new InvalidOperationException("Direct quarter-heart healing did not synchronize to the HUD.");

        _activeGroup = 0;
        ClearDeactivatedWarp();
        _currentRoom = _world.LoadRoom(_activeGroup, 0x03);
        _roomView.SetRoom(_currentRoom.Texture);
        Vector2 safe = new(56, 8);
        _player.WarpTo(safe);
        _player.WarpTo(new Vector2(8, 24), recordSafe: false);

        _player._PhysicsProcess(1.0 / 60.0);
        if (_player.HealthQuarters != 10 || _hud.HealthQuarters != 10)
            throw new InvalidOperationException(
                "Lava hazard did not apply half-heart damage and update the HUD.");
        if (_player.Visible)
            throw new InvalidOperationException("Lava hazard did not trigger Link's respawn state.");

        _player._PhysicsProcess(0.5);
        if (!_player.Visible || _player.Position.DistanceSquaredTo(safe) > 1.0f)
            throw new InvalidOperationException("Lava hazard did not return Link to the last safe tile.");

        GD.Print("Validated quarter-heart health, HUD synchronization, and half-heart terrain damage.");
    }

    private void ValidateAnimations()
    {
        OracleRoomData water = _world.LoadRoom(0, 0xb8);
        ulong waterStart = water.GetAnimationChecksum(0);
        bool waterChanged = false;
        for (int tick = 1; tick <= 120 && !waterChanged; tick++)
            waterChanged = water.GetAnimationChecksum(tick) != waterStart;

        OracleRoomData lava = _world.LoadRoom(0, 0x03);
        ulong lavaStart = lava.GetAnimationChecksum(0);
        bool lavaChanged = false;
        for (int tick = 1; tick <= 60 && !lavaChanged; tick++)
            lavaChanged = lava.GetAnimationChecksum(tick) != lavaStart;

        if (!waterChanged || !lavaChanged)
            throw new InvalidOperationException(
                $"Expected animated water and lava frames; water={waterChanged}, lava={lavaChanged}.");
        GD.Print("Validated disassembly-driven water animation in room b8 and lava animation in room 03.");
    }

    private void ValidateSigns()
    {
        WarpToSignTest();
        if (_currentRoom.GetMetatile(new Vector2(88, 58)) != 0xf2)
            throw new InvalidOperationException("Expected sign metatile $f2 in room 2a at $35.");
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 2a test sign did not open its dialogue.");
        GD.Print("Validated sign $35 in room 2a and opened TX_2e01.");
    }

    private void ValidateNpcs()
    {
        WarpToNpcTest();
        NpcCharacter? villager = _npcNodes.Find(npc => npc.Record.Id == 0x3a && npc.Record.SubId == 0x03);
        if (villager is null)
            throw new InvalidOperationException($"Expected the room 0:48 villager among {_npcNodes.Count} extracted NPCs.");
        if (villager.TextId != 0x1420)
            throw new InvalidOperationException($"Expected room 0:48 villager to resolve TX_1420, got TX_{villager.TextId:x4}.");
        if (villager.Position != new Vector2(0x38, 0x48) ||
            villager.SpriteBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager sprite is not centered on its object tile.");
        }
        if (villager.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            villager.ObjectCollisionBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager object hitbox does not match the original $06/$06 collision radii.");
        }
        if (villager.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            villager.LinkBlockingBounds.GetCenter() != villager.Position)
        {
            throw new InvalidOperationException("The room 0:48 villager does not combine NPC and Link $06 radii into a 24px blocking region.");
        }
        if (!villager.BlocksLinkCenter(villager.Position) ||
            villager.BlocksLinkCenter(villager.Position + new Vector2(0.0f, 12.0f)))
        {
            throw new InvalidOperationException("The room 0:48 villager's strict radius collision boundary is not centered correctly.");
        }
        if (!Collides(villager.Position + new Vector2(0.0f, 11.9f)) ||
            Collides(villager.Position + new Vector2(0.0f, 12.1f)))
        {
            throw new InvalidOperationException("The room 0:48 villager did not stop Link at the original bottom collision radius.");
        }
        if (!TryInteract(_player) || !_dialogue.IsOpen)
            throw new InvalidOperationException("The room 0:48 villager did not open dialogue.");
        int frameBase = villager.Record.TileBase / 4;
        if (villager.CurrentFrameColumn != frameBase)
            throw new InvalidOperationException("The room 0:48 villager did not face down toward Link after talking.");
        villager.UpdateNpc(16.0 / 60.0, _player.Position);
        if (villager.CurrentAnimationFrame != 1)
            throw new InvalidOperationException("The room 0:48 villager did not advance its original 16-frame idle animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Left * 20.0f);
        if (villager.FacingVector != Vector2I.Left || villager.CurrentAnimationFrame != 0)
            throw new InvalidOperationException("The room 0:48 villager did not face nearby Link and reset its animation.");
        villager.UpdateNpc(1.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Left)
            throw new InvalidOperationException("The NPC facing cooldown did not preserve the current direction.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 20.0f);
        if (villager.FacingVector != Vector2I.Right || villager.CurrentFrameColumn != frameBase + 2)
            throw new InvalidOperationException("The villager did not use the mirrored side OAM after the 30-frame facing delay.");
        villager.UpdateNpc(30.0 / 60.0, villager.Position + Vector2.Right * 80.0f);
        if (villager.FacingVector != Vector2I.Down)
            throw new InvalidOperationException("The villager did not return to facing down when Link left the $28 awareness radius.");
        GD.Print("Validated villager idle animation, $28 Link awareness, 30-frame facing delay, and TX_1420 dialogue.");
    }

    private static int GetStartingRoom()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--room=", StringComparison.OrdinalIgnoreCase))
                continue;
            string value = argument[7..].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int room)
                && room is >= 0 and <= 0xff)
                return room;
        }
        return 0x11;
    }

    private static int GetStartingGroup()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--group=", StringComparison.OrdinalIgnoreCase))
                continue;
            string value = argument[8..].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int group)
                && group is >= 0 and <= 5)
                return group;
        }
        return 0;
    }

    private void ValidateStartupTransition()
    {
        if (_currentRoom.Id != 0x11)
            throw new InvalidOperationException("The transition validation expects startup room 11.");

        // Room 11's top staircase is metatile $d0 at column 4. This position
        // crosses the same collision samples and room-exit code as player input.
        Vector2 exitPosition = new(4 * OracleRoomData.MetatileSize + 8, -2);
        for (float y = _player.Position.Y; y >= exitPosition.Y; y -= 2)
        {
            if (Collides(new Vector2(exitPosition.X, y)))
                throw new InvalidOperationException(
                    $"Room 11's path to the top staircase is blocked at y={y}.");
        }

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x01)
            throw new InvalidOperationException(
                $"Expected room 01 after the startup transition, got {_currentRoom.Id:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        if (_currentRoom.GetPackedPosition(_player.Position) != 0x74)
            throw new InvalidOperationException(
                $"Expected Link to finish the 11 -> 01 transition near $74, got " +
                $"${_currentRoom.GetPackedPosition(_player.Position):x2}.");
        GD.Print("Validated original-style transition 11 -> 01 through staircase collision $18.");
    }

    private void ValidateSymmetryTransition()
    {
        if (_currentRoom.Id != 0x22)
            throw new InvalidOperationException("The Symmetry transition validation expects room 22.");

        int oldTileset = _currentRoom.TilesetId;
        Vector2 exitPosition = new(3 * OracleRoomData.MetatileSize + 8, -2);
        if (Collides(exitPosition))
            throw new InvalidOperationException("Room 22's north staircase is blocked.");

        _player.WarpTo(exitPosition);
        CheckRoomExit(_player);
        if (_currentRoom.Id != 0x12 || _currentRoom.TilesetId == oldTileset)
            throw new InvalidOperationException(
                $"Expected room 12 / a new tileset, got {_currentRoom.Id:x2} / {_currentRoom.TilesetId:x2}.");
        ValidateLinkScrollsForOneTransitionFrame();
        FinishActiveScrollingTransitionForValidation();
        GD.Print($"Validated cross-tileset transition 22 ({oldTileset:x2}) -> " +
            $"12 ({_currentRoom.TilesetId:x2}).");
    }

    private void ValidateLinkScrollsForOneTransitionFrame()
    {
        if (!IsTransitioning)
            return;

        Vector2 position = _player.Position;
        UpdateScrollingTransition(1.0 / 60.0);
        Vector2 moved = _player.Position - position;
        Vector2 scrollDirection = -(Vector2)_scrollTransitionDirection;
        if (moved.Dot(scrollDirection) <= 0.0f)
            throw new InvalidOperationException("Link did not scroll with the screen transition.");
    }

    private void FinishActiveScrollingTransitionForValidation()
    {
        for (int i = 0; i < 80 && IsTransitioning; i++)
            UpdateScrollingTransition(1.0 / 60.0);
        if (IsTransitioning)
            throw new InvalidOperationException("Scrolling transition did not finish within 80 frames.");
    }
}

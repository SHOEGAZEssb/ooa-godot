using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class RoomTransitionController
{
    public event Action<Vector2I>? ScrollingTransitionFinished;

    private enum WarpPhase
    {
        None,
        FadeOut,
        LeaveScreen,
        FadeIn,
        TimeWarpInitialize,
        TimeWarpDissolve,
        TimeWarpSetup,
        TimeWarpSourceEffect,
        TimeWarpSourceTrail,
        TimeWarpBlackFadeIn,
        TimeWarpWhiteFadeOut,
        TimeWarpArrivalFadeIn,
        TimeWarpArrivalWait,
        TimeWarpArrivalEffect,
        TimeWarpArrivalFlicker
    }

    public const float WarpFadeFrames = 32.0f;
    // applyWarpTransition2 bit 7 calls fadeoutToWhiteWithDelay(4). The palette
    // offset advances on updates 1,5,...121 and stops when it reaches $20 on 125.
    public const float DelayedWarpFadeFrames = 125.0f;
    public const float WarpLeaveFrames = 16.0f;
    public const float WarpEnterFrames = 28.0f;
    public const float FastPaletteFadeFrames = 11.0f;
    public const int TimeWarpInitializeFrames = 1;
    public const int TimeWarpDissolveFrames = 48;
    public const int TimeWarpDissolveBufferSteps = 6;
    public const int TimeWarpDissolveCommitBufferStep = 5;
    public const int TimeWarpSetupFrames = 2;
    public const int TimeWarpSourceEffectFrames = 120;
    public const int TimeWarpSourceTrailFrames = 60;
    public const int TimeWarpArrivalWaitFrames = 30;
    public const int TimeWarpArrivalEffectFrames = 16;
    public const int TimeWarpArrivalFlickerFrames = 30;

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
    private readonly DeathRespawnPointController _deathRespawnPoints;
    private readonly OracleSoundEngine _sound;
    private readonly TimeWarpEffectDatabase _timeWarpEffects = new();

    private bool _scrollActive;
    private Vector2I _scrollDirection;
    private Vector2 _scrollLinkStart;
    private Vector2 _scrollLinkStep;
    private Vector2 _scrollFinishOffset;
    private Vector2 _scrollIncomingStartOffset;
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
    private float _warpFadeOutFrames = WarpFadeFrames;
    private Vector2 _warpWalkStart;
    private Vector2 _warpWalkEnd;
    private bool _destinationWalk;
    private bool _timeWarp;
    private int _timeWarpPhaseFrame;
    private int _timeWarpGlobalFrame;
    private int _timeWarpDissolveStep = -1;
    private int _timeWarpDissolveBufferStep = -1;
    private int _timeWarpAppliedDissolveStep = -1;
    private double _timeWarpTickAccumulator;
    private bool _timeWarpUsesIndoorBeamPalette;
    private TimeWarpEffect? _timeWarpEffect;
    private readonly Dictionary<CanvasItem, Material?> _dissolvedItems = new();
    private ShaderMaterial? _dissolveMaterial;

    private static readonly (int Even, int Odd)[] TimeWarpDissolveMasks =
    {
        (0xdd, 0xff), (0xdd, 0xbb), (0x55, 0xbb), (0x55, 0xaa),
        (0x11, 0xaa), (0x11, 0x88), (0x00, 0x88), (0x00, 0x00)
    };

    public bool IsTransitioning => _warpActive || _scrollActive || _roomView.IsTransitioning;
    public bool ScrollActive => _scrollActive;
    public Vector2I ScrollDirection => _scrollDirection;
    internal Vector2 ScrollLinkPositionInDestination =>
        _scrollLinkStart + _scrollLinkStep * _scrollFrame + _scrollFinishOffset;
    public float ScrollDistance => _scrollDistance;
    public int ScrollFrames => _scrollFrames;
    internal bool TimeWarpActive => _timeWarp && _warpActive;
    internal int TimeWarpPhaseFrame => _timeWarpPhaseFrame;
    internal int TimeWarpDissolveStep => _timeWarpDissolveStep;
    internal int TimeWarpDissolveBufferStep => _timeWarpDissolveBufferStep;
    internal int TimeWarpAppliedDissolveStep => _timeWarpAppliedDissolveStep;
    internal string TimeWarpPhaseName => _warpPhase.ToString();
    internal TimeWarpEffect? ActiveTimeWarpEffect => _timeWarpEffect;
    internal static (int Even, int Odd) TimeWarpDissolveMaskForValidation(int step) =>
        TimeWarpDissolveMasks[step];

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
        Func<Vector2, bool> collides,
        DeathRespawnPointController deathRespawnPoints,
        OracleSoundEngine sound)
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
        _deathRespawnPoints = deathRespawnPoints;
        _sound = sound;

        if (_timeWarpEffects.DissolveFrames != TimeWarpDissolveFrames ||
            _timeWarpEffects.SourceEffectFrames != TimeWarpSourceEffectFrames ||
            _timeWarpEffects.SourceTrailFrames != TimeWarpSourceTrailFrames ||
            _timeWarpEffects.ArrivalWaitFrames != TimeWarpArrivalWaitFrames ||
            _timeWarpEffects.ArrivalEffectFrames != TimeWarpArrivalEffectFrames ||
            _timeWarpEffects.ArrivalFlickerFrames != TimeWarpArrivalFlickerFrames)
        {
            throw new InvalidOperationException(
                "Imported CUTSCENE_TIMEWARP timing disagrees with the runtime state machine.");
        }
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
        OracleRoomData target = _rooms.GetRoom(_rooms.ActiveGroup, targetId);
        UpdateCamera();
        Vector2 sourceCameraOrigin = CurrentCameraOrigin;
        Vector2 start = player.Position;
        if (direction == Vector2I.Up) start.Y = 6;
        if (direction == Vector2I.Down) start.Y = source.Height - 7;
        if (direction == Vector2I.Left) start.X = 6;
        if (direction == Vector2I.Right) start.X = source.Width - 6;

        _scrollActive = true;
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
        _scrollIncomingStartOffset = sourceCameraOrigin - destinationCameraOrigin +
            (Vector2)direction * _scrollDistance;
        _rooms.SetLoadedRoom(_rooms.ActiveGroup, target);
        _roomView.StartScreenTransition(
            target.Texture, direction, _scrollDistance, sourceCameraOrigin, destinationCameraOrigin);
        _entities.BeginScreenTransition(
            _rooms.ActiveGroup, target, _scrollIncomingStartOffset, direction,
            target.GetPackedPosition(transitionEnd));
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
        _entities.SetScreenTransitionOffsets(-screenScroll, _scrollIncomingStartOffset - screenScroll);
        _player.SetScrollingTransitionPosition(linkPosition, screenScroll);
        if (_scrollFrame < _scrollFrames)
            return;

        _scrollActive = false;
        _roomView.FinishTransition();
        _player.FinishScrollingTransition(linkPosition + _scrollFinishOffset);
        ScrollingTransitionFinished?.Invoke(_scrollDirection);
        _entities.FinishScreenTransition();
        UpdateCamera();
    }

    public void ApplyWarp(Player player, WarpDatabase.Warp warp)
    {
        BeginWarp(player, warp, false);
    }

    public void ApplyWarpWithDelayedFadeOut(Player player, WarpDatabase.Warp warp)
    {
        BeginWarp(player, warp, true);
    }

    public void ApplyTimePortalWarp(Player player, Vector2 portalPosition)
    {
        int destinationGroup = _rooms.ActiveGroup ^ 0x01;
        if (_warpActive || _rooms.ActiveGroup is not (0 or 1) ||
            !_rooms.World.HasRoom(destinationGroup, _rooms.CurrentRoom.Id))
            return;

        int position = _rooms.CurrentRoom.GetPackedPosition(portalPosition);
        _pendingWarp = new WarpDatabase.Warp(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, position, 0, 0,
            destinationGroup, _rooms.CurrentRoom.Id, position, 0, 6);
        _timeWarp = true;
        _warpActive = true;
        _warpPhase = WarpPhase.TimeWarpInitialize;
        _warpFrame = 0.0f;
        _timeWarpPhaseFrame = 0;
        _timeWarpTickAccumulator = 0.0;
        _timeWarpGlobalFrame = _entities.FrameCounter;
        _timeWarpDissolveStep = -1;
        _timeWarpDissolveBufferStep = -1;
        _timeWarpAppliedDissolveStep = -1;
        // CUTSCENE_TIMEWARP writes $01/$02 from the source room's
        // wTilesetFlags bit 7 into wcc50. TRANSITION_DEST_TIMEWARP copies that
        // same value into the destination interaction instead of recomputing
        // it after the era swap.
        _timeWarpUsesIndoorBeamPalette =
            (_rooms.CurrentRoom.TilesetFlags & 0x80) != 0;
        _destinationWalk = false;
        _dialogue.Close();
        // interactionBeginTimewarp copies the portal's position into w1Link,
        // forces DIR_DOWN, and calls restartSound before CUTSCENE_TIMEWARP is
        // serviced on the next update.
        player.BeginTimeWarpTransition(portalPosition);
        _sound.RestartSound();
        _roomView.SetBackgroundFade(Colors.Black, 0.0f);
        SetFade(0.0f);
    }

    private void BeginWarp(Player player, WarpDatabase.Warp warp, bool delayedFadeOut)
    {
        if (_warpActive || !_rooms.World.HasRoom(warp.DestinationGroup, warp.DestinationRoom))
            return;
        _dialogue.Close();
        _pendingWarp = warp;
        _timeWarp = false;
        _warpActive = true;
        _warpFrame = 0.0f;
        _warpFadeOutFrames = delayedFadeOut ? DelayedWarpFadeFrames : WarpFadeFrames;
        _destinationWalk = false;
        player.BeginRoomWarpTransition();
        if (delayedFadeOut)
        {
            _warpPhase = WarpPhase.FadeOut;
            SetFade(0.0f);
            return;
        }
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
        if (_timeWarp)
        {
            _timeWarpTickAccumulator += delta * 60.0;
            if (_timeWarpTickAccumulator + 0.000001 >= 1.0)
            {
                _timeWarpTickAccumulator -= 1.0;
                AdvanceTimeWarpFrame();
                // This cutscene rewrites one graphics buffer per vblank. A
                // render/driver hitch (notably the first shader compilation)
                // must slow the sequence instead of consuming several masks
                // before another frame can be drawn.
                _timeWarpTickAccumulator = Math.Min(
                    _timeWarpTickAccumulator, 0.999999);
            }
            return;
        }

        _warpFrame += (float)delta * 60.0f;
        switch (_warpPhase)
        {
            case WarpPhase.FadeOut:
                SetFade(_warpFrame / _warpFadeOutFrames);
                if (_warpFrame >= _warpFadeOutFrames)
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

    private void AdvanceTimeWarpFrame()
    {
        _timeWarpGlobalFrame = (_timeWarpGlobalFrame + 1) & 0xff;
        _timeWarpPhaseFrame++;

        switch (_warpPhase)
        {
            case WarpPhase.TimeWarpInitialize:
                // State 0 starts the fast BG-only black fade, leaves OBJ
                // palettes untouched, prepares all loaded object graphics for
                // masking, and hides the status bar.
                _hud.Visible = false;
                _roomView.SetBackgroundFade(
                    Colors.Black,
                    _timeWarpPhaseFrame / FastPaletteFadeFrames);
                if (_timeWarpPhaseFrame >= TimeWarpInitializeFrames)
                {
                    BeginTimeWarpDissolve();
                    SetTimeWarpPhase(WarpPhase.TimeWarpDissolve);
                }
                break;

            case WarpPhase.TimeWarpDissolve:
                int dissolveFrame = _timeWarpPhaseFrame - 1;
                int step = Math.Min(
                    TimeWarpDissolveMasks.Length - 1,
                    dissolveFrame / TimeWarpDissolveBufferSteps);
                int bufferStep = dissolveFrame % TimeWarpDissolveBufferSteps;
                SetTimeWarpDissolvePosition(step, bufferStep);
                _roomView.SetBackgroundFade(
                    Colors.Black,
                    Math.Min(1.0f,
                        (TimeWarpInitializeFrames + _timeWarpPhaseFrame) /
                        FastPaletteFadeFrames));
                if (_timeWarpPhaseFrame >= TimeWarpDissolveFrames)
                {
                    _entities.Clear();
                    SetTimeWarpPhase(WarpPhase.TimeWarpSetup);
                }
                break;

            case WarpPhase.TimeWarpSetup:
                // State 2 substeps $00/$01 reload the tilemap, then create
                // INTERAC_TIMEWARP $dd:$00 with counter1=120.
                if (_timeWarpPhaseFrame >= TimeWarpSetupFrames)
                {
                    SpawnTimeWarpEffect(source: true);
                    _sound.PlaySound(OracleSoundEngine.SndTimewarpInitiated);
                    SetTimeWarpPhase(WarpPhase.TimeWarpSourceEffect);
                }
                break;

            case WarpPhase.TimeWarpSourceEffect:
                if (_timeWarpPhaseFrame >= TimeWarpSourceEffectFrames)
                {
                    // CUTSCENE_TIMEWARP creates $dd:$02 and then calls
                    // objectDelete_de on $d000 (w1Link). Link remains intact
                    // through the object-gfx masking and source expansion,
                    // then disappears at this exact 120-count handoff.
                    _player.Visible = false;
                    _timeWarpEffect?.BeginSourceTrail();
                    _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                    SetTimeWarpPhase(WarpPhase.TimeWarpSourceTrail);
                }
                else
                {
                    _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                }
                break;

            case WarpPhase.TimeWarpSourceTrail:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                if (_timeWarpPhaseFrame >= TimeWarpSourceTrailFrames)
                    SetTimeWarpPhase(WarpPhase.TimeWarpBlackFadeIn);
                break;

            case WarpPhase.TimeWarpBlackFadeIn:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                // Keep the source tilemap fully masked. The original palette
                // handoff transitions the displayed field from black to white;
                // exposing the unfaded Godot room between those palette
                // operations produces a map flash that the hardware path does
                // not present.
                _roomView.SetBackgroundFade(Colors.Black, 1.0f);
                if (_timeWarpPhaseFrame >= FastPaletteFadeFrames)
                {
                    SetTimeWarpPhase(WarpPhase.TimeWarpWhiteFadeOut);
                    // The cutscene observes palette mode 0 on this update and
                    // immediately calls fadeoutToWhite. Apply its first step
                    // now so there is no fully neutral source-room frame
                    // between the black and white fades.
                    _timeWarpPhaseFrame = 1;
                    SetFade(_timeWarpPhaseFrame / WarpFadeFrames);
                }
                break;

            case WarpPhase.TimeWarpWhiteFadeOut:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                _roomView.SetBackgroundFade(Colors.Black, 1.0f);
                SetFade(_timeWarpPhaseFrame / WarpFadeFrames);
                if (_timeWarpPhaseFrame >= WarpFadeFrames)
                {
                    SetFade(1.0f);
                    LoadWarpDestination();
                }
                break;

            case WarpPhase.TimeWarpArrivalFadeIn:
                SetFade(1.0f - _timeWarpPhaseFrame / WarpFadeFrames);
                if (_timeWarpPhaseFrame >= WarpFadeFrames)
                {
                    SetFade(0.0f);
                    SetTimeWarpPhase(WarpPhase.TimeWarpArrivalWait);
                }
                break;

            case WarpPhase.TimeWarpArrivalWait:
                if (_timeWarpPhaseFrame >= TimeWarpArrivalWaitFrames)
                {
                    SpawnTimeWarpEffect(source: false);
                    SetTimeWarpPhase(WarpPhase.TimeWarpArrivalEffect);
                }
                break;

            case WarpPhase.TimeWarpArrivalEffect:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                if (_timeWarpPhaseFrame >= TimeWarpArrivalEffectFrames)
                {
                    _player.Visible = true;
                    _sound.PlaySound(OracleSoundEngine.SndTimewarpCompleted);
                    SetTimeWarpPhase(WarpPhase.TimeWarpArrivalFlicker);
                }
                break;

            case WarpPhase.TimeWarpArrivalFlicker:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                // objectFlickerVisibility uses b=$03: only global frames
                // divisible by four are invisible.
                _player.Visible = (_timeWarpGlobalFrame & 0x03) != 0;
                if (_timeWarpPhaseFrame >= TimeWarpArrivalFlickerFrames)
                    FinishWarp();
                break;
        }
    }

    private void SetTimeWarpPhase(WarpPhase phase)
    {
        _warpPhase = phase;
        _timeWarpPhaseFrame = 0;
    }

    private void LoadWarpDestination()
    {
        if (_timeWarp)
        {
            _timeWarpEffect?.StopImmediately();
            _timeWarpEffect = null;
            EndTimeWarpDissolve();
            _roomView.ClearBackgroundFade();
        }

        WarpDatabase.Warp warp = _pendingWarp;
        OracleRoomData room = _rooms.Load(warp.DestinationGroup, warp.DestinationRoom);
        _roomView.SetRoom(room.Texture);
        // Room-palette darkening belongs to the source room. Time-warp already
        // clears it above; ordinary/delayed warps must not carry it into the
        // destination either (notably after an Essence get sequence).
        _roomView.ClearBackgroundFade();
        // Standard warp loading clears wEnemiesKilledList only when the
        // destination's wDungeonIndex is $ff. Dungeon-to-dungeon warps retain
        // the same transient last-eight-room suppression as scrolling.
        if (_rooms.CurrentDungeonIndex < 0)
            _entities.ClearRecentEnemyDefeats();
        _entities.LoadRoom(
            _rooms.ActiveGroup, room,
            EnemyPlacementContext.FromWarpDestination(warp.DestinationPosition));

        Vector2 spawn;
        if (warp.DestinationTransition == 3)
        {
            Vector2I direction = (warp.DestinationParameter & 0x04) != 0 ? Vector2I.Down : Vector2I.Up;
            bool entersFromScreen = true;
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
                entersFromScreen = false;
            }
            if (entersFromScreen)
            {
                _warpWalkStart = spawn;
                _warpWalkEnd = spawn + (Vector2)direction * WarpEnterFrames;
                _destinationWalk = true;
                _player.BeginRoomWarpWalk(spawn, direction);
            }
            else
            {
                // warpTransition3:@destInit only runs the 28-update entrance
                // walk for destination positions $ff or $f0-$ff. A normal
                // packed position such as the Maku cutscene's $45 is placed
                // directly and returned to standing.
                _destinationWalk = false;
                _player.WarpTo(spawn);
                _player.Face(direction);
            }
            ClearDeactivatedWarp();
        }
        else
        {
            int tileX = warp.DestinationPosition & 0x0f;
            int tileY = (warp.DestinationPosition >> 4) & 0x0f;
            spawn = new Vector2(tileX * OracleRoomData.MetatileSize + 8, tileY * OracleRoomData.MetatileSize + 8);
            if (warp.DestinationTransition == 0x0e)
                spawn.X -= 8.0f;
            byte destinationTile = room.GetMetatile(spawn);
            if (warp.DestinationTransition == 0x0c)
            {
                _deactivatedWarpGroup = _rooms.ActiveGroup;
                _deactivatedWarpRoom = room.Id;
                _deactivatedWarpPosition = warp.DestinationPosition;
                _player.Face(warp.DestinationParameter switch
                {
                    0 => Vector2I.Up,
                    1 => Vector2I.Right,
                    2 => Vector2I.Down,
                    _ => Vector2I.Left
                });
            }
            else if (ShouldStepOut(warp, destinationTile, tileX, tileY))
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

        if (_timeWarp)
        {
            _destinationWalk = false;
            _player.WarpTo(spawn);
            _player.Face(Vector2I.Down);
            _player.Visible = false;
            _hud.Visible = true;
            SetTimeWarpPhase(WarpPhase.TimeWarpArrivalFadeIn);
        }
        else
        {
            _warpPhase = WarpPhase.FadeIn;
        }
        _warpFrame = 0.0f;
        UpdateCamera();
        _hud.Refresh();
    }

    private void FinishWarp()
    {
        bool finishedTimeWarp = _timeWarp;
        _player.FinishRoomWarpTransition(_destinationWalk ? _warpWalkEnd : _player.Position);
        _deathRespawnPoints.RecordWarpDestination(_pendingWarp.DestinationTransition);
        if (finishedTimeWarp)
        {
            _timeWarpEffect?.ContinueAfterTransition(_timeWarpGlobalFrame);
            _timeWarpEffect = null;
            EndTimeWarpDissolve();
            _roomView.ClearBackgroundFade();
            _hud.Visible = true;
        }
        _destinationWalk = false;
        _timeWarp = false;
        _warpActive = false;
        _warpPhase = WarpPhase.None;
        _player.Visible = true;
        SetFade(0.0f);
    }

    private void SpawnTimeWarpEffect(bool source)
    {
        _timeWarpEffect?.StopImmediately();
        _timeWarpEffect = new TimeWarpEffect(
            _timeWarpEffects,
            _player.Position,
            source,
            _timeWarpUsesIndoorBeamPalette)
        {
            Name = source ? "TimeWarpSourceEffect" : "TimeWarpArrivalEffect"
        };
        _player.GetParent().AddChild(_timeWarpEffect);
    }

    private void BeginTimeWarpDissolve()
    {
        EndTimeWarpDissolve();
        var shader = new Shader
        {
            Code = @"shader_type canvas_item;
uniform int even_mask = 255;
uniform int odd_mask = 255;
void fragment() {
    vec4 pixel = texture(TEXTURE, UV) * COLOR;
    ivec2 source_pixel = ivec2(floor(UV / TEXTURE_PIXEL_SIZE));
    int x = source_pixel.x & 7;
    int mask = (source_pixel.y & 1) == 0 ? even_mask : odd_mask;
    if ((mask & (128 >> x)) == 0) {
        pixel.a = 0.0;
    }
    COLOR = pixel;
}"
        };
        _dissolveMaterial = new ShaderMaterial { Shader = shader };

        Node root = _player.GetParent();
        foreach (Node child in root.GetChildren())
        {
            if (child is not CanvasItem canvas || child == _roomView ||
                child == _camera || child == _player)
                continue;
            _dissolvedItems[canvas] = canvas.Material;
            canvas.Material = _dissolveMaterial;
        }
        // CUTSCENE_TIMEWARP masks four object-gfx chunks and two bank-6
        // object/companion chunks on six consecutive updates. Link's graphics
        // are loaded separately straight into VRAM and are not among them.
        // Our objects share one material, so commit its next aggregate mask
        // after the final source-buffer pass.
        _timeWarpDissolveStep = 0;
        _timeWarpDissolveBufferStep = -1;
        SetTimeWarpDissolveMaterialMask(-1);
    }

    private void SetTimeWarpDissolvePosition(int step, int bufferStep)
    {
        _timeWarpDissolveStep = Math.Clamp(step, 0, TimeWarpDissolveMasks.Length - 1);
        _timeWarpDissolveBufferStep = Math.Clamp(
            bufferStep, 0, TimeWarpDissolveBufferSteps - 1);
        if (_timeWarpDissolveBufferStep == TimeWarpDissolveCommitBufferStep)
            SetTimeWarpDissolveMaterialMask(_timeWarpDissolveStep);
    }

    private void SetTimeWarpDissolveMaterialMask(int step)
    {
        if (_dissolveMaterial is null)
            return;
        _timeWarpAppliedDissolveStep = step;
        (int even, int odd) = step < 0
            ? (0xff, 0xff)
            : TimeWarpDissolveMasks[step];
        _dissolveMaterial.SetShaderParameter("even_mask", even);
        _dissolveMaterial.SetShaderParameter("odd_mask", odd);
    }

    private void EndTimeWarpDissolve()
    {
        foreach ((CanvasItem item, Material? material) in _dissolvedItems)
        {
            if (GodotObject.IsInstanceValid(item))
                item.Material = material;
        }
        _dissolvedItems.Clear();
        _dissolveMaterial = null;
        _timeWarpDissolveStep = -1;
        _timeWarpDissolveBufferStep = -1;
        _timeWarpAppliedDissolveStep = -1;
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
        _camera.Position = origin + GameplayCameraOffset;
    }

    /// <summary>Returns physical 160x144 display coordinates below the HUD.</summary>
    public Vector2 WorldToScreen(Vector2 worldPosition) =>
        WorldToGameplayScreen(worldPosition) +
        new Vector2(0, OracleRoomData.GameplayScreenTop);

    /// <summary>
    /// Returns the original engine's 160x128 gameplay-field coordinates.
    /// Object bounds and textbox-side selection use this coordinate space.
    /// </summary>
    internal Vector2 WorldToGameplayScreen(Vector2 worldPosition) =>
        worldPosition - CurrentCameraOrigin;

    private Vector2 CurrentCameraOrigin => _camera.Position -
        GameplayCameraOffset;

    private static Vector2 GameplayCameraOffset => new(
        OracleRoomData.ViewportWidth / 2.0f,
        OracleRoomData.ScreenHeight / 2.0f - OracleRoomData.GameplayScreenTop);

    private static Vector2 GetCameraOrigin(OracleRoomData room, Vector2 playerPosition)
    {
        float maxX = Mathf.Max(0.0f, room.Width - OracleRoomData.ViewportWidth);
        float maxY = Mathf.Max(0.0f, room.Height - OracleRoomData.ViewportHeight);
        return new Vector2(
            Mathf.Clamp(playerPosition.X - OracleRoomData.ViewportWidth / 2.0f, 0.0f, maxX),
            Mathf.Clamp(playerPosition.Y - OracleRoomData.ViewportHeight / 2.0f, 0.0f, maxY));
    }

    private void SetFade(float alpha) =>
        SetFadeColor(Colors.White, alpha);

    private void SetFadeColor(Color color, float alpha) =>
        _warpFade.Color = new Color(color.R, color.G, color.B, Mathf.Clamp(alpha, 0.0f, 1.0f));

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
        else player.Face(Vector2I.Down);
    }
}

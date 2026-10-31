using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed class AgesRoomTransitionController : RoomTransitionController
{
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

    private readonly TimePortalDatabase _timePortals;
    private readonly TimeWarpEffectDatabase _timeWarpEffects = new();
    private readonly TimeWarpLandingDatabase _timeWarpLanding = new();
    private readonly FairiesWoodsScramblerDatabase _fairiesWoodsScrambler = new();
    private DiveWarpDatabase? _diveWarpsSourceCache;
    private DiveWarpDatabase _diveWarps => _diveWarpsSourceCache ??= new(AssetRoot);
    private CollapsingFloorDatabase? _collapsingFloorsSourceCache;
    private CollapsingFloorDatabase _collapsingFloors => _collapsingFloorsSourceCache ??= new(AssetRoot);
    private bool _deepWaterDiveRequested;
    private TimeWarpPhase _timeWarpPhase;
    private bool _timeWarp;
    private int _timeWarpPhaseFrame;
    private int _timeWarpGlobalFrame;
    private int _timeWarpDissolveStep = -1;
    private int _timeWarpDissolveBufferStep = -1;
    private int _timeWarpAppliedDissolveStep = -1;
    private double _timeWarpTickAccumulator;
    private bool TimeWarpCreatesPortal => _entities.RuntimeState.ReadField("wLinkTimeWarpTile") == 0;
    private byte _timeWarpDestinationFlags;
    private bool _timeWarpArrivalRejected;
    private OracleRoomData? _timeWarpSourceRoom;
    private bool _timeWarpSourcePositionOccupied;
    private TimeWarpEffect? _timeWarpEffect;
    private readonly List<TimeWarpEffect> _continuingTimeWarpEffects = new();
    private readonly Dictionary<CanvasItem, Material?> _dissolvedItems = new();
    private ShaderMaterial? _dissolveMaterial;

    private static readonly (int Even, int Odd)[] TimeWarpDissolveMasks =
    {
        (0xdd, 0xff), (0xdd, 0xbb), (0x55, 0xbb), (0x55, 0xaa),
        (0x11, 0xaa), (0x11, 0x88), (0x00, 0x88), (0x00, 0x00)
    };

    internal bool TimeWarpActive => _timeWarp && _warpActive;
    private protected override bool ExtendedSequenceActive => _timeWarp;
    private protected override bool RetainsDestinationLinkPosition => base.RetainsDestinationLinkPosition ||
        _timeWarp && _entities.RuntimeState.ReadField("wcddf") != 0;
    internal override bool PaletteFadeActive => base.PaletteFadeActive || TimeWarpActive && (_timeWarpPhase switch
    {
        TimeWarpPhase.TimeWarpInitialize => true,
        TimeWarpPhase.TimeWarpDissolve => TimeWarpInitializeFrames + _timeWarpPhaseFrame < FastPaletteFadeFrames,
        TimeWarpPhase.TimeWarpBlackFadeIn => _timeWarpPhaseFrame < FastPaletteFadeFrames,
        TimeWarpPhase.TimeWarpWhiteFadeOut or TimeWarpPhase.TimeWarpArrivalFadeIn or TimeWarpPhase.TimeWarpReturnFadeOut => _timeWarpPhaseFrame < WarpFadeFrames,
        _ => false
    });
    internal bool TimeWarpDestinationActive => TimeWarpActive && _timeWarpPhase is
        TimeWarpPhase.TimeWarpArrivalFadeIn or TimeWarpPhase.TimeWarpArrivalWait or
        TimeWarpPhase.TimeWarpArrivalEffect or TimeWarpPhase.TimeWarpArrivalFlicker or
        TimeWarpPhase.TimeWarpRejectedFlicker or TimeWarpPhase.TimeWarpRejectedEffect or
        TimeWarpPhase.TimeWarpRejectedWait or TimeWarpPhase.TimeWarpReturnFadeOut;
    internal int TimeWarpPhaseFrame => _timeWarpPhaseFrame;
    internal int TimeWarpDissolveStep => _timeWarpDissolveStep;
    internal int TimeWarpDissolveBufferStep => _timeWarpDissolveBufferStep;
    internal int TimeWarpAppliedDissolveStep => _timeWarpAppliedDissolveStep;
    internal string TimeWarpPhaseName => _timeWarp ? _timeWarpPhase.ToString() : PhaseName;
    internal TimeWarpEffect? ActiveTimeWarpEffect => _timeWarpEffect;
    internal static (int Even, int Odd) TimeWarpDissolveMaskForValidation(int step) =>
        TimeWarpDissolveMasks[step];

    public AgesRoomTransitionController(
        RoomSession rooms, WarpDatabase warps, RoomView roomView, RoomLoadColumnRevealOverlay roomLoadReveal,
        Player player, Camera2D camera, ColorRect warpFade, Hud hud, DialogueBox dialogue,
        RoomEntityManager entities, DeathRespawnPointController deathRespawnPoints, OracleSoundEngine sound,
        TimePortalDatabase timePortals)
        : base(RequireAges(rooms),warps,roomView,roomLoadReveal,player,camera,warpFade,hud,dialogue,entities,deathRespawnPoints,sound)
    {
        _timePortals = timePortals;
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

    private static RoomSession RequireAges(RoomSession rooms) => rooms.SaveData.Game == OracleGame.Ages ? rooms :
        throw new ArgumentException("Ages transition sequences require Ages room content.",nameof(rooms));
    internal bool RequestDeepWaterDive(Player player, int packedPosition)
        => RequestUnderwaterTravel(player, packedPosition, surface: false);

    internal bool RequestUnderwaterSurface(Player player, ActiveTerrainInfo terrain)
    {
        if (UnderwaterTravelDisabled() ||
            (_rooms.CurrentRoom.TilesetFlags & (int)TilesetFlags.Underwater) == 0 ||
            terrain.Terrain.Type == TerrainType.Whirlpool)
            return false;
        if (terrain.Terrain.Type == TerrainType.WarpHole)
            return RequestUnderwaterTravel(player, terrain.PackedPosition, surface: false);
        if (!UnderwaterSurfacingDatabase.Shared.CanSurface(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, _rooms.CurrentRoom.TilesetFlags,
            terrain.PackedPosition, _rooms.SaveData.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed),
            _rooms.CurrentDungeonIndex, _rooms.SaveData.ReadField("wJabuWaterLevel")))
            return false;
        return RequestUnderwaterTravel(player, terrain.PackedPosition, surface: true);
    }

    private bool UnderwaterTravelDisabled() =>
        IsTransitioning || AllScreenTransitionsDisabledSource() || ScreenTransitionsDisabledSource() ||
        _entities.RuntimeState.ReadField("wDisableScreenTransitions") != 0;

    private bool RequestUnderwaterTravel(Player player, int packedPosition, bool surface)
    {
        // link.s:checkForUnderwaterTransition@levelDown writes a direct
        // warp before the rest of this object pass. cutscene01 consumes it
        // after func_60e9, rather than advancing a fade inside Link's update.
        if (UnderwaterTravelDisabled()) return false;
        int group = _rooms.ActiveGroup, room = _rooms.CurrentRoom.Id;
        int destinationGroup = (group + (surface ? -2 : 2)) & 7;
        int destinationRoom = room;
        if ((_rooms.CurrentRoom.TilesetFlags & (int)TilesetFlags.Dungeon) != 0)
        {
            destinationGroup = group;
            destinationRoom = surface
                ? _rooms.DungeonMaps.DungeonStairDestination(_rooms.CurrentDungeonIndex, room, floorDelta: 1).Room
                : _rooms.DungeonMaps.DungeonHoleDestination(_rooms.CurrentDungeonIndex, room).Room;
        }
        if (!_rooms.HasRoom(destinationGroup, destinationRoom))
            throw new NotSupportedException($"checkForUnderwaterTransition {(surface ? "@dungeon" : "@levelDown")}: room {group:x1}:{room:x2} " +
                $"position ${packedPosition:x2} has unavailable destination {destinationGroup:x1}:{destinationRoom:x2}.");
        _pendingWarp = new(group, room, packedPosition, 0, WarpSourceTransition.FadeOut,
            destinationGroup, destinationRoom, packedPosition, 0, WarpDestinationTransition.Basic, DirectFadeOut: true);
        _deepWaterDiveRequested = true;
        return true;
    }

    internal bool BeginRequestedDeepWaterDive(Player player)
    {
        if (!_deepWaterDiveRequested) return false;
        _deepWaterDiveRequested = false;
        if (!IsTransitioning) BeginWarp(player, _pendingWarp, delayedFadeOut: false);
        return true;
    }

    internal override bool TryGetScreenTransitionDestination(
        Vector2I direction,
        out int targetId)
    {
        if (EyePuzzleDatabase.For(AssetRoot).TryResolve(_entities.RuntimeState, _rooms.ActiveGroup,
                _rooms.CurrentRoom.Id, direction, out targetId)) return true;
        if (_rooms.ActiveGroup == 0 &&
            !_rooms.SaveData.HasGlobalFlag(
                AgesGlobalFlag.ForestUnscrambled) &&
            _fairiesWoodsScrambler.TryResolve(
                _rooms.CurrentRoom.Id, direction, out targetId))
        {
            return true;
        }
        return base.TryGetScreenTransitionDestination(direction,out targetId);
    }
    private protected override void CommitScreenTransition(Vector2I direction) =>
        EyePuzzleDatabase.For(AssetRoot).CommitTransition(_entities.RuntimeState,_rooms.ActiveGroup,_rooms.CurrentRoom.Id,direction);
    private protected override bool TryGetSpecialHoleWarp(int packedPosition, out Warp warp) =>
        _collapsingFloors.TryGetHoleWarp(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,packedPosition,out warp);
    private protected override bool TryGetInteractionTileWarp(Player player, Vector2 point, out Warp warp)
    {
        if (player.TopDownDiving && _diveWarps.TryGetDiveWarp(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,point,out DiveWarp dive))
        { warp = dive.ToWarp(); return true; }
        warp = default; return false;
    }
    private protected override bool TryResolveTileWarp(OracleRoomData room, int position, byte tile, out Warp warp, out bool staircaseCue)
    {
        staircaseCue = false;
        if (_entities.RuntimeState.ReadField("wDisableWarps") != 0 && _warps.IsWarpTile(room.ActiveCollisions,tile))
        {
            warp = new(_rooms.ActiveGroup,room.Id,position,0,WarpSourceTransition.FadeOut,
                _rooms.ActiveGroup,room.Id,position,0,WarpDestinationTransition.Basic);
            return true;
        }
        if (base.TryResolveTileWarp(room,position,tile,out warp,out staircaseCue)) return true;
        staircaseCue = TryGetDungeonStairFallback(position,tile,out warp);
        return staircaseCue;
    }
    private bool TryGetDungeonStairFallback(
        int packedPosition,
        byte tile,
        out Warp warp)
    {
        int dungeon = _rooms.CurrentDungeonIndex;
        if (dungeon < 0 ||
            !_warps.IsWarpTile(_rooms.CurrentRoom.ActiveCollisions, tile))
        {
            warp = default;
            return false;
        }

        int floorDelta = (tile & 0x01) == 0 ? 1 : -1;
        DungeonCell destination = _rooms.DungeonMaps.DungeonStairDestination(
            dungeon,
            _rooms.CurrentRoom.Id,
            floorDelta);
        warp = new Warp(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            packedPosition,
            EdgeMask: 0,
            SourceTransition: WarpSourceTransition.FadeOut,
            DestinationGroup: _rooms.ActiveGroup,
            DestinationRoom: destination.Room,
            DestinationPosition: packedPosition,
            DestinationParameter: 0,
            DestinationTransition: WarpDestinationTransition.Basic,
            DirectFadeOut: true);
        return true;
    }
    public void ApplyTimePortalWarp(Player player, Vector2 portalPosition)
    {
        BeginTimePortalWarp(
            player,
            portalPosition,
            createDestinationPortal: false,
            clearExistingPortal: false);
    }

    internal void ApplyTimePortalWarp(Player player, TimePortal portal)
    {
        RequireActive();
        BeginTimePortalWarp(
            player,
            portal.Position,
            createDestinationPortal: false,
            clearExistingPortal: portal.Temporary);
    }

    internal void ApplyHarpTimeWarp(Player player, Vector2 playerPosition)
    {
        BeginTimePortalWarp(
            player,
            playerPosition,
            createDestinationPortal: true,
            clearExistingPortal: false);
    }

    private void BeginTimePortalWarp(
        Player player,
        Vector2 portalPosition,
        bool createDestinationPortal,
        bool clearExistingPortal)
    {
        RequireActive();
        int destinationGroup = _rooms.ActiveGroup ^ 0x01;
        if (_warpActive || _rooms.ActiveGroup is not (0 or 1) ||
            !_rooms.HasRoom(destinationGroup, _rooms.CurrentRoom.Id))
            return;

        if (clearExistingPortal)
            _rooms.SaveData.ClearTimePortalLocation();
        int position = _rooms.CurrentRoom.GetPackedPosition(portalPosition);
        _timeWarpSourceRoom = _rooms.CurrentRoom;
        _timeWarpSourcePositionOccupied = _entities.TimeWarpPositionOccupied(_timeWarpLanding, position);
        _pendingWarp = new Warp(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, position, 0, 0,
            destinationGroup, _rooms.CurrentRoom.Id, position, 0, WarpDestinationTransition.TimeWarp);
        _timeWarp = true;
        _suppressDestinationMusic = false;
        _warpActive = true;
        _timeWarpPhase = TimeWarpPhase.TimeWarpInitialize;
        _warpFrame = 0.0f;
        _timeWarpPhaseFrame = 0;
        _timeWarpTickAccumulator = 0.0;
        _timeWarpGlobalFrame = _entities.FrameCounter;
        _timeWarpDissolveStep = -1;
        _timeWarpDissolveBufferStep = -1;
        _timeWarpAppliedDissolveStep = -1;
        _entities.RuntimeState.SetField("wLinkTimeWarpTile",
            createDestinationPortal ? (byte)0 : (byte)(position + 1));
        _entities.RuntimeState.SetField("wcde0",
            createDestinationPortal ? (byte)0x5b : (byte)(position + 1));
        _timeWarpArrivalRejected = false;
        _destinationWalk = false;
        _dialogue.Close();
        // interactionBeginTimewarp copies the portal's position into w1Link,
        // forces DIR_DOWN, and calls restartSound before CUTSCENE_TIMEWARP is
        // serviced on the next update.
        player.BeginTimeWarpTransition(portalPosition, portalContact: !createDestinationPortal);
        _sound.RestartSound();
        _roomView.SetBackgroundFade(Colors.Black, 0.0f);
        ClearRoomLoadColumnReveal();
        SetFade(0.0f);
    }

    public override void UpdateWarp(double delta)
    {
        if (!_warpActive) return;
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

        base.UpdateWarp(delta);
    }
    private void AdvanceTimeWarpFrame()
    {
        bool destinationUpdate = TimeWarpDestinationActive;
        bool enemiesDisabled = _timeWarpPhase is TimeWarpPhase.TimeWarpInitialize or
            TimeWarpPhase.TimeWarpArrivalFadeIn or TimeWarpPhase.TimeWarpReturnFadeOut ||
            _timeWarpPhase == TimeWarpPhase.TimeWarpDissolve &&
                TimeWarpInitializeFrames + _timeWarpPhaseFrame < FastPaletteFadeFrames;
        _timeWarpGlobalFrame = _entities.FrameCounter & 0xff;
        _timeWarpPhaseFrame++;
        switch (_timeWarpPhase)
        {
            case TimeWarpPhase.TimeWarpInitialize:
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
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpDissolve);
                }
                break;

            case TimeWarpPhase.TimeWarpDissolve:
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
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpSetup);
                }
                break;

            case TimeWarpPhase.TimeWarpSetup:
                // State 2 substeps $00/$01 reload the tilemap, then create
                // INTERAC_TIMEWARP $dd:$00 with counter1=120.
                if (_timeWarpPhaseFrame >= TimeWarpSetupFrames)
                {
                    // func_03_7244@state2@cbb3_01 publishes the selector at
                    // source-effect birth, after both tilemap setup updates.
                    _entities.RuntimeState.SetField("wcc50",(byte)
                        ((_entities.RuntimeState.ReadField("wTilesetFlags") & 0x80) == 0 ? 1 : 2));
                    SpawnTimeWarpEffect(source: true);
                    _sound.PlaySound(SoundId.SndTimewarpInitiated);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpSourceEffect);
                }
                break;

            case TimeWarpPhase.TimeWarpSourceEffect:
                if (_timeWarpPhaseFrame >= TimeWarpSourceEffectFrames)
                {
                    // CUTSCENE_TIMEWARP creates $dd:$02 and then calls
                    // objectDelete_de on $d000 (w1Link). Link remains intact
                    // through the object-gfx masking and source expansion,
                    // then disappears at this exact 120-count handoff.
                    _player.Visible = false;
                    if (_timeWarpSourcePositionOccupied)
                        _pendingWarp = _pendingWarp with
                        {
                            DestinationPosition = _rooms.CurrentRoom.GetPackedPosition(_player.Position)
                        };
                    _timeWarpEffect?.BeginSourceTrail();
                    _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpSourceTrail);
                }
                else
                {
                    _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                }
                break;

            case TimeWarpPhase.TimeWarpSourceTrail:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                if (_timeWarpPhaseFrame >= TimeWarpSourceTrailFrames)
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpBlackFadeIn);
                break;

            case TimeWarpPhase.TimeWarpBlackFadeIn:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                // Keep the source tilemap fully masked. The original palette
                // handoff transitions the displayed field from black to white;
                // exposing the unfaded Godot room between those palette
                // operations produces a map flash that the hardware path does
                // not present.
                _roomView.SetBackgroundFade(Colors.Black, 1.0f);
                if (_timeWarpPhaseFrame >= FastPaletteFadeFrames)
                {
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpWhiteFadeOut);
                    // The cutscene observes palette mode 0 on this update and
                    // immediately calls fadeoutToWhite. Apply its first step
                    // now so there is no fully neutral source-room frame
                    // between the black and white fades.
                    _timeWarpPhaseFrame = 1;
                    SetFade(_timeWarpPhaseFrame / WarpFadeFrames);
                }
                break;

            case TimeWarpPhase.TimeWarpWhiteFadeOut:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                _roomView.SetBackgroundFade(Colors.Black, 1.0f);
                SetFade(_timeWarpPhaseFrame / WarpFadeFrames);
                if (_timeWarpPhaseFrame >= WarpFadeFrames)
                {
                    SetFade(1.0f);
                    LoadWarpDestination();
                }
                break;

            case TimeWarpPhase.TimeWarpArrivalFadeIn:
                SetFade(1.0f - _timeWarpPhaseFrame / WarpFadeFrames);
                if (_timeWarpPhaseFrame >= WarpFadeFrames)
                {
                    SetFade(0.0f);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpArrivalWait);
                }
                break;

            case TimeWarpPhase.TimeWarpArrivalWait:
                if (_timeWarpPhaseFrame >= TimeWarpArrivalWaitFrames)
                {
                    SpawnTimeWarpEffect(source: false);
                    _timeWarpArrivalRejected =
                        _entities.RuntimeState.ReadField("wSentBackByStrangeForce") == 1 ||
                        !_timeWarpLanding.CanStandOnTile(_rooms.CurrentRoom, _player.Position,
                            _player.Inventory.HasTreasure(TreasureId.MermaidSuit)) ||
                        _entities.TimeWarpPositionOccupied(_timeWarpLanding, _pendingWarp.DestinationPosition);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpArrivalEffect);
                }
                break;

            case TimeWarpPhase.TimeWarpArrivalEffect:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                if (_timeWarpPhaseFrame >= TimeWarpArrivalEffectFrames)
                {
                    _player.Visible = true;
                    _sound.PlaySound(SoundId.SndTimewarpCompleted);
                    SetTimeWarpPhase(_timeWarpArrivalRejected
                        ? TimeWarpPhase.TimeWarpRejectedFlicker : TimeWarpPhase.TimeWarpArrivalFlicker);
                }
                break;

            case TimeWarpPhase.TimeWarpArrivalFlicker:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                // objectFlickerVisibility uses b=$03: only global frames
                // divisible by four are invisible.
                _player.Visible = (_timeWarpGlobalFrame & 0x03) != 0;
                if (_timeWarpPhaseFrame >= TimeWarpArrivalFlickerFrames)
                    FinishWarp();
                break;

            case TimeWarpPhase.TimeWarpRejectedFlicker:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                _player.Visible = (_timeWarpGlobalFrame & 3) != 0;
                if (_timeWarpPhaseFrame >= 0x78)
                {
                    SpawnTimeWarpEffect(source: false);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpRejectedEffect);
                }
                break;

            case TimeWarpPhase.TimeWarpRejectedEffect:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                _player.Visible = (_timeWarpGlobalFrame & 3) != 0;
                if (_timeWarpPhaseFrame >= 0x10)
                {
                    _player.Visible = false;
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpRejectedWait);
                }
                break;

            case TimeWarpPhase.TimeWarpRejectedWait:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                if (_timeWarpPhaseFrame >= 0x14)
                {
                    // warpTransition6 substate 7 returns through a direct
                    // white fade, without replaying CUTSCENE_TIMEWARP.
                    int packed = _rooms.CurrentRoom.GetPackedPosition(_player.Position);
                    if ((_timeWarpDestinationFlags & 0x10) == 0)
                        _rooms.SaveData.SetRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagVisited, false);
                    _pendingWarp = new Warp(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, packed, 0, 0,
                        _rooms.ActiveGroup ^ 1, _rooms.CurrentRoom.Id, packed, 0, WarpDestinationTransition.TimeWarp);
                    _entities.RuntimeState.SetField("wLinkTimeWarpTile", (byte)(packed + 1));
                    _entities.RuntimeState.SetField("wcddf", (byte)(packed + 1));
                    byte strangeForce = _entities.RuntimeState.ReadField("wSentBackByStrangeForce");
                    if (strangeForce != 0)
                        _entities.RuntimeState.SetField("wSentBackByStrangeForce", (byte)(strangeForce + 1));
                    _sound.PlaySound(SoundId.SndTimewarpCompleted);
                    SetTimeWarpPhase(TimeWarpPhase.TimeWarpReturnFadeOut);
                }
                break;

            case TimeWarpPhase.TimeWarpReturnFadeOut:
                _timeWarpEffect?.AdvanceFrame(_timeWarpGlobalFrame);
                SetFade(_timeWarpPhaseFrame / WarpFadeMaximumOffset);
                if (_timeWarpPhaseFrame >= WarpFadeFrames)
                    LoadWarpDestination();
                break;
        }
        // Room loading clears wDisabledObjects. During destination Link's
        // substate machine, other objects keep animating and consuming RNG;
        // wMenuDisabled/checkLinkCollisionsEnabled still prevent contact.
        if (TimeWarpActive)
        {
            if (destinationUpdate)
                _entities.UpdateObjects(1.0 / 60.0, _player, timeWarpArrival: true,
                    enemiesDisabled: enemiesDisabled);
            else
                _entities.UpdateTimeWarpSource(_player, interactionsDisabled: TimeWarpCreatesPortal,
                    enemiesDisabled: !TimeWarpCreatesPortal || enemiesDisabled);
        }
    }

    private void SetTimeWarpPhase(TimeWarpPhase phase)
    {
        _timeWarpPhase = phase;
        _timeWarpPhaseFrame = 0;
    }

    private protected override void BeforeDestinationLoad()
    {
        if (!_timeWarp) return;
        _timeWarpEffect?.StopImmediately(); _timeWarpEffect = null;
        EndTimeWarpDissolve(); _roomView.ClearBackgroundFade();
    }
    private protected override void BeforeDestinationLayout(Warp warp)
    {
        if (!_timeWarp) return;
        _timeWarpDestinationFlags = _rooms.SaveData.GetRoomFlags(warp.DestinationGroup,warp.DestinationRoom);
        if (_entities.RuntimeState.ReadField("wcddf") == 0 && _timeWarpLanding.SentBackByStrangeForce(_rooms.CurrentRoom.Id))
            _entities.RuntimeState.SetField("wSentBackByStrangeForce",1);
    }
    private protected override OracleRoomData LoadDestinationRoom(Warp warp)
    {
        bool returningThroughTime = _timeWarp && _entities.RuntimeState.ReadField("wcddf") != 0;
        OracleRoomData room;
        if (returningThroughTime)
        {
            // func_131f restores the saved source layout, collision bytes and
            // tilemap from banks 2/6, preserving unsaved cuts and dug tiles.
            room = _timeWarpSourceRoom ?? throw new InvalidOperationException(
                "warpTransition6 return has no saved source room layout.");
            _rooms.SetLoadedRoom(warp.DestinationGroup, room);
        }
        else
            room = base.LoadDestinationRoom(warp);
        if (_timeWarp && !returningThroughTime)
        {
            _timePortals.ApplyEntryTileReplacement(
                room, warp.DestinationPosition, _entities.FrameCounter);
            int returnTile = _entities.RuntimeState.ReadField("wLinkTimeWarpTile");
            if (returnTile != 0)
            {
                _timePortals.ApplyReturnTileReplacement(
                    room, returnTile - 1, _entities.FrameCounter);
            }
        }
        return room;
    }
    private protected override void BeginDestinationPresentation(Vector2 spawn)
    {
        if (!_timeWarp) { base.BeginDestinationPresentation(spawn); return; }
        _destinationWalk = false; _player.WarpTo(spawn); _player.Face(Vector2I.Down);
        _player.Visible = false; _hud.Visible = true;
        SetTimeWarpPhase(TimeWarpPhase.TimeWarpArrivalFadeIn);
    }
    private protected override void OnWarpFinished()
    {
        if (_timeWarp)
        {
            if (_timeWarpEffect is not null)
            {
                _timeWarpEffect.ContinueAfterTransition(
                    _timeWarpGlobalFrame,
                    applicationUpdateOwned: true);
                _continuingTimeWarpEffects.Add(_timeWarpEffect);
            }
            _timeWarpEffect = null;
            EndTimeWarpDissolve();
            _roomView.ClearBackgroundFade();
            _hud.Visible = true;
            if (TimeWarpCreatesPortal)
            {
                int packed = _pendingWarp.DestinationPosition;
                Vector2 position = new(
                    (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                    (packed >> 4) * OracleRoomData.MetatileSize + 8);
                _rooms.SaveData.SetTimePortalLocation(
                    _rooms.ActiveGroup,
                    _rooms.CurrentRoom.Id,
                    packed);
                _entities.SpawnTemporaryTimePortal(position);
            }
            if (_entities.RuntimeState.ReadField("wSentBackByStrangeForce") != 0)
                _dialogue.ShowMessage(_timeWarpLanding.StrangeForceText, _player.Position.Y);
            _entities.RuntimeState.SetField("wSentBackByStrangeForce", 0);
            _entities.RuntimeState.SetField("wcddf", 0);
            _entities.RuntimeState.SetField("wcde0", 0);
            _player.ApplyInteractionInvincibility(0x78); // signed byte $88
        }
        _timeWarp = false;
        _timeWarpPhase = TimeWarpPhase.None;
        _entities.RuntimeState.SetField("wLinkTimeWarpTile",0);
        _timeWarpSourceRoom = null;
    }
    private protected override void OnOrdinaryWarpStarting()
    {
        _deepWaterDiveRequested = false;
        _timeWarp = false;
        _entities.RuntimeState.SetField("wLinkTimeWarpTile",0);
    }
    private void SpawnTimeWarpEffect(bool source)
    {
        int selector = _entities.RuntimeState.ReadField("wcc50");
        if (selector is not (1 or 2))
            throw new NotSupportedException($"object_code/ages/interactions/timewarp.s: INTERAC $dd var03=0x{selector:x2} requires unimported PALH 0x{((selector + 0xc0) & 255):x2}.");
        _timeWarpEffect?.StopImmediately();
        _timeWarpEffect = new TimeWarpEffect(
            _timeWarpEffects,
            _player.Position,
            source,
            selector == 2)
        {
            Name = source ? "TimeWarpSourceEffect" : "TimeWarpArrivalEffect"
        };
        _player.GetParent().AddChild(_timeWarpEffect);
    }

    private protected override void UpdateContinuingEffects()
    {
        for (int index = _continuingTimeWarpEffects.Count - 1;
            index >= 0;
            index--)
        {
            if (!_continuingTimeWarpEffects[index].AdvanceApplicationUpdate())
                _continuingTimeWarpEffects.RemoveAt(index);
        }
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

}

internal enum TimeWarpPhase
{
    None,
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
    TimeWarpArrivalFlicker,
    TimeWarpRejectedFlicker,
    TimeWarpRejectedEffect,
    TimeWarpRejectedWait,
    TimeWarpReturnFadeOut
}

using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruIntroEvent : IRoomEvent, ICutsceneCommandHost
{
    private enum NayruStage
    {
        None,
        Crowd,
        BearLead,
        BearMove,
        BearSettle,
        BearText,
        TriggerDelay,
        TriggerText,
        TriggerPostText,
        SingingFadeIn,
        Singing,
        SingingFadeOut,
        Script
    }

    private sealed class FleeingAudience(
        NpcCharacter actor,
        NayruIntroEventDatabase.FleeRecord record,
        Vector2 velocity)
    {
        public NpcCharacter Actor { get; } = actor;
        public NayruIntroEventDatabase.FleeRecord Record { get; } = record;
        public Vector2 Velocity { get; } = velocity;
        public int Delay { get; set; } = record.Delay;
        public int ZFixed;
        public int SpeedZ = record.WaitJumpSpeedZ;
        public bool Escaping { get; set; }
    }

    private sealed class NayruAudienceTalkState(
        string actor,
        int resetAnimation,
        int resetDelay,
        bool hopping)
    {
        public string Actor { get; } = actor;
        public int ResetAnimation { get; } = resetAnimation;
        public int ResetDelay { get; } = resetDelay;
        public bool Hopping { get; } = hopping;
        public bool WaitingForText { get; set; } = true;
        public int Counter { get; set; }
        public int ZFixed;
        public int SpeedZ = hopping ? -0xc0 : 0;
    }

    private sealed class TimedNayruEffect(
        NpcCharacter actor,
        int duration,
        Vector2 velocity,
        bool sway,
        bool musicNote,
        bool floatsLeft,
        Vector2 spawnPosition,
        int soundId)
    {
        public NpcCharacter Actor { get; } = actor;
        public int Remaining { get; set; } = duration;
        public Vector2 Velocity { get; } = velocity;
        public bool Sway { get; } = sway;
        public bool MusicNote { get; } = musicNote;
        public bool FloatsLeft { get; } = floatsLeft;
        public Vector2 SpawnPosition { get; } = spawnPosition;
        public int SoundId { get; } = soundId;
        public bool SoundPending { get; set; } = soundId != 0;
    }

    private sealed class NayruVignetteMonkeyState(
        NpcCharacter actor,
        NayruIntroEventDatabase.VignetteMonkeyRecord record,
        int startupDelay,
        int jumpSpeedZ)
    {
        public NpcCharacter Actor { get; } = actor;
        public NayruIntroEventDatabase.VignetteMonkeyRecord Record { get; } = record;
        public int StartupDelay { get; } = startupDelay;
        public int JumpSpeedZ { get; } = jumpSpeedZ;
        public int ZFixed;
        public int SpeedZ = jumpSpeedZ;
        public int MovementPhase { get; set; }
        public int MovementCounter { get; set; }
        public int HopCount { get; set; }
        public int Direction { get; set; } = 1;
        public int Animation { get; set; } = record.Animation;
        public bool Stone { get; set; }
    }

    private sealed class NayruPossessionState(Vector2 nayruStart, Vector2 ralphStart)
    {
        public Vector2 NayruStart { get; } = nayruStart;
        public Vector2 RalphStart { get; } = ralphStart;
        public int Elapsed { get; set; }
        public int SwayX { get; set; }
        public int MinimumSwayX { get; set; }
        public int MaximumSwayX { get; set; }
        public int PaletteCounter { get; set; } = 15;
        public int NormalPaletteFrames { get; set; } = 15;
        public int PossessedPaletteFrames { get; set; } = 1;
        public bool PossessedPalette { get; set; }
        public bool PaletteComplete { get; set; }
        public int NayruMoveStart { get; set; } = -1;
        public int RalphMoveStart { get; set; } = -1;
    }

    private readonly RoomEventContext _context;
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly RoomTransitionController _transitions;
    private readonly Player _player;
    private readonly RoomView _roomView;
    private readonly InventoryState _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly Func<long> _animationTick;
    private readonly ImpaIntroEvent _impaEvent;
    private readonly NayruIntroEventDatabase _nayruDatabase;
    private readonly NayruIntroEventDatabase.EventRecord _nayruRecord;
    private readonly CanvasLayer _nayruInterfaceLayer;
    private readonly ColorRect _nayruFade;
    private readonly Hud _nayruHud;
    private readonly NayruActorRegistry _nayruActors;
    private readonly List<FleeingAudience> _nayruFleeingAudience = new();
    private readonly List<NayruAudienceTalkState> _nayruAudienceTalkStates = new();
    private readonly List<TimedNayruEffect> _nayruEffects = new();
    private readonly List<NayruVignetteMonkeyState> _nayruVignetteMonkeys = new();
    private readonly CutsceneCommandRunner _commandRunner;
    private NayruStage _nayruStage;
    private OracleRoomData? _nayruRoom;
    private NayruSingingScreen? _nayruSingingScreen;
    private int _nayruAudienceMask;
    private int _nayruSingingElapsed;
    private int _nayruSingingScrollCounter;
    private int _nayruNotePhase;
    private ChestTreasureEffect? _nayruSwordEffect;
    private int _nayruNoteSpawnCount;
    private int _nayruLightningSpawnCount;
    private int _nayruGhostRevealFlickerRemaining;
    private int _nayruRalphSwordAnimation = -1;
    private bool _nayruTrackLinkVeranFacing;
    private bool _nayruTrackRalphVeranFacing;
    private bool _nayruUpdateVeranFacingTarget;
    private Vector2 _nayruVeranFacingTarget;
    private bool _nayruNayruHeldVeranFacing;
    private NayruPossessionState? _nayruPossessionState;
    private bool _nayruGhostRumbling;
    private int _nayruGhostEmergencePhase;
    private int _nayruGhostEmergenceCounter;
    private bool _nayruTrackAftermathRalphFacing;
    private int _nayruVignetteIndex = -1;
    private int _nayruVignetteElapsed;
    private int _nayruVignetteExclamationCount;
    private int _nayruVignetteOldManZ;
    private int _nayruVignetteOldManSpeedZ;
    private bool _nayruMusicInitialized;
    private int _counter;
    private int _nativeZFixed;
    private int _nativeSpeedZ;
    private int _nativePhase;
    private int _nativeCounter;
    private float _nativeStartAlpha;

    private ImpaIntroEventDatabase _impaDatabase => _impaEvent.Database;
    private NpcCharacter? _impa
    {
        get => _impaEvent.Actor;
        set => _impaEvent.Actor = value;
    }

    public NayruIntroEvent(RoomEventContext context, ImpaIntroEvent impaEvent)
    {
        _context = context;
        _rooms = context.Rooms;
        _entities = context.Entities;
        _transitions = context.Transitions;
        _player = context.Player;
        _roomView = context.RoomView;
        _inventory = context.Inventory;
        _treasures = context.Treasures;
        _animationTick = context.AnimationTick;
        _impaEvent = impaEvent;
        _nayruInterfaceLayer = context.InterfaceLayer;
        _nayruFade = context.Fade;
        _nayruHud = context.Hud;
        _nayruDatabase = new NayruIntroEventDatabase();
        _nayruRecord = _nayruDatabase.Event;
        _nayruActors = new NayruActorRegistry(_rooms, _entities, _nayruDatabase);
        _commandRunner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _nayruStage != NayruStage.None;
    public bool BlocksGameplay =>
        _nayruStage is not (NayruStage.None or NayruStage.Crowd);
    internal bool CrowdActive => _nayruStage == NayruStage.Crowd;
    internal bool IntroCompleted => _rooms.SaveData.HasGlobalFlag(_nayruRecord.IntroFlag);
    internal int Counter => _counter;

    internal bool Matches(int group, OracleRoomData room) =>
        group == _nayruRecord.Group && room.Id == _nayruRecord.Room;

    internal int CurrentStage => (int)_nayruStage;
    internal NayruActorRegistry ActorRegistry => _nayruActors;
    internal int CurrentVignetteIndex => _nayruVignetteIndex;
    internal int VignetteElapsed => _nayruVignetteElapsed;

    internal void RestoreCompletedPortal(int group, OracleRoomData room)
    {
        if (group != _nayruRecord.Group || room.Id != _nayruRecord.Room ||
            !_rooms.SaveData.HasRoomFlag(
                group, room.Id, (byte)_nayruRecord.CompletionRoomFlag))
            return;
        Vector2 point = new(
            (_nayruRecord.PortalPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (_nayruRecord.PortalPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte current = room.GetMetatile(point);
        if (current != _nayruRecord.PortalTile)
        {
            room.ReplaceMetatile(
                point, current, (byte)_nayruRecord.PortalTile, _animationTick());
            _roomView.QueueRedraw();
        }
    }

    internal void Start(OracleRoomData room)
    {
        Cancel();
        _nayruRoom = room;
        _nayruAudienceMask = 0;
        _nayruNotePhase = 0;
        _nayruNoteSpawnCount = 0;
        _nayruLightningSpawnCount = 0;
        _nayruGhostRevealFlickerRemaining = 0;
        _nayruRalphSwordAnimation = -1;
        _nayruTrackLinkVeranFacing = false;
        _nayruTrackRalphVeranFacing = false;
        _nayruUpdateVeranFacingTarget = false;
        _nayruVeranFacingTarget = Vector2.Zero;
        _nayruNayruHeldVeranFacing = false;
        _nayruPossessionState = null;
        _nayruGhostRumbling = false;
        _nayruGhostEmergencePhase = 0;
        _nayruGhostEmergenceCounter = 0;
        _nayruTrackAftermathRalphFacing = false;
        _nayruVignetteIndex = -1;
        _nayruVignetteElapsed = 0;
        _nayruVignetteExclamationCount = 0;
        _nayruVignetteOldManZ = 0;
        _nayruVignetteOldManSpeedZ = 0;
        _nayruMusicInitialized = false;
        _nayruVignetteMonkeys.Clear();
        _nayruAudienceTalkStates.Clear();
        _nayruStage = NayruStage.Crowd;
        _counter = 0;

        // The positioned bear $5d:$02 and portal-departure Ralph $37:$0d are
        // later story variants. $6b:$01 owns the intro actors instead.
        _context.DeactivateNpcs(0x5d, 0x02);
        _context.DeactivateNpcs(0x37, 0x0d);

        NpcCharacter nayru = _nayruActors.Spawn("Nayru", "Nayru", solid: true);
        NayruIntroEventDatabase.ActorRecord nayruRecord = _nayruDatabase.Actor("Nayru");
        nayru.SetScriptAnimation(nayruRecord.Animation(nayruRecord.InitialAnimation));
        NpcCharacter ralph = _nayruActors.Spawn("Ralph", "Ralph", solid: true);
        NayruIntroEventDatabase.ActorRecord ralphRecord = _nayruDatabase.Actor("Ralph");
        ralph.SetScriptAnimation(ralphRecord.Animation(ralphRecord.InitialAnimation));
        NpcCharacter bear = _nayruActors.SpawnAudience(
            "Bear", 0x5702, _nayruRecord.Group, _nayruRecord.Room);
        if (!_rooms.SaveData.HasRoomFlag(
            _nayruRecord.Group, _nayruRecord.Room, (byte)_nayruRecord.BearRoomFlag))
            bear.Position += Vector2.Down * 16.0f;
        _nayruActors.SpawnAudience(
            "Monkey", 0x5704, _nayruRecord.Group, _nayruRecord.Room);
        _nayruActors.SpawnAudience(
            "Rabbit", 0x5705, _nayruRecord.Group, _nayruRecord.Room);
        _nayruActors.SpawnAudience(
            "Boy", 0x2510, _nayruRecord.Group, _nayruRecord.Room);
        _nayruActors.SpawnAudience(
            "Bird", 0x3214, _nayruRecord.Group, _nayruRecord.Room);
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_nayruStage != NayruStage.Crowd)
            return false;
        string? name = _nayruActors.NameOf(npc);
        if (name is null)
            return false;

        int bit = name switch
        {
            "Bird" => 0x01,
            "Rabbit" => 0x02,
            "Boy" => 0x04,
            "Monkey" => 0x08,
            "Bear" => 0x10,
            _ => 0
        };
        if (bit == 0)
            return false;
        if (_nayruAudienceTalkStates.Exists(state => state.Actor == name))
            return true;
        _nayruAudienceMask |= bit;
        Observe("AudienceMask", name, _nayruAudienceMask);

        if (name != "Bear")
        {
            BeginNayruAudienceTalk(name, npc);
            ShowNayruText(name switch
            {
                "Bird" => 0x3214,
                "Rabbit" => 0x5705,
                "Boy" => 0x2510,
                _ => 0x5704
            });
            return true;
        }
        if (_nayruAudienceMask != 0x1f)
        {
            ShowNayruText(0x5702);
            return true;
        }

        _player.BeginCutsceneControl();
        _counter = 20;
        _nayruStage = NayruStage.BearLead;
        _nayruActors.SetAnimation("Bear", 1);
        return true;
    }

    private void BeginNayruAudienceTalk(string name, NpcCharacter npc)
    {
        int resetAnimation;
        int resetDelay;
        bool hopping = false;
        switch (name)
        {
            case "Monkey":
                // monkeySubid0Script uses cplinkx rather than the general
                // four-way turn helper: animation $00 faces left and $01 right.
                _nayruActors.SetAnimation(
                    name, _player.Position.X <= npc.Position.X ? 0 : 1);
                resetAnimation = 2;
                resetDelay = 20;
                break;
            case "Bird":
                // birdScript_listeningToNayruGameStart adds two to cplinkx's
                // result and repeatedly applies -$00c0/$0020 Z physics while
                // the textbox is active.
                _nayruActors.SetAnimation(
                    name, _player.Position.X <= npc.Position.X ? 2 : 3);
                resetAnimation = 1;
                resetDelay = 10;
                hopping = true;
                break;
            default:
                // The rabbit and boy call scriptHelp.turnToFaceLink, whose
                // diagonal ties use convertAngleToDirection's clockwise round.
                Vector2I facing = FacingForTrackedTarget(
                    OracleObjectMath.ToPixelPosition(_player.Position) -
                    OracleObjectMath.ToPixelPosition(npc.Position));
                _nayruActors.SetAnimation(name, AnimationForFacing(facing));
                resetAnimation = 0;
                resetDelay = 10;
                break;
        }
        _nayruAudienceTalkStates.Add(new NayruAudienceTalkState(
            name, resetAnimation, resetDelay, hopping));
    }

    public void UpdateFrame()
    {
        if (!_nayruMusicInitialized)
        {
            // INTERAC_NAYRU $36:$00 restores full volume when its state 0
            // first runs after the incoming 0:39 scroll has completed.
            _context.Sound.SetMusicVolume(3);
            _nayruMusicInitialized = true;
        }
        UpdateNayruFleeingAudience();
        UpdateNayruAudienceTalks();
        UpdateNayruAmbientActors();
        UpdateNayruEffects();
        UpdateNayruSingingNotes();
        UpdateNayruRalphSword();
        if (_nayruSwordEffect is not null && !_nayruSwordEffect.Finished)
            _nayruSwordEffect.Advance(1.0 / 60.0);
        switch (_nayruStage)
        {
            case NayruStage.Crowd:
                if (!_context.DialogueOpen &&
                    _rooms.SaveData.HasRoomFlag(
                        _nayruRecord.Group, _nayruRecord.Room,
                        (byte)_nayruRecord.BearRoomFlag) &&
                    _player.Position.X >= _nayruRecord.TriggerX &&
                    _player.Position.Y < _nayruRecord.TriggerY)
                    BeginNayruTrigger();
                break;
            case NayruStage.BearLead:
                if (--_counter == 0)
                {
                    _counter = 32;
                    _nayruStage = NayruStage.BearMove;
                }
                break;
            case NayruStage.BearMove:
                _nayruActors["Bear"].Position += Vector2.Up * 0.5f;
                if (--_counter == 0)
                {
                    _counter = 50;
                    _nayruStage = NayruStage.BearSettle;
                }
                break;
            case NayruStage.BearSettle:
                if (--_counter == 0)
                {
                    _nayruActors.SetAnimation("Bear", 0);
                    _rooms.SaveData.SetRoomFlag(
                        _nayruRecord.Group, _nayruRecord.Room,
                        (byte)_nayruRecord.BearRoomFlag);
                    ShowNayruText(0x5703);
                    _nayruStage = NayruStage.BearText;
                }
                break;
            case NayruStage.BearText:
                if (!_context.DialogueOpen)
                {
                    _nayruStage = NayruStage.Crowd;
                    _player.EndCutsceneControl();
                }
                break;
            case NayruStage.TriggerDelay:
                if (--_counter == 0)
                {
                    ShowNayruText(0x5706);
                    _nayruStage = NayruStage.TriggerText;
                }
                break;
            case NayruStage.TriggerText:
                if (!_context.DialogueOpen)
                {
                    _counter = _nayruRecord.PostBearTextFrames;
                    _nayruStage = NayruStage.TriggerPostText;
                }
                break;
            case NayruStage.TriggerPostText:
                if (--_counter == 0)
                    BeginNayruSingingScreen();
                break;
            case NayruStage.SingingFadeIn:
                UpdateNayruSingingFade(fadeIn: true);
                break;
            case NayruStage.Singing:
                UpdateNayruSingingFrame();
                break;
            case NayruStage.SingingFadeOut:
                UpdateNayruSingingFade(fadeIn: false);
                break;
            case NayruStage.Script:
                UpdateNayruTimeline();
                break;
        }
    }

    private void UpdateNayruAudienceTalks()
    {
        for (int index = _nayruAudienceTalkStates.Count - 1; index >= 0; index--)
        {
            NayruAudienceTalkState state = _nayruAudienceTalkStates[index];
            if (!_nayruActors.TryGetActive(state.Actor, out NpcCharacter actor))
            {
                _nayruAudienceTalkStates.RemoveAt(index);
                continue;
            }

            if (state.WaitingForText)
            {
                if (state.Hopping)
                    UpdateNayruTalkingBirdHop(state, actor);
                if (_context.DialogueOpen)
                    continue;

                // The bird clears var37 and zh immediately after the textbox;
                // the other listeners retain their talk pose through the wait.
                state.WaitingForText = false;
                state.Counter = state.ResetDelay;
                if (state.Hopping)
                {
                    state.ZFixed = 0;
                    state.SpeedZ = 0;
                    actor.SetScriptDrawOffset(Vector2.Zero);
                }
                continue;
            }

            if (--state.Counter > 0)
                continue;
            _nayruActors.SetAnimation(state.Actor, state.ResetAnimation);
            _nayruAudienceTalkStates.RemoveAt(index);
        }
    }

    private static void UpdateNayruTalkingBirdHop(
        NayruAudienceTalkState state,
        NpcCharacter bird)
    {
        if (!OracleObjectMath.UpdateSpeedZ(
            ref state.ZFixed,
            ref state.SpeedZ,
            0x20))
        {
            bird.SetScriptDrawOffset(new Vector2(0, state.ZFixed / 256.0f));
            return;
        }

        state.SpeedZ = -0xc0;
        bird.SetScriptDrawOffset(Vector2.Zero);
    }

    private void BeginNayruTrigger()
    {
        _player.BeginCutsceneControl();
        _impaEvent.StopFollowing();
        _counter = _nayruRecord.BearDelayFrames;
        _nayruStage = NayruStage.TriggerDelay;
    }

    private void BeginNayruSingingScreen()
    {
        // CUTSCENE_NAYRU_SINGING state 0 uses the menu-close effect before
        // replacing the gameplay tilemap.
        _context.Sound.PlaySound(OracleSoundEngine.SndCloseMenu);
        _nayruSingingScreen = new NayruSingingScreen(_nayruDatabase);
        _nayruInterfaceLayer.AddChild(_nayruSingingScreen);
        _nayruHud.Visible = false;
        _nayruFade.Color = Colors.White;
        _counter = (int)InventoryMenuController.FastFadeFrames;
        _nayruStage = NayruStage.SingingFadeIn;
    }

    private void UpdateNayruSingingFade(bool fadeIn)
    {
        _counter--;
        float progress = 1.0f - _counter / InventoryMenuController.FastFadeFrames;
        _nayruFade.Color = new Color(1, 1, 1, fadeIn ? 1.0f - progress : progress);
        if (_counter != 0)
            return;
        if (fadeIn)
        {
            _nayruFade.Color = new Color(1, 1, 1, 0);
            _nayruSingingElapsed = 0;
            _nayruSingingScrollCounter = 0;
            _nayruStage = NayruStage.Singing;
            return;
        }

        _nayruFade.Color = Colors.White;
        _nayruSingingScreen?.QueueFree();
        _nayruSingingScreen = null;
        _nayruHud.Visible = true;
        BuildNayruScript();
        _nayruStage = NayruStage.Script;
    }

    private void UpdateNayruSingingFrame()
    {
        _nayruSingingElapsed++;
        if (_nayruSingingElapsed % _nayruRecord.SingingScrollPeriod == 0 &&
            _nayruSingingScrollCounter < _nayruRecord.SingingScrollSteps)
        {
            _nayruSingingScrollCounter++;
            _nayruSingingScreen!.SetScrollX(_nayruSingingScrollCounter);
        }
        int remaining = _nayruRecord.SingingFrames - _nayruSingingElapsed;
        bool skip = remaining <= _nayruRecord.SingingSkipWindow &&
            Input.IsActionJustPressed("attack");
        if (remaining > 0 && !skip)
            return;
        _context.Sound.PlaySound(OracleSoundEngine.SndCloseMenu);
        _counter = (int)InventoryMenuController.FastFadeFrames;
        _nayruStage = NayruStage.SingingFadeOut;
    }

    private void BuildNayruScript() =>
        _commandRunner.Start(_nayruDatabase.Commands);

    private void UpdateNayruTimeline()
    {
        _commandRunner.AdvanceFrame();
        if (!_commandRunner.Active && _nayruStage == NayruStage.Script)
            FinishNayruIntro();
    }
    private Vector2 ActorPosition(string actor) => actor == "Player"
        ? _player.Position
        : _nayruActors[actor].Position;

    private void SetActorPosition(
        string actor,
        Vector2 position,
        Vector2 delta,
        Vector2 movement)
    {
        if (actor == "Player")
        {
            _player.AdvanceCutsceneMovement(movement, FacingForDelta(delta));
            if (_player.Walking && _nayruActors.ContainsKey("AftermathRalph"))
                Observe("AftermathLinkWalk", "Player", position: _player.Position);
        }
        else
        {
            _nayruActors[actor].Position = position;
        }

        if (actor == "Player" &&
            position.IsEqualApprox(new Vector2(0x57, 0x3c)) &&
            _nayruActors.ContainsKey("GhostVeran"))
        {
            // linkCutscene3 enters substate $08 after its 22-update left
            // movement, six-update hold, and eight-update downward movement.
            _nayruTrackLinkVeranFacing = true;
        }
        if (actor == "Ralph" &&
            position.IsEqualApprox(new Vector2(0x88, 0x51)) &&
            _player.Position.IsEqualApprox(new Vector2(0x57, 0x30)) &&
            _nayruActors.ContainsKey("GhostVeran"))
        {
            // Ralph reaches @faceVeranGhost when the paired movedown $16
            // finishes. His object begins tracking before Link's later
            // downward movement has completed.
            _nayruTrackRalphVeranFacing = true;
        }
    }

    private static Vector2I FacingForDelta(Vector2 delta) =>
        Mathf.Abs(delta.X) > Mathf.Abs(delta.Y)
            ? (delta.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (delta.Y > 0 ? Vector2I.Down : Vector2I.Up);

    private static Vector2I FacingForTrackedTarget(Vector2 delta)
    {
        float x = Mathf.Abs(delta.X);
        float y = Mathf.Abs(delta.Y);
        if (x == 0 && y == 0)
            return Vector2I.Zero;
        if (x > y)
            return delta.X > 0 ? Vector2I.Right : Vector2I.Left;
        if (y > x)
            return delta.Y > 0 ? Vector2I.Down : Vector2I.Up;

        // objectGetRelativeAngle returns the exact diagonal here, and
        // convertAngleToDirection adds $04 before truncating. Consequently
        // diagonal ties round clockwise, not uniformly toward one axis.
        if (delta.X > 0)
            return delta.Y < 0 ? Vector2I.Right : Vector2I.Down;
        return delta.Y > 0 ? Vector2I.Left : Vector2I.Up;
    }

    private static int AnimationForFacing(Vector2I facing)
    {
        if (facing == Vector2I.Up)
            return 0;
        if (facing == Vector2I.Right)
            return 1;
        if (facing == Vector2I.Down)
            return 2;
        return 3;
    }

    private static int DirectionMask(Vector2I facing)
    {
        if (facing == Vector2I.Up)
            return 0x01;
        if (facing == Vector2I.Right)
            return 0x02;
        if (facing == Vector2I.Down)
            return 0x04;
        return facing == Vector2I.Left ? 0x08 : 0;
    }

    private void SetupNayruPossessionScene()
    {
        _player.WarpTo(new Vector2(0x78, 0x30), recordSafe: false);
        _player.Face(Vector2I.Right);
        _nayruActors["Nayru"].Position = new Vector2(0x78, 0x18);
        _nayruActors.SetAnimation("Nayru", 2);
        _nayruActors["Nayru"].SetScriptPaletteOverride(null);
        _nayruActors["Ralph"].Position = new Vector2(0x88, 0x30);
        _nayruActors.SetAnimation("Ralph", 3);
        if (_impa is null || !_impa.Active)
        {
            _impa = _nayruActors.Spawn(
                "AftermathImpa", "Impa", new Vector2(0x38, 0x68));
            _impa.SetSpritePalette(_impaDatabase.PossessedPalette);
        }
        else
        {
            _nayruActors.Register("Impa", _impa);
            _impa.Position = new Vector2(0x38, 0x68);
        }
        _impa.SetBlocksLink(false);
    }

    private void AlarmNayruAudience()
    {
        _nayruActors.SetAnimation("Bear", 2);
        _nayruActors.SetAnimation("Monkey", 6);
        _nayruActors.SetAnimation("Rabbit", 2);
        _nayruActors.SetAnimation("Boy", 2);
        // boyRunSubid00 calls interactionAnimate once before its substate
        // dispatch and a second time in shocked substate $01.
        _nayruActors["Boy"].SetAnimationRate(2.0f);
        _nayruActors.SetAnimation("Bird", 1);
    }

    private void BeginNayruAudienceEscape()
    {
        // Signal $10 advances the boy to substate $02, which returns to the
        // single interactionAnimate call before applying -$0180/$0020 Z physics.
        _nayruActors["Boy"].SetAnimationRate(1.0f);
        _nayruFleeingAudience.Clear();
        AddFlee("Bear");
        AddFlee("Monkey");
        AddFlee("Rabbit");
        AddFlee("Boy");
        AddFlee("Bird");
    }

    private void AddFlee(string actor)
    {
        NayruIntroEventDatabase.FleeRecord record = _nayruDatabase.Flee(actor);
        _nayruActors.SetAnimation(actor, record.WaitAnimation);
        _nayruFleeingAudience.Add(new FleeingAudience(
            _nayruActors[actor], record,
            OracleObjectMath.VectorFromAngle32(record.Angle) * record.Speed));
    }

    private void UpdateNayruFleeingAudience()
    {
        foreach (FleeingAudience fleeing in _nayruFleeingAudience)
        {
            if (!fleeing.Actor.Active)
                continue;
            if (!fleeing.Escaping)
            {
                bool landed = UpdateAudienceJump(
                    fleeing,
                    fleeing.Record.WaitJumpSpeedZ,
                    fleeing.Record.WaitGravity,
                    fleeing.Record.RepeatWaitJump);
                if (fleeing.Record.WaitForLanding)
                {
                    if (!landed)
                        continue;
                }
                else if (fleeing.Delay > 0)
                {
                    fleeing.Delay--;
                    if (fleeing.Delay > 0)
                        continue;
                }
                BeginAudienceEscape(fleeing);
                continue;
            }
            fleeing.Actor.Position += fleeing.Velocity;
            UpdateAudienceJump(
                fleeing,
                fleeing.Record.EscapeJumpSpeedZ,
                fleeing.Record.EscapeGravity,
                fleeing.Record.RepeatEscapeJump);
            if (!OracleObjectMath.IsInsideOriginalScreenBoundary(fleeing.Actor.Position))
            {
                fleeing.Actor.SetActive(false);
                if (fleeing.Record.Actor == "Boy")
                    Observe("BoyEscaped", "Boy");
            }
        }
    }

    private void BeginAudienceEscape(FleeingAudience fleeing)
    {
        fleeing.Escaping = true;
        fleeing.ZFixed = 0;
        fleeing.SpeedZ = fleeing.Record.EscapeJumpSpeedZ;
        fleeing.Actor.SetScriptDrawOffset(Vector2.Zero);
        _nayruActors.SetAnimation(
            fleeing.Record.Actor, fleeing.Record.EscapeAnimation);
        if (fleeing.Record.Actor == "Boy")
            Observe("BoyEscapeStarted", "Boy");
    }

    private bool UpdateAudienceJump(
        FleeingAudience fleeing,
        int initialSpeedZ,
        int gravity,
        bool repeat)
    {
        if (initialSpeedZ == 0)
            return true;
        if (!OracleObjectMath.UpdateSpeedZ(
            ref fleeing.ZFixed,
            ref fleeing.SpeedZ,
            gravity))
        {
            fleeing.Actor.SetScriptDrawOffset(new Vector2(0, fleeing.ZFixed / 256.0f));
            Observe("AudienceAirborne", fleeing.Record.Actor);
            return false;
        }
        fleeing.Actor.SetScriptDrawOffset(Vector2.Zero);
        fleeing.SpeedZ = repeat ? initialSpeedZ : 0;
        return true;
    }

    private void UpdateNayruSingingNotes()
    {
        if (_nayruStage is < NayruStage.Crowd or > NayruStage.TriggerPostText ||
            !_nayruActors.TryGetActive("Nayru", out NpcCharacter nayru))
        {
            return;
        }

        if (_nayruNotePhase is 0 or 45)
        {
            float xOffset = _nayruNotePhase == 0 ? -6.0f : 8.0f;
            SpawnNayruEffect(
                "MusicNote", nayru.Position + new Vector2(xOffset, -4),
                $"MusicNote{_nayruNoteSpawnCount}",
                floatsLeft: _nayruNotePhase == 0);
            _nayruNoteSpawnCount++;
            Observe("NoteSpawn", "Nayru", _nayruNoteSpawnCount);
        }
        _nayruNotePhase = (_nayruNotePhase + 1) % 90;
    }

    private void UpdateNayruEffects()
    {
        // floatingImage.s adds the selected table value to xh on global
        // eight-update boundaries; it is not a note-relative target offset.
        ReadOnlySpan<int> swaySteps = [-1, -2, -1, 0, 1, 2, 1, 0];
        for (int index = _nayruEffects.Count - 1; index >= 0; index--)
        {
            TimedNayruEffect effect = _nayruEffects[index];
            if (effect.SoundPending)
            {
                effect.SoundPending = false;
                _context.Sound.PlaySound(effect.SoundId);
            }
            effect.Actor.Position += effect.Velocity;
            if (effect.Sway && (_entities.FrameCounter & 7) == 0)
                effect.Actor.Position += Vector2.Right *
                    swaySteps[(_entities.FrameCounter >> 3) & 7];
            if (effect.MusicNote && effect.Actor.Position.Y < effect.SpawnPosition.Y)
            {
                if (effect.FloatsLeft && effect.Actor.Position.X < effect.SpawnPosition.X)
                    Observe("NoteMotion", effect.Actor.Name.ToString(), 0x01);
                if (!effect.FloatsLeft && effect.Actor.Position.X > effect.SpawnPosition.X)
                    Observe("NoteMotion", effect.Actor.Name.ToString(), 0x02);
            }
            effect.Remaining--;
            if (effect.Remaining > 0)
                continue;
            effect.Actor.SetActive(false);
            _nayruEffects.RemoveAt(index);
        }
    }

    private void UpdateNayruAmbientActors()
    {
        UpdateNayruVignette();

        // runVeranGhostSubid0 emits SND_RUMBLE2 on global 16-update
        // boundaries while cfd0 remains $12, from the pre-charge threat
        // through the slow backward movement.
        if (_nayruGhostRumbling && (_entities.FrameCounter & 0x0f) == 0)
            _context.Sound.PlaySound(OracleSoundEngine.SndRumble2);

        if (_nayruGhostRevealFlickerRemaining > 0 &&
            _nayruActors.TryGetActive("GhostVeran", out NpcCharacter ghost))
        {
            ghost.Visible = (_entities.FrameCounter & 1) != 0;
            _nayruGhostRevealFlickerRemaining--;
            if (_nayruGhostRevealFlickerRemaining == 0)
            {
                ghost.Visible = true;
                // runVeranGhostSubid0 starts this cue when its initial $5a
                // flicker counter expires and the ghost appears from Impa.
                _context.Sound.PlaySound(OracleSoundEngine.MusRoomOfRites);
            }
        }

        UpdateGhostVeranEmergence();
        UpdateNayruPossessionRecovery();

        // runVeranGhostSubid0 writes its integer YX position to cfd5/cfd6
        // before advancing the flight script. Link and Ralph read that cached
        // position independently; neither faces the live object directly.
        if (_nayruUpdateVeranFacingTarget &&
            _nayruActors.TryGetActive("GhostVeran", out NpcCharacter trackedGhost))
        {
            _nayruVeranFacingTarget = OracleObjectMath.ToPixelPosition(trackedGhost.Position);
            if (_nayruActors.TryGetValue("Nayru", out _) &&
                !_nayruActors.IsUsingAnimation("Nayru", 2))
            {
                _nayruNayruHeldVeranFacing = false;
            }
        }
        if (_nayruTrackLinkVeranFacing && (_entities.FrameCounter & 7) == 0)
        {
            Vector2I facing = FacingForTrackedTarget(
                _nayruVeranFacingTarget -
                OracleObjectMath.ToPixelPosition(_player.Position));
            if (facing != Vector2I.Zero)
            {
                _player.Face(facing);
                Observe("LinkVeranFacing", "Player", DirectionMask(facing));
            }
            if (!_nayruUpdateVeranFacingTarget)
                Observe("GhostTrackingPhase", "Player", 0x08);
        }
        if (_nayruTrackRalphVeranFacing && (_entities.FrameCounter & 15) == 0 &&
            _nayruActors.TryGetActive("Ralph", out NpcCharacter trackingRalph))
        {
            Vector2I facing = FacingForTrackedTarget(
                _nayruVeranFacingTarget -
                OracleObjectMath.ToPixelPosition(trackingRalph.Position));
            if (facing != Vector2I.Zero)
            {
                _nayruActors.SetAnimation("Ralph", AnimationForFacing(facing));
                Observe("RalphVeranFacing", "Ralph", DirectionMask(facing));
            }
        }

        // linkCutscene4 reads Ralph's cfd5/cfd6 position every eight updates
        // until his subid $02 script signals cfd0=$20 and deletes itself.
        if (_nayruTrackAftermathRalphFacing &&
            (_entities.FrameCounter & 7) == 0 &&
            _nayruActors.TryGetActive(
                "AftermathRalph", out NpcCharacter aftermathRalph))
        {
            Vector2I facing = FacingForTrackedTarget(
                OracleObjectMath.ToPixelPosition(aftermathRalph.Position) -
                OracleObjectMath.ToPixelPosition(_player.Position));
            _player.Face(facing);
            Observe("AftermathRalphFacing", "Player", DirectionMask(facing));
        }

    }

    private void UpdateNayruVignette()
    {
        if (_nayruVignetteIndex < 0)
            return;
        _nayruVignetteElapsed++;
        if (_nayruVignetteIndex == 0)
        {
            // INTERAC_MISCELLANEOUS_1 $6b:$05 runs restartSound on its first
            // script update, then waits 120 updates before MUS_DISASTER.
            if (_nayruVignetteElapsed == 1)
                _context.Sound.RestartSound();
            else if (_nayruVignetteElapsed == 121)
                _context.Sound.PlaySound(OracleSoundEngine.MusDisaster);
        }
        switch (_nayruVignetteIndex)
        {
            case 0:
                UpdateNayruLightningVignette();
                break;
            case 1:
                UpdateNayruMonkeyVignette();
                break;
            case 2:
                UpdateNayruStoneChildVignette();
                break;
        }
    }

    private void UpdateNayruLightningVignette()
    {
        int frame = _nayruVignetteElapsed;
        UpdateNayruVignetteLightningShade(frame);
        if (!_nayruActors.TryGetValue("VignetteGuy", out NpcCharacter? guy) ||
            !_nayruActors.TryGetValue("VignetteGirl", out NpcCharacter? girl) ||
            !_nayruActors.TryGetValue("VignetteOldMan", out NpcCharacter? oldMan))
            return;

        if (frame == 343)
        {
            guy.SetAnimationRate(0.0f);
            girl.SetAnimationRate(0.0f);
        }
        if (frame is >= 343 and <= 680)
        {
            guy.Position = new Vector2(0x78 - (_entities.FrameCounter & 1), guy.Position.Y);
            girl.Position = new Vector2(0x68 - (_entities.FrameCounter & 1), girl.Position.Y);
        }
        else if (frame is >= 681 and <= 696)
        {
            girl.Position = new Vector2(0x68 - (_entities.FrameCounter & 1), girl.Position.Y);
        }

        if (frame is 601 or 621 or 641 or 661)
        {
            Vector2 position = frame switch
            {
                601 => new Vector2(0x28, 0x28),
                621 => new Vector2(0x38, 0x58),
                641 => new Vector2(0x68, 0x38),
                _ => new Vector2(0x98, 0x48)
            };
            SpawnNayruLightning(position);
        }
        if (frame == 681)
            SpawnNayruLightning(guy.Position);
        if (frame == 697)
        {
            guy.SetActive(false);
            oldMan.SetActive(true);
            oldMan.Visible = true;
            oldMan.SetAnimationRate(0.0f);
        }

        if (frame is >= 728 and <= 755)
        {
            int airborneFrame = frame - 727;
            int z = airborneFrame * -0x1c0 +
                0x10 * airborneFrame * (airborneFrame - 1);
            girl.SetScriptDrawOffset(new Vector2(0, z / 256.0f));
            if (z < 0)
                Observe("VignetteGirlJump", "VignetteGirl", z);
        }
        else if (frame == 756)
        {
            girl.SetScriptDrawOffset(Vector2.Zero);
            girl.SetAnimationRate(2.0f);
        }
        if (frame == 727)
            _context.Sound.PlaySound(OracleSoundEngine.SndJump);
        if (frame == 846)
            _nayruActors.SetAnimationIfChanged("VignetteGirl", 0);
        if (frame is >= 876 and <= 937)
        {
            if (frame == 876)
            {
                _nayruActors.SetAnimationIfChanged("VignetteGirl", 0);
                Observe("VignetteMovement", "VignetteGirl", 0);
            }
            girl.Position += Vector2.Up;
        }

        // npcTurnedToOldManCutsceneScript writes cfdf and immediately enters
        // jumpAndWaitUntilLanded; its first eleven airborne updates remain
        // visible beneath the outgoing white fade.
        if (frame >= 937 && oldMan.Active)
        {
            if (frame == 937)
            {
                _nayruVignetteOldManZ = 0;
                _nayruVignetteOldManSpeedZ = -0x200;
                _context.Sound.PlaySound(OracleSoundEngine.SndJump);
            }
            if (!OracleObjectMath.UpdateSpeedZ(
                ref _nayruVignetteOldManZ,
                ref _nayruVignetteOldManSpeedZ,
                0x30))
            {
                oldMan.SetScriptDrawOffset(
                    new Vector2(0, _nayruVignetteOldManZ / 256.0f));
            }
            else
            {
                oldMan.SetScriptDrawOffset(Vector2.Zero);
            }
        }
    }

    private void UpdateNayruVignetteLightningShade(int frame)
    {
        float alpha;
        if (frame is >= 121 and <= 312)
        {
            int pulse = (frame - 121) % 48;
            alpha = pulse switch
            {
                < 16 => 0.5f * (pulse + 1) / 16.0f,
                < 24 => 0.5f,
                < 40 => 0.5f * (40 - pulse - 1) / 16.0f,
                _ => 0.0f
            };
        }
        else if (frame is >= 433 and <= 592)
        {
            int pulse = (frame - 433) % 16;
            alpha = pulse switch
            {
                < 4 => 0.5f * (pulse + 1) / 4.0f,
                < 8 => 0.5f,
                < 12 => 0.5f * (12 - pulse - 1) / 4.0f,
                _ => 0.0f
            };
        }
        else if (frame is >= 593 and <= 600)
        {
            alpha = 0.5f * (frame - 592) / 8.0f;
        }
        else if (frame is > 600 and <= 937)
        {
            alpha = 0.5f;
        }
        else
        {
            return;
        }
        _nayruFade.Color = new Color(0, 0, 0, alpha);
    }

    private void UpdateNayruMonkeyVignette()
    {
        int frame = _nayruVignetteElapsed;
        foreach (NayruVignetteMonkeyState state in _nayruVignetteMonkeys)
        {
            if (!state.Actor.Active)
                continue;
            int index = state.Record.Index;
            int stoneFrame = index switch
            {
                0 or 1 or 2 or 4 => state.Record.StoneCounter + state.StartupDelay,
                5 => 300,
                8 => 450,
                _ => state.Record.StoneCounter
            };

            if (!state.Stone && frame < stoneFrame)
            {
                if (index is 0 or 1 or 2 or 4 && frame > state.StartupDelay)
                    UpdateNayruVignetteMonkeyHop(state);
                else if (index == 8)
                    UpdateNayruVignetteMonkeyEight(state, frame);
                else if (index == 9)
                    UpdateNayruVignetteMonkeyNine(state, frame);
            }
            if (index == 5 && frame == 120)
                SpawnNayruExclamation(state.Actor.Position + new Vector2(-8, -13), 90);
            if (index == 8 && frame == 180)
                SpawnNayruExclamation(state.Actor.Position + new Vector2(-8, -13), 60);

            if (!state.Stone && frame == stoneFrame)
            {
                state.Stone = true;
                state.Actor.SetAnimationRate(0.0f);
                state.Actor.SetScriptDrawOffset(Vector2.Zero);
                state.Actor.SetScriptPaletteOverride(_nayruDatabase.StoneSpritePalette);
                _context.Sound.PlaySound(OracleSoundEngine.SndClink);
                Observe("VignetteMonkeyStone", state.Actor.Name.ToString());
            }
            if (!state.Stone)
                continue;

            int flickerFrame = stoneFrame + 60;
            if (frame >= flickerFrame)
            {
                state.Actor.Visible = (_entities.FrameCounter & 1) != 0;
                Observe("VignetteMonkeyFlicker", state.Actor.Name.ToString());
            }
            if (index == 8)
            {
                if (frame >= 570)
                    state.Actor.Visible = false;
                if (frame >= 600)
                    state.Actor.SetActive(false);
            }
            else if (frame >= stoneFrame + 120)
            {
                state.Actor.SetActive(false);
            }
        }
    }

    private void UpdateNayruVignetteMonkeyHop(NayruVignetteMonkeyState state)
    {
        if (!OracleObjectMath.UpdateSpeedZ(
            ref state.ZFixed,
            ref state.SpeedZ,
            0x10))
        {
            state.Actor.SetScriptDrawOffset(new Vector2(0, state.ZFixed / 256.0f));
            Observe("VignetteMonkeyHop", state.Actor.Name.ToString());
            return;
        }
        state.SpeedZ = state.JumpSpeedZ;
        state.Actor.SetScriptDrawOffset(Vector2.Zero);
    }

    private void UpdateNayruVignetteMonkeyEight(
        NayruVignetteMonkeyState state,
        int frame)
    {
        if (frame < 180)
        {
            state.Actor.SetAnimationRate((_entities.FrameCounter & 1) == 0 ? 1.0f : 0.0f);
            return;
        }
        state.Actor.SetAnimationRate(0.0f);
        if (frame >= 270 && (_entities.FrameCounter & 0x0f) == 0)
        {
            state.Animation ^= 1;
            _nayruActors.SetAnimationIfChanged(
                $"VignetteMonkey{state.Record.Index}", state.Animation);
        }
    }

    private void UpdateNayruVignetteMonkeyNine(
        NayruVignetteMonkeyState state,
        int frame)
    {
        if (frame == 1)
        {
            state.SpeedZ = -0x100;
            SetVignetteMonkeyAnimation(state, 0);
        }
        int heightAnimation = state.ZFixed <= -0x400 ? 1 : 0;
        SetVignetteMonkeyAnimation(state, heightAnimation);
        switch (state.MovementPhase)
        {
            case 0:
                if (!OracleObjectMath.UpdateSpeedZ(
                    ref state.ZFixed,
                    ref state.SpeedZ,
                    0x20))
                {
                    state.Actor.SetScriptDrawOffset(
                        new Vector2(0, state.ZFixed / 256.0f));
                    state.Actor.Position += Vector2.Right * state.Direction;
                    Observe("VignetteMonkeyHop", state.Actor.Name.ToString());
                    return;
                }
                state.Actor.SetScriptDrawOffset(Vector2.Zero);
                state.SpeedZ = -0x100;
                state.HopCount++;
                if (state.HopCount == 3)
                {
                    state.MovementPhase = 1;
                    state.MovementCounter = 16;
                }
                break;
            case 1:
                if (--state.MovementCounter == 0)
                {
                    state.Direction = -state.Direction;
                    SetVignetteMonkeyAnimation(state, state.Direction < 0 ? 3 : 8);
                    state.SpeedZ = -0x100;
                    state.MovementPhase = 2;
                    state.MovementCounter = 16;
                    Observe("VignetteMonkeyPacing", state.Actor.Name.ToString());
                }
                break;
            case 2:
                if (--state.MovementCounter == 0)
                {
                    state.HopCount = 0;
                    state.MovementPhase = 0;
                }
                break;
        }
    }

    private void SetVignetteMonkeyAnimation(NayruVignetteMonkeyState state, int animation)
    {
        if (state.Animation == animation)
            return;
        state.Animation = animation;
        _nayruActors.SetAnimationIfChanged(
            $"VignetteMonkey{state.Record.Index}", animation);
    }

    private void UpdateNayruStoneChildVignette()
    {
        int frame = _nayruVignetteElapsed;
        if (!_nayruActors.TryGetValue("VignetteBoy", out NpcCharacter? boy) ||
            !_nayruActors.TryGetValue("VignetteLady", out NpcCharacter? lady))
            return;

        if (frame == 1)
        {
            _nayruActors.SetAnimationIfChanged("VignetteBoy", 3);
            boy.SetAnimationRate(2.0f);
            Observe("VignetteMovement", "VignetteBoy", 3);
        }
        if (frame <= 80)
            boy.Position = new Vector2(0x78 - frame, 0x48);
        else if (frame <= 88)
        {
            boy.Position = new Vector2(0x28, 0x48);
            boy.SetAnimationRate(0.0f);
        }
        else if (frame <= 168)
        {
            if (frame == 89)
            {
                _nayruActors.SetAnimationIfChanged("VignetteBoy", 1);
                boy.SetAnimationRate(2.0f);
                Observe("VignetteMovement", "VignetteBoy", 1);
            }
            boy.Position = new Vector2(0x28 + frame - 88, 0x48);
        }
        else if (frame <= 176)
        {
            boy.Position = new Vector2(0x78, 0x48);
            boy.SetAnimationRate(0.0f);
        }
        else if (frame <= 224)
        {
            if (frame == 177)
            {
                _nayruActors.SetAnimationIfChanged("VignetteBoy", 3);
                boy.SetAnimationRate(2.0f);
                Observe("VignetteMovement", "VignetteBoy", 3);
            }
            boy.Position = new Vector2(0x78 - (frame - 176), 0x48);
        }
        else if (frame <= 364)
        {
            boy.Position = new Vector2(0x48, 0x48);
            boy.SetAnimationRate(0.0f);
        }
        else
        {
            boy.Position = new Vector2(
                frame <= 428 ? 0x48 - (frame - 364) * 0.25f : 0x38,
                0x48);
            boy.SetAnimationRate(frame <= 458 ? 1.0f : 0.0f);
        }
        if (frame == 224)
            SpawnNayruExclamation(boy.Position + new Vector2(0, -13), 60);
        if (frame is >= 275 and <= 364 && (_entities.FrameCounter & 7) == 0)
        {
            bool stone = (_entities.FrameCounter & 8) != 0;
            boy.SetScriptPaletteOverride(stone
                ? _nayruDatabase.StoneSpritePalette
                : _nayruDatabase.BoySpritePalette);
            if (stone)
                Observe("VignetteBoyPalette", "VignetteBoy");
        }
        if (frame == 365)
        {
            boy.SetScriptPaletteOverride(_nayruDatabase.StoneSpritePalette);
            _nayruActors.SetAnimationIfChanged("VignetteBoy", 3);
            Observe("VignetteMovement", "VignetteBoy", 3);
        }

        if (frame == 459)
        {
            _nayruActors.SetAnimationIfChanged("VignetteLady", 2);
            lady.SetAnimationRate(3.0f);
            Observe("VignetteMovement", "VignetteLady", 2);
        }
        if (frame is >= 459 and <= 472)
            lady.Position = new Vector2(0x68, 0x28 + (frame - 458) * 2.5f);
        else if (frame is >= 473 and <= 476)
        {
            lady.Position = new Vector2(0x68, 0x4b);
            lady.SetAnimationRate(1.0f);
        }
        else if (frame is >= 477 and <= 489)
        {
            if (frame == 477)
            {
                _nayruActors.SetAnimationIfChanged("VignetteLady", 3);
                lady.SetAnimationRate(3.0f);
                Observe("VignetteMovement", "VignetteLady", 3);
            }
            lady.Position = new Vector2(0x68 - (frame - 476) * 2.5f, 0x4b);
        }
        else if (frame is >= 490 and <= 505)
        {
            lady.Position = new Vector2(71.5f, 0x4b);
            lady.SetAnimationRate(1.0f);
        }
        else if (frame is >= 506 and <= 565)
        {
            lady.SetAnimationRate(0.0f);
        }
        else if (frame is >= 566 and <= 585)
        {
            lady.SetAnimationRate(3.0f);
            Observe("VignetteLadyCadence", "VignetteLady");
        }
        else if (frame >= 586)
        {
            lady.SetAnimationRate(0.0f);
        }
    }

    private void SpawnNayruExclamation(Vector2 position, int duration)
    {
        string name = $"VignetteExclamation{_nayruVignetteExclamationCount}";
        NpcCharacter actor = _nayruActors.Spawn("Exclamation", name, position);
        NayruIntroEventDatabase.ActorRecord record = _nayruDatabase.Actor("Exclamation");
        actor.SetScriptAnimation(record.Animation(record.InitialAnimation));
        actor.SetAnimationRate(1.0f);
        _nayruEffects.Add(new TimedNayruEffect(
            actor, duration, Vector2.Zero, false, false, false, position, 0));
        _nayruVignetteExclamationCount++;
        Observe("VignetteExclamation", name, _nayruVignetteExclamationCount, position);
    }

    private NpcCharacter SpawnNayruEffect(
        string template,
        Vector2 position,
        string name,
        bool floatsLeft = false)
    {
        NayruIntroEventDatabase.EffectRecord effect = _nayruDatabase.Effect(template);
        NpcCharacter actor = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            effect.ToNpcRecord(
                _rooms.ActiveGroup,
                _rooms.CurrentRoom.Id,
                Mathf.RoundToInt(position.Y),
                Mathf.RoundToInt(position.X)),
            $"NayruIntroEffect_{name}"));
        actor.Position = position;
        actor.SetScriptAnimation(effect.Animation);
        float velocityX = effect.VelocityXFixed / 256.0f;
        if (floatsLeft)
            velocityX = -velocityX;
        _nayruEffects.Add(new TimedNayruEffect(
            actor,
            effect.Duration,
            new Vector2(velocityX, effect.VelocityYFixed / 256.0f),
            effect.Sway,
            template == "MusicNote",
            floatsLeft,
            position,
            template == "Lightning" ? OracleSoundEngine.SndLightning : 0));
        return actor;
    }

    private void SpawnNayruLightning(Vector2 position)
    {
        SpawnNayruEffect("Lightning", position, $"Lightning{_nayruLightningSpawnCount}");
        _nayruLightningSpawnCount++;
        Observe("LightningSpawn", value: _nayruLightningSpawnCount, position: position);
    }

    private void SpawnGhostVeran()
    {
        Vector2 position = _nayruActors["Impa"].Position;
        _nayruActors["Impa"].SetActive(false);
        SpawnCollapsedImpa(position, "CollapsedImpa");
        _nayruActors.Spawn("GhostVeran", "GhostVeran", position).SetScriptAnimation(
            _nayruDatabase.Actor("GhostVeran").Animation(0));
        _nayruGhostRevealFlickerRemaining = 90;
        _nayruVeranFacingTarget = OracleObjectMath.ToPixelPosition(position);
        _nayruUpdateVeranFacingTarget = true;
        _nayruNayruHeldVeranFacing =
            _nayruActors.IsUsingAnimation("Nayru", 2);
        // Impa substate $0e creates INTERAC_GHOST_VERAN and plays this on
        // the same object update.
        _context.Sound.PlaySound(OracleSoundEngine.SndBossDead);
    }

    private void BeginGhostRumble() => _nayruGhostRumbling = true;

    private void BeginGhostCharge()
    {
        _nayruGhostRumbling = false;
        _context.Sound.PlaySound(OracleSoundEngine.SndSwordSpin);
        NpcCharacter ghost = _nayruActors["GhostVeran"];
        ghost.Position = new Vector2(0x78, ghost.Position.Y);
        _nayruActors.SetAnimation("Nayru", 2);
        if (_nayruNayruHeldVeranFacing &&
            _nayruActors.IsUsingAnimation("Nayru", 2))
        {
            // nayruScript00_part1 never calls turnToFaceSomething. Its
            // explicit animation $02 is held while angle $00 moves backward.
            Observe("GhostTrackingPhase", "Nayru", 0x04);
        }
    }

    private void FinishGhostCharge()
    {
        // Ghost substate 6 stops updating cfd5/cfd6 after this script ends.
        // Link and Ralph continue reading the final cached collision point.
        _nayruUpdateVeranFacingTarget = false;
        _nayruActors.SetAnimation("Nayru", 2);
        if (_nayruActors.IsUsingAnimation("Nayru", 2))
            Observe("PostChargeFacing", "Nayru", 2);
        _context.Sound.PlaySound(OracleSoundEngine.SndKillEnemy);
    }

    private void BeginNayruPossessionRecovery()
    {
        NpcCharacter nayru = _nayruActors["Nayru"];
        NpcCharacter ralph = _nayruActors["Ralph"];
        _nayruActors.SetAnimation("Nayru", 2);
        nayru.SetScriptPaletteOverride(null);
        _nayruActors.SetAnimation("Ralph", 0);
        _nayruTrackRalphVeranFacing = false;
        if (_nayruActors.IsUsingAnimation("Ralph", 0))
        {
            // cfd0=$15 exits @faceVeranGhost and selects animation $00.
            Observe("GhostTrackingPhase", "Ralph", 0x10);
        }
        _nayruPossessionState = new NayruPossessionState(nayru.Position, ralph.Position);
    }

    private void UpdateNayruPossessionRecovery()
    {
        NayruPossessionState? state = _nayruPossessionState;
        if (state is null ||
            !_nayruActors.TryGetValue("Nayru", out NpcCharacter? nayru) ||
            !_nayruActors.TryGetValue("Ralph", out NpcCharacter? ralph))
        {
            return;
        }

        state.Elapsed++;
        int elapsed = state.Elapsed;

        // cfd16 is written after Nayru's 120-update wait. Her palette begins
        // alternating immediately, then applyspeed $81 starts 30 updates later.
        if (elapsed >= 120)
            UpdateNayruPossessionPalette(state, nayru);

        if (elapsed is >= 150 and < 279)
        {
            if (state.NayruMoveStart < 0)
                state.NayruMoveStart = elapsed;
            int movementFrame = elapsed - 150 + 1;
            if ((_entities.FrameCounter & 7) == 0)
            {
                ReadOnlySpan<int> sway = [-1, -1, -1, 0, 1, 1, 1, 0];
                state.SwayX += sway[(_entities.FrameCounter >> 3) & 7];
                state.MinimumSwayX = Math.Min(state.MinimumSwayX, state.SwayX);
                state.MaximumSwayX = Math.Max(state.MaximumSwayX, state.SwayX);
                // The original table travels three pixels between its two
                // extrema. Which extremum is the initial coordinate depends
                // on the persistent global frame phase.
                if (state.MaximumSwayX - state.MinimumSwayX >= 3)
                    Observe("PossessionSway", "Nayru");
            }
            int forwardPixels = movementFrame * 0x20 >> 8;
            nayru.Position = new Vector2(
                state.NayruStart.X + state.SwayX,
                state.NayruStart.Y + forwardPixels);
        }
        else if (elapsed == 279)
        {
            // The script clamps the accumulated SPEED_020 result with
            // setcoords $28,$78 after its 129 applyspeed updates.
            nayru.Position = new Vector2(0x78, 0x28);
        }

        if (elapsed is >= 220 and < 349)
        {
            if (state.RalphMoveStart < 0)
                state.RalphMoveStart = elapsed;
            int movementFrame = elapsed - 220 + 1;
            int retreatPixels = movementFrame * 0x20 >> 8;
            ralph.Position = state.RalphStart + Vector2.Down * retreatPixels;
        }
        else if (elapsed == 349)
        {
            ralph.Position = state.RalphStart + Vector2.Down * 16.0f;
        }

        if (elapsed == 489)
        {
            SetNayruPossessionPalette(nayru, possessed: true);
            _nayruActors.SetAnimation("Nayru", 5);
            _context.Sound.PlaySound(OracleSoundEngine.SndSwordObtained);
        }
        if (elapsed < 549)
            return;

        _nayruActors.SetAnimation("Nayru", 2);
        StartGhostVeranEmergence();
        bool movementSynchronized =
            state.NayruMoveStart == 150 && state.RalphMoveStart == 220 &&
            nayru.Position == new Vector2(0x78, 0x28) &&
            ralph.Position == state.RalphStart + Vector2.Down * 16.0f &&
            _nayruActors.IsUsingAnimation("Nayru", 2) &&
            _nayruActors.IsUsingAnimation("Ralph", 0);
        if (movementSynchronized)
            Observe("PossessionMovementSync", "Nayru");
        _nayruPossessionState = null;
    }

    private void UpdateNayruPossessionPalette(
        NayruPossessionState state,
        NpcCharacter nayru)
    {
        if (state.PaletteComplete || --state.PaletteCounter != 0)
            return;

        state.PossessedPalette = !state.PossessedPalette;
        SetNayruPossessionPalette(nayru, state.PossessedPalette);

        if ((_entities.FrameCounter & 1) == 0)
        {
            state.NormalPaletteFrames--;
            state.PossessedPaletteFrames++;
            if (state.NormalPaletteFrames == state.PossessedPaletteFrames)
            {
                state.PaletteComplete = true;
                state.PossessedPalette = true;
                SetNayruPossessionPalette(nayru, possessed: true);
                return;
            }
        }

        state.PaletteCounter = state.PossessedPalette
            ? state.PossessedPaletteFrames
            : state.NormalPaletteFrames;
    }

    private void SetNayruPossessionPalette(NpcCharacter nayru, bool possessed)
    {
        ulong previous = nayru.CurrentAnimationPixelHash;
        nayru.SetScriptPaletteOverride(
            possessed ? _nayruDatabase.PossessedSpritePalette : null);
        if (previous != nayru.CurrentAnimationPixelHash)
            Observe("PossessionBlink", "Nayru", possessed ? 1 : 0);
    }

    private void StartGhostVeranEmergence()
    {
        if (!_nayruActors.TryGetValue("GhostVeran", out NpcCharacter? ghost))
            return;
        ghost.Position = new Vector2(0x78, 0x24);
        ghost.SetActive(true);
        ghost.Visible = true;
        _nayruGhostEmergencePhase = 1;
        _nayruGhostEmergenceCounter = 30;
    }

    private void UpdateGhostVeranEmergence()
    {
        if (_nayruGhostEmergencePhase == 0 ||
            !_nayruActors.TryGetActive("GhostVeran", out NpcCharacter ghost))
        {
            return;
        }
        if (_nayruGhostEmergencePhase == 1)
        {
            if (--_nayruGhostEmergenceCounter > 0)
                return;
            _nayruGhostEmergencePhase = 2;
            _nayruGhostEmergenceCounter = 69;
            return;
        }
        if (_nayruGhostEmergencePhase != 2)
            return;

        ghost.Position += Vector2.Up * 0.25f;
        if (--_nayruGhostEmergenceCounter > 0)
            return;
        _nayruGhostEmergencePhase = 3;
        if (ghost.Position == new Vector2(0x78, 0x24 - 17.25f) && ghost.Visible)
            Observe("GhostEmergence", "GhostVeran", position: ghost.Position);
    }

    private void SpawnHumanVeran()
    {
        NpcCharacter human = _nayruActors.Spawn(
            "HumanVeran", "HumanVeran", _nayruActors["GhostVeran"].Position);
        human.SetScriptAnimation(_nayruDatabase.Actor("HumanVeran").Animation(0));
        _context.Sound.PlaySound(OracleSoundEngine.SndTeleport);
    }

    private void HideGhostVeranAfterPossession()
    {
        if (!_nayruActors.TryGetValue("GhostVeran", out NpcCharacter? ghost))
            return;
        ghost.Visible = false;
        if (ghost.Active && !ghost.Visible)
            Observe("GhostHiddenAfterPossession", "GhostVeran");
    }

    private void SpawnRalphSword()
    {
        NpcCharacter sword = _nayruActors.Spawn(
            "RalphSword", "RalphSword", _nayruActors["Ralph"].Position);
        sword.SetScriptAnimation(_nayruDatabase.Actor("RalphSword").Animation(0));
        sword.SetActive(false);
        _nayruRalphSwordAnimation = -1;
    }

    private void UpdateNayruRalphSword()
    {
        if (!_nayruActors.TryGetValue("RalphSword", out NpcCharacter? sword) ||
            !_nayruActors.TryGetActive("Ralph", out NpcCharacter ralph))
            return;
        sword.Position = ralph.Position;
        if (!_nayruActors.IsUsingAnimation("Ralph", 4))
        {
            sword.SetActive(false);
            return;
        }

        int animation = Mathf.Clamp(ralph.CurrentAnimationFrame, 0, 4);
        if (animation != _nayruRalphSwordAnimation)
        {
            sword.SetScriptAnimation(_nayruDatabase.Actor("RalphSword").Animation(animation));
            _nayruRalphSwordAnimation = animation;
        }
        sword.SetActive(true);
        sword.Visible = true;
        if (sword.CurrentAnimationOpaquePixels > 0)
            Observe("RalphSwordVisible", "RalphSword", animation, sword.Position);
        if (_nayruActors.TryGetValue("Nayru", out NpcCharacter? nayru) &&
            ralph.Position.DistanceTo(nayru.Position) >= 32.0f)
        {
            Observe("RalphSwordSpacing", "RalphSword");
        }
    }

    private NpcCharacter SpawnCollapsedImpa(Vector2 position, string name)
    {
        NayruIntroEventDatabase.ActorRecord actor = _nayruDatabase.Actor("AftermathImpa");
        // subid $01 state 0 explicitly selects animation $06 after loading
        // spr_impafainted; that sheet's first body tiles are OAM slots 0 and 2.
        string animation = actor.Animation(6);
        NpcDatabase.NpcRecord record = actor.ToNpcRecord(
            _nayruRecord.Group, _nayruRecord.Room) with
        {
            SpriteName = "spr_impafainted",
            TileBase = 0,
            UpAnimation = animation,
            RightAnimation = animation,
            DownAnimation = animation,
            LeftAnimation = animation
        };
        NpcCharacter npc = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record, name));
        npc.Position = position;
        _nayruActors.Register(name, npc);
        if (record.DownAnimation == actor.Animation(6) &&
            npc.CurrentAnimationOpaquePixels > 0)
        {
            Observe("CollapsedImpaRendered", name, position: position);
        }
        return npc;
    }

    private void ActivateNayruPortal()
    {
        // cfd2 becomes nonzero on this update. linkCutscene3 stops reading
        // cfd5/cfd6 and forces left, while Ralph's script also selects $03.
        _nayruTrackLinkVeranFacing = false;
        _player.Face(Vector2I.Left);
        _nayruActors.SetAnimation("Ralph", 3);
        if (_player.FacingVector == Vector2I.Left &&
            _nayruActors.IsUsingAnimation("Ralph", 3))
        {
            Observe("GhostTrackingPhase", "Ralph", 0x20);
        }
        _rooms.SaveData.SetRoomFlag(
            _nayruRecord.Group, _nayruRecord.Room,
            (byte)_nayruRecord.CompletionRoomFlag);
        Vector2 point = new(
            (_nayruRecord.PortalPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (_nayruRecord.PortalPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte current = _nayruRoom!.GetMetatile(point);
        _nayruRoom.ReplaceMetatile(
            point, current, (byte)_nayruRecord.PortalTile, _animationTick());
        _roomView.QueueRedraw();
    }

    private void BeginNayruVignette(int index)
    {
        _nayruRoom?.ClearTemporaryBackgroundPalette(_animationTick());
        ClearNayruActors();
        NayruIntroEventDatabase.VignetteRecord vignette = _nayruDatabase.Vignette(index);
        LoadNayruCutsceneRoom(vignette.Group, vignette.Room, includeTimePortals: false);
        _nayruVignetteIndex = index;
        _nayruVignetteElapsed = 0;
        _nayruVignetteOldManZ = 0;
        _nayruVignetteOldManSpeedZ = 0;
        Observe("VignetteVisited", value: 1 << index);
        _player.Visible = false;
        // loadGfxRegisterStateIndex $02 restores the status bar after each
        // cutscene_loadRoomObjectSetAndFadein room load.
        _nayruHud.Visible = true;
        switch (index)
        {
            case 0:
                NpcCharacter guy = _nayruActors.Spawn("VignetteGuy", "VignetteGuy");
                guy.SetScriptAnimation(_nayruDatabase.Actor("VignetteGuy").Animation(3));
                NpcCharacter oldMan = _nayruActors.Spawn(
                    "VignetteOldMan", "VignetteOldMan");
                oldMan.SetScriptAnimation(_nayruDatabase.Actor("VignetteOldMan").Animation(4));
                oldMan.SetAnimationRate(0.0f);
                oldMan.SetActive(false);
                NpcCharacter girl = _nayruActors.Spawn("VignetteGirl", "VignetteGirl");
                girl.SetScriptAnimation(_nayruDatabase.Actor("VignetteGirl").Animation(1));
                break;
            case 1:
                foreach (NayruIntroEventDatabase.VignetteMonkeyRecord record in
                    _nayruDatabase.VignetteMonkeys)
                {
                    NpcCharacter monkey = _nayruActors.Spawn(
                        "Monkey", $"VignetteMonkey{record.Index}",
                        new Vector2(record.X, record.Y));
                    monkey.SetScriptAnimation(
                        _nayruDatabase.Actor("Monkey").Animation(record.Animation));
                    int random = (_entities.FrameCounter + record.Index * 7) & 0x0f;
                    int startupDelay = random == 0 ? 0x100 : random;
                    monkey.AdjustInitialAnimationCounter(random - 7);
                    int[] jumpSpeeds = [-0x80, -0xa0, -0x70, -0x90];
                    int jumpSpeed = jumpSpeeds[
                        ((_entities.FrameCounter >> 4) + record.Index * 5) & 3];
                    var state = new NayruVignetteMonkeyState(
                        monkey, record, startupDelay, jumpSpeed);
                    _nayruVignetteMonkeys.Add(state);
                    if (record.Index is 5 or 8 or 9)
                        monkey.SetAnimationRate(0.0f);
                    if (record.Index == 5)
                        monkey.SetScriptPaletteOverride(_nayruDatabase.BoySpritePalette);
                }
                break;
            case 2:
                NpcCharacter boy = _nayruActors.Spawn("VignetteBoy", "VignetteBoy");
                boy.SetScriptAnimation(_nayruDatabase.Actor("VignetteBoy").Animation(1));
                boy.SetScriptPaletteOverride(_nayruDatabase.BoySpritePalette);
                boy.SetAnimationRate(0.0f);
                NpcCharacter lady = _nayruActors.Spawn("VignetteLady", "VignetteLady");
                lady.SetScriptAnimation(_nayruDatabase.Actor("VignetteLady").Animation(2));
                break;
        }
    }

    private void BeginNayruAftermath()
    {
        ClearNayruActors();
        LoadNayruCutsceneRoom(
            _nayruRecord.Group, _nayruRecord.Room, includeTimePortals: true);
        _player.Visible = true;
        _nayruHud.Visible = true;
        NpcCharacter ralph = _nayruActors.Spawn("AftermathRalph", "AftermathRalph");
        // @initSubid02 selects animation $09 before ralphSubid02Script starts.
        ralph.SetScriptAnimation(_nayruDatabase.Actor("AftermathRalph").Animation(9));
        SpawnCollapsedImpa(new Vector2(0x38, 0x68), "AftermathImpaCollapsed");
        // State $0f issues these back-to-back after restoring room 0:39.
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
        _context.Sound.PlaySound(OracleSoundEngine.MusSadness);
        _player.WarpTo(new Vector2(0x58, 0x38), recordSafe: false);
        _player.Face(Vector2I.Up);
        _nayruTrackAftermathRalphFacing = true;
    }

    private void FinishAftermathRalphDeparture()
    {
        _nayruTrackAftermathRalphFacing = false;
        _nayruActors.Hide("AftermathRalph");
    }

    private void LoadNayruCutsceneRoom(int group, int room, bool includeTimePortals)
    {
        OracleRoomData loaded = _rooms.LoadCutsceneRoom(group, room);
        // LoadCutsceneRoom intentionally bypasses the ordinary entity-loaded
        // notification. Reapply the completed $0:$39 portal explicitly when
        // the vignette sequence returns to the source room, just as the
        // ordinary entry path does.
        RestoreCompletedPortal(group, loaded);
        _nayruRoom = loaded;
        _roomView.SetRoom(loaded.Texture);
        _entities.LoadCutsceneRoom(group, loaded, includeTimePortals);
        _transitions.UpdateCamera();
    }

    private void RestoreAftermathImpa()
    {
        _nayruActors.Hide("AftermathImpaCollapsed");
        NpcCharacter impa = _nayruActors.Spawn(
            "AftermathImpa", "AftermathImpa", new Vector2(0x38, 0x68));
        impa.SetScriptAnimation(_nayruDatabase.Actor("AftermathImpa").Animation(2));
    }

    private void BeginNayruSwordGift()
    {
        RemoveNayruSwordEffect();
        TreasureDatabase.TreasureObjectRecord sword =
            _treasures.GetObject("TREASURE_OBJECT_SWORD_00");
        _nayruSwordEffect = new ChestTreasureEffect { Name = "NayruSwordGift", ZIndex = 12 };
        _nayruSwordEffect.Initialize(
            // Treasure grab mode $01 calls objectTakePositionWithOffset with
            // b=$f2/c=$fc: 14 pixels above and four pixels left of Link.
            _player.Position + new Vector2(-4, -14),
            _treasures.GetObjectVisual(sword.Graphic));
        _roomView.GetParent().AddChild(_nayruSwordEffect);
    }

    private void GrantNayruSword()
    {
        _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWORD_00"));
        _player.BeginGetItemOneHandPose();
        // TREASURE_OBJECT_SWORD_00 uses grab mode $01; treasure state 3 plays
        // its collection behavior's SND_GETITEM as Link raises the item.
        _context.Sound.PlaySound(OracleSoundEngine.SndGetItem);
        _nayruHud.Refresh();
        Observe("SwordGift", "Player");
    }

    private void FinishNayruIntro()
    {
        if (_nayruStage == NayruStage.None)
            return;
        _rooms.SaveData.SetGlobalFlag(_nayruRecord.IntroFlag);
        _nayruRoom?.ClearTemporaryBackgroundPalette(_animationTick());
        ClearNayruActors();
        RemoveNayruSwordEffect();
        _nayruFade.Color = new Color(1, 1, 1, 0);
        _nayruHud.Visible = true;
        _player.Visible = true;
        _player.EndCutsceneControl();
        _player.Face(Vector2I.Left);
        _nayruTrackLinkVeranFacing = false;
        _nayruTrackRalphVeranFacing = false;
        _nayruUpdateVeranFacingTarget = false;
        _nayruTrackAftermathRalphFacing = false;
        _commandRunner.Clear();
        _nayruStage = NayruStage.None;
    }

    private void ClearNayruActors()
    {
        _nayruActors.Clear(deactivateActors: true);
        _nayruFleeingAudience.Clear();
        _nayruAudienceTalkStates.Clear();
        _nayruVignetteMonkeys.Clear();
        _nayruVignetteIndex = -1;
        ClearNayruEffects(deactivateActors: true);
        _impa = null;
    }

    private void ClearNayruEffects(bool deactivateActors)
    {
        if (deactivateActors)
        {
            foreach (TimedNayruEffect effect in _nayruEffects)
                effect.Actor.SetActive(false);
        }
        _nayruEffects.Clear();
    }

    private void RemoveNayruSwordEffect()
    {
        _player.EndGetItemOneHandPose();
        if (_nayruSwordEffect is null)
            return;
        Node? parent = _nayruSwordEffect.GetParent();
        parent?.RemoveChild(_nayruSwordEffect);
        _nayruSwordEffect.QueueFree();
        _nayruSwordEffect = null;
    }

    public void Cancel() => Cancel(deactivateActors: true);

    internal void Cancel(bool deactivateActors)
    {
        _nayruSingingScreen?.QueueFree();
        _nayruSingingScreen = null;
        _nayruFade?.Set("color", new Color(1, 1, 1, 0));
        if (_nayruHud is not null)
            _nayruHud.Visible = true;
        _player.Visible = true;
        _nayruRoom?.ClearTemporaryBackgroundPalette(_animationTick());
        _nayruActors.Clear(deactivateActors);
        _nayruFleeingAudience.Clear();
        _nayruAudienceTalkStates.Clear();
        _nayruVignetteMonkeys.Clear();
        _nayruVignetteIndex = -1;
        ClearNayruEffects(deactivateActors);
        RemoveNayruSwordEffect();
        _commandRunner.Clear();
        _nayruTrackLinkVeranFacing = false;
        _nayruTrackRalphVeranFacing = false;
        _nayruUpdateVeranFacingTarget = false;
        _nayruTrackAftermathRalphFacing = false;
        _nayruGhostRumbling = false;
        _nayruMusicInitialized = false;
        _nayruRoom = null;
        _nayruStage = NayruStage.None;
    }

    private void ShowNayruText(int textId)
    {
        NayruIntroEventDatabase.TextRecord text = _nayruDatabase.Text(textId);
        int? textboxPosition = text.TextboxPosition >= 0 ? text.TextboxPosition : null;
        _context.ShowDialogue(text.Message, textboxPosition);
    }

    private void Observe(
        string observation,
        string? actor = null,
        int value = 0,
        Vector2 position = default) =>
        _context.CommandTraceSink?.RecordObservation(
            new CutsceneObservationTraceEntry(
                _entities.FrameCounter,
                "NayruIntro",
                observation,
                actor is null
                    ? (CutsceneActorId?)null
                    : new CutsceneActorId(actor),
                value,
                position));

    bool ICutsceneCommandHost.DialogueOpen => _context.DialogueOpen;
    bool ICutsceneCommandHost.IsLinkedGame => _rooms.SaveData.IsLinkedGame;
    int ICutsceneCommandHost.FrameCounter => _entities.FrameCounter;
    ICutsceneCommandTraceSink? ICutsceneCommandHost.TraceSink =>
        _context.CommandTraceSink;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value is "Player" or "Impa" or "GhostVeran" or "HumanVeran" or
            "RalphSword" or "AftermathRalph" or "AftermathImpa" ||
        _nayruDatabase.HasActor(actor.Value);

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
            _player.EndCutsceneControl();
        else
            _player.BeginCutsceneControl();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled)
    {
    }

    void ICutsceneCommandHost.SetDisabledObjects(int value)
    {
    }

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException($"Unknown Nayru cutscene gate '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"Unknown Nayru cutscene memory binding '{binding}'.");

    void ICutsceneCommandHost.ShowText(int textId, string message) =>
        ShowNayruText(textId);

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        _nayruActors.SetAnimation(actor, animation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        _nayruActors.SetAnimation(actor, AnimationForFacing(
            FacingForDelta(OracleObjectMath.VectorFromAngle32(angle))));

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        throw new InvalidOperationException(
            $"Nayru actor '{actor}' does not expose script collision changes.");

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw new InvalidOperationException(
            $"Nayru actor '{actor}' does not expose A-button sensitivity changes.");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw new InvalidOperationException(
            "The imported Nayru controller uses fixed translated actor lanes.");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        _nayruActors[actor].SetScriptDrawOffset(new Vector2(0, zFixed / 256.0f));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        _nayruActors[actor].Visible = visible;

    Vector2 ICutsceneCommandHost.GetActorPosition(CutsceneActorId actor) =>
        ActorPosition(actor.Value);

    void ICutsceneCommandHost.SetActorPosition(
        CutsceneActorId actor,
        Vector2 position,
        Vector2 facingDelta,
        Vector2 movement) =>
        SetActorPosition(actor.Value, position, facingDelta, movement);

    void ICutsceneCommandHost.CompleteActorTranslation(CutsceneActorId actor)
    {
        if (actor.Value == "Player")
        {
            // linkCutscene3 stops calling specialObjectAnimate when each
            // counter reaches zero. Clear Player's walking-body selection on
            // that same fixed update while preserving the final facing.
            _player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Zero);
        }
    }

    void ICutsceneCommandHost.DeleteActor(CutsceneActorId actor) =>
        _nayruActors.Hide(actor.Value);

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw new InvalidOperationException(
            $"Unknown Nayru cutscene memory binding '{binding}'.");

    void ICutsceneCommandHost.PlaySound(int sound) =>
        _context.Sound.PlaySound(sound);

    void ICutsceneCommandHost.SetGlobalFlag(int flag) =>
        _rooms.SaveData.SetGlobalFlag(flag);

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        _rooms.SaveData.SetRoomFlag(
            _nayruRecord.Group, _nayruRecord.Room, (byte)flag);

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SetupNayruPossessionScene": SetupNayruPossessionScene(); break;
            case "FacePlayerUp": _player.Face(Vector2I.Up); break;
            case "FacePlayerRight": _player.Face(Vector2I.Right); break;
            case "FacePlayerDown": _player.Face(Vector2I.Down); break;
            case "FacePlayerLeft": _player.Face(Vector2I.Left); break;
            case "FastMusicFadeOut":
                _context.Sound.PlaySound(OracleSoundEngine.SndCtrlFastFadeOut);
                break;
            case "MediumMusicFadeOut":
                _context.Sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
                break;
            case "PlaySideviewMusic":
                _context.Sound.PlaySound(OracleSoundEngine.MusLadxSideview);
                break;
            case "AlarmNayruAudience": AlarmNayruAudience(); break;
            case "SpawnGhostVeran": SpawnGhostVeran(); break;
            case "BeginNayruAudienceEscape": BeginNayruAudienceEscape(); break;
            case "PlayDoubleUnknown5":
                _context.Sound.PlaySound(OracleSoundEngine.SndUnknown5);
                _context.Sound.PlaySound(OracleSoundEngine.SndUnknown5);
                break;
            case "SpawnHumanVeran": SpawnHumanVeran(); break;
            case "HideHumanVeran": _nayruActors.Hide("HumanVeran"); break;
            case "BeginGhostRumble": BeginGhostRumble(); break;
            case "BeginGhostCharge": BeginGhostCharge(); break;
            case "FinishGhostCharge": FinishGhostCharge(); break;
            case "HideGhostVeranAfterPossession": HideGhostVeranAfterPossession(); break;
            case "BeginNayruPossessionRecovery": BeginNayruPossessionRecovery(); break;
            case "SpawnRalphSword": SpawnRalphSword(); break;
            case "SpawnPortalLightning":
                SpawnNayruLightning(new Vector2(0x28, 0x24));
                break;
            case "ActivateNayruPortal": ActivateNayruPortal(); break;
            case "HideGhostVeran": _nayruActors.Hide("GhostVeran"); break;
            case "HideNayru": _nayruActors.Hide("Nayru"); break;
            case "BeginNayruVignette0": BeginNayruVignette(0); break;
            case "BeginNayruVignette1": BeginNayruVignette(1); break;
            case "BeginNayruVignette2": BeginNayruVignette(2); break;
            case "BeginNayruAftermath": BeginNayruAftermath(); break;
            case "FinishAftermathRalphDeparture": FinishAftermathRalphDeparture(); break;
            case "RestoreAftermathImpa": RestoreAftermathImpa(); break;
            case "BeginNayruSwordGift": BeginNayruSwordGift(); break;
            case "GrantNayruSword": GrantNayruSword(); break;
            case "RemoveNayruSwordEffect": RemoveNayruSwordEffect(); break;
            case "RestoreRoomMusic":
                _context.Sound.PlayRoomMusic(_nayruRecord.Group, _nayruRecord.Room);
                break;
            case "PlaySwordObtained":
                _context.Sound.PlaySound(OracleSoundEngine.SndSwordObtained);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown native Nayru cutscene handler '{handler}'.");
        }
    }

    bool ICutsceneCommandHost.UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload) => handler switch
        {
            "Jump" => UpdateNayruJump(actor, commandUpdate),
            "PortalFlight" => UpdateNayruPortalFlight(actor, commandUpdate),
            "RoomPalette" => UpdateNayruRoomPalette(commandUpdate, frames),
            "Fade" => UpdateNayruFade(commandUpdate, frames, payload),
            "Flicker" => UpdateNayruFlicker(actor, commandUpdate, frames, payload),
            _ => throw new InvalidOperationException(
                $"Unknown blocking Nayru cutscene handler '{handler}'.")
        };

    private bool UpdateNayruJump(CutsceneActorId? actorId, int commandUpdate)
    {
        string actor = RequireNativeActor(actorId, "Jump");
        if (commandUpdate == 0)
        {
            _nativeZFixed = 0;
            _nativeSpeedZ = _nayruRecord.NpcJumpSpeedZ;
            Observe("RalphJump", actor);
            _context.Sound.PlaySound(OracleSoundEngine.SndJump);
        }
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _nativeZFixed, ref _nativeSpeedZ, _nayruRecord.NpcJumpGravity);
        _nayruActors[actor].SetScriptDrawOffset(
            landed ? Vector2.Zero : new Vector2(0, _nativeZFixed / 256.0f));
        return landed;
    }

    private bool UpdateNayruPortalFlight(CutsceneActorId? actorId, int commandUpdate)
    {
        string actor = RequireNativeActor(actorId, "PortalFlight");
        NpcCharacter nayru = _nayruActors[actor];
        if (commandUpdate == 0)
        {
            _nativeSpeedZ = _nayruRecord.NayruAscentSpeedZ;
            _nativeZFixed = 0;
            _nativePhase = 0;
            _nayruActors.SetAnimation(actor, 5);
            _context.Sound.PlaySound(OracleSoundEngine.SndSwordSpin);
        }
        if (_nativePhase == 0)
        {
            _nativeZFixed += _nativeSpeedZ;
            nayru.SetScriptDrawOffset(new Vector2(0, _nativeZFixed / 256.0f));
            if (_nativeZFixed > _nayruRecord.NayruTransferZ - 0x400)
                return false;
            nayru.Position = new Vector2(0x28, 0x38);
            _nativeZFixed = _nayruRecord.NayruTransferZ;
            nayru.SetScriptDrawOffset(new Vector2(0, _nativeZFixed / 256.0f));
            _nativeCounter = _nayruRecord.NayruLandingDelay;
            _nativePhase = 1;
            return false;
        }
        if (_nativePhase == 1)
        {
            if (--_nativeCounter > 0)
                return false;
            _nativeSpeedZ = _nayruRecord.NayruFallSpeedZ;
            _nativePhase = 2;
        }
        if (!OracleObjectMath.UpdateSpeedZ(
                ref _nativeZFixed, ref _nativeSpeedZ, _nayruRecord.NayruFallGravity))
        {
            nayru.SetScriptDrawOffset(new Vector2(0, _nativeZFixed / 256.0f));
            return false;
        }
        nayru.SetScriptDrawOffset(Vector2.Zero);
        _nayruActors.SetAnimation(actor, 2);
        _context.Sound.PlaySound(OracleSoundEngine.SndSlash);
        if (nayru.Position == new Vector2(0x28, 0x38))
            Observe("PortalFlight", actor, position: nayru.Position);
        return true;
    }

    private bool UpdateNayruRoomPalette(int commandUpdate, int frames)
    {
        int elapsed = commandUpdate + 1;
        float blend = Mathf.Min(16, (elapsed + 1) / 2) / 16.0f;
        _nayruRoom!.SetTemporaryBackgroundPalette(
            _nayruDatabase.DarkBackgroundPalettes, blend);
        if (elapsed < frames)
            return false;
        if (_nayruRoom.TemporaryBackgroundPaletteBlend >= 1.0f)
            Observe("DarkPalette");
        return true;
    }

    private bool UpdateNayruFade(int commandUpdate, int frames, string direction)
    {
        if (commandUpdate == 0)
            _nativeStartAlpha = _nayruFade.Color.A;
        float target = direction == "in" ? 0.0f : direction == "out" ? 1.0f :
            throw new InvalidOperationException($"Unknown Nayru fade direction '{direction}'.");
        float progress = (commandUpdate + 1.0f) / frames;
        _nayruFade.Color = new Color(
            1, 1, 1, Mathf.Lerp(_nativeStartAlpha, target, progress));
        return commandUpdate + 1 >= frames;
    }

    private bool UpdateNayruFlicker(
        CutsceneActorId? actorId,
        int commandUpdate,
        int frames,
        string completedHandler)
    {
        string name = RequireNativeActor(actorId, "Flicker");
        NpcCharacter? actor = null;
        if (_nayruActors.TryGetValue(name, out actor))
            actor.Visible = (_entities.FrameCounter & 1) != 0;
        if (commandUpdate + 1 < frames)
            return false;
        if (actor is not null)
            actor.Visible = actor.Active;
        if (!string.IsNullOrEmpty(completedHandler))
            ((ICutsceneCommandHost)this).RunNativeHandler(completedHandler);
        return true;
    }

    private static string RequireNativeActor(CutsceneActorId? actor, string handler) =>
        actor?.Value ?? throw new InvalidOperationException(
            $"Native cutscene handler '{handler}' requires a typed actor binding.");

    void ICutsceneCommandHost.ScriptEnded() => FinishNayruIntro();

}

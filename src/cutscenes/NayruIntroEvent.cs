using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruIntroEvent : IRoomEvent
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

    private enum NayruCommandKind
    {
        Wait, Text, Move, ParallelMove, Jump, PortalFlight, RoomPalette,
        Animation, Callback, Fade, Flicker, PaletteFlicker
    }

    [Flags]
    private enum NayruMoveFacing : ulong
    {
        NayruApproach = 1UL << 0,
        ImpaRight = 1UL << 1,
        ImpaUp = 1UL << 2,
        RalphSwordLeft = 1UL << 3,
        RalphSwordUp = 1UL << 4,
        RalphPortalUp = 1UL << 5,
        RalphPortalLeft = 1UL << 6,
        NayruPortalUp = 1UL << 7,
        VignetteGirlUp = 1UL << 8,
        VignetteBoyLeft1 = 1UL << 9,
        VignetteBoyRight = 1UL << 10,
        VignetteBoyLeft2 = 1UL << 11,
        VignetteLadyDown = 1UL << 12,
        VignetteLadyLeft = 1UL << 13,
        AftermathRalphRight = 1UL << 14,
        AftermathRalphDown = 1UL << 15,
        AftermathImpaRight = 1UL << 16,
        AftermathImpaDown = 1UL << 17,
        ImpaSpin = 1UL << 18,
        RalphSecondRetreat = 1UL << 19,
        AftermathRalphStaggerRight = 1UL << 20,
        AftermathRalphCliffLeft = 1UL << 21,
        VignetteBoyStoneLeft = 1UL << 22,
        All = (1UL << 23) - 1
    }

    private sealed class NayruCommand
    {
        public NayruCommandKind Kind { get; init; }
        public int Frames { get; init; }
        public int Frames2 { get; init; }
        public int Value { get; init; } = -1;
        public int TextId { get; init; }
        public string Actor { get; init; } = string.Empty;
        public string Actor2 { get; init; } = string.Empty;
        public Vector2 Delta { get; init; }
        public Vector2 Delta2 { get; init; }
        public Action? Callback { get; init; }
        public Color FadeColor { get; init; } = Colors.White;
        public float TargetAlpha { get; init; }
        public float StartAlpha { get; set; }
        public int Counter { get; set; }
        public bool Started { get; set; }
        public Vector2 StartPosition { get; set; }
        public Vector2 StartPosition2 { get; set; }
        public int ZFixed { get; set; }
        public int SpeedZ { get; set; }
        public int Phase { get; set; }
        public ulong FacingAuditBit { get; init; }
        public bool SetFacingOnStart { get; init; }
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
        public int ZFixed { get; set; }
        public int SpeedZ { get; set; } = record.WaitJumpSpeedZ;
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
        public int ZFixed { get; set; }
        public int SpeedZ { get; set; } = hopping ? -0xc0 : 0;
    }

    private sealed class TimedNayruEffect(
        NpcCharacter actor,
        int duration,
        Vector2 velocity,
        bool sway,
        bool musicNote,
        bool floatsLeft,
        Vector2 spawnPosition)
    {
        public NpcCharacter Actor { get; } = actor;
        public int Remaining { get; set; } = duration;
        public Vector2 Velocity { get; } = velocity;
        public bool Sway { get; } = sway;
        public bool MusicNote { get; } = musicNote;
        public bool FloatsLeft { get; } = floatsLeft;
        public Vector2 SpawnPosition { get; } = spawnPosition;
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
        public int ZFixed { get; set; }
        public int SpeedZ { get; set; } = jumpSpeedZ;
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
        public int PaletteCounter { get; set; } = 15;
        public int NormalPaletteFrames { get; set; } = 15;
        public int PossessedPaletteFrames { get; set; } = 1;
        public bool PossessedPalette { get; set; }
        public bool PaletteComplete { get; set; }
        public int NayruMoveStart { get; set; } = -1;
        public int RalphMoveStart { get; set; } = -1;
    }

    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly RoomTransitionController _transitions;
    private readonly DialogueBox _dialogue;
    private readonly Player _player;
    private readonly RoomView _roomView;
    private readonly InventoryState _inventory;
    private readonly TreasureDatabase _treasures;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly Func<long> _animationTick;
    private readonly ImpaIntroEvent _impaEvent;
    private readonly NayruIntroEventDatabase _nayruDatabase;
    private readonly NayruIntroEventDatabase.EventRecord _nayruRecord;
    private readonly CanvasLayer _nayruInterfaceLayer;
    private readonly ColorRect _nayruFade;
    private readonly Hud _nayruHud;
    private readonly Dictionary<string, NpcCharacter> _nayruActors = new();
    private readonly List<FleeingAudience> _nayruFleeingAudience = new();
    private readonly List<NayruAudienceTalkState> _nayruAudienceTalkStates = new();
    private readonly List<TimedNayruEffect> _nayruEffects = new();
    private readonly List<NayruVignetteMonkeyState> _nayruVignetteMonkeys = new();
    private readonly Queue<NayruCommand> _nayruCommands = new();
    private NayruStage _nayruStage;
    private NayruCommand? _nayruCommand;
    private OracleRoomData? _nayruRoom;
    private NayruSingingScreen? _nayruSingingScreen;
    private int _nayruAudienceMask;
    private int _nayruSingingElapsed;
    private int _nayruSingingScrollCounter;
    private int _nayruNotePhase;
    private ChestTreasureEffect? _nayruSwordEffect;
    private bool _nayruSwordGiftShown;
    private int _nayruVisitedVignettes;
    private int _nayruNoteSpawnCount;
    private int _nayruLightningSpawnCount;
    private Vector2 _nayruInitialMoveEnd = new(-1, -1);
    private bool _nayruCollapsedImpaRendered;
    private int _nayruGhostRevealFlickerRemaining;
    private int _nayruRalphJumpCount;
    private int _nayruRalphSwordAnimation = -1;
    private bool _nayruDarkPaletteShown;
    private bool _nayruAudienceJumpShown;
    private bool _nayruVeranReactionMoved;
    private bool _nayruPossessionFlashShown;
    private bool _nayruRalphSwordShown;
    private bool _nayruPortalFlightShown;
    private bool _nayruBoyEscaped;
    private bool _nayruBoyEscapeStarted;
    private bool _nayruGhostTrackingShown;
    private bool _nayruTrackLinkVeranFacing;
    private bool _nayruTrackRalphVeranFacing;
    private bool _nayruUpdateVeranFacingTarget;
    private Vector2 _nayruVeranFacingTarget;
    private int _nayruGhostTrackingMask;
    private int _nayruLinkVeranFacingMask;
    private int _nayruRalphVeranFacingMask;
    private bool _nayruNayruHeldVeranFacing;
    private bool _nayruBackstepShown;
    private bool _nayruGhostHiddenAfterPossession;
    private bool _nayruSwordSpacingShown;
    private bool _nayruAftermathLinkWalkShown;
    private int _nayruNoteMotionMask;
    private NayruPossessionState? _nayruPossessionState;
    private int _nayruPossessionPaletteFlips;
    private bool _nayruPossessionBlinkShown;
    private bool _nayruPossessionSwayShown;
    private bool _nayruPossessionMovementSyncShown;
    private bool _nayruPostChargeFacingShown;
    private int _nayruGhostEmergencePhase;
    private int _nayruGhostEmergenceCounter;
    private bool _nayruGhostEmergenceShown;
    private ulong _nayruMoveFacingMask;
    private bool _nayruTrackAftermathRalphFacing;
    private int _nayruAftermathRalphFacingMask;
    private int _nayruVignetteIndex = -1;
    private int _nayruVignetteElapsed;
    private int _nayruVignetteExclamationCount;
    private int _nayruVignetteOldManZ;
    private int _nayruVignetteOldManSpeedZ;
    private bool _nayruVignetteGirlJumpShown;
    private bool _nayruVignetteMonkeyHopShown;
    private bool _nayruVignetteMonkeyPacingShown;
    private bool _nayruVignetteMonkeyStoneShown;
    private bool _nayruVignetteMonkeyFlickerShown;
    private bool _nayruVignetteBoyPaletteShown;
    private bool _nayruVignetteLadyCadenceShown;
    private int _counter;

    private ImpaIntroEventDatabase _impaDatabase => _impaEvent.Database;
    private NpcCharacter? _impa
    {
        get => _impaEvent.Actor;
        set => _impaEvent.Actor = value;
    }

    public NayruIntroEvent(RoomEventContext context, ImpaIntroEvent impaEvent)
    {
        _rooms = context.Rooms;
        _entities = context.Entities;
        _transitions = context.Transitions;
        _dialogue = context.Dialogue;
        _player = context.Player;
        _roomView = context.RoomView;
        _inventory = context.Inventory;
        _treasures = context.Treasures;
        _worldToScreen = context.WorldToScreen;
        _animationTick = context.AnimationTick;
        _impaEvent = impaEvent;
        _nayruInterfaceLayer = context.InterfaceLayer;
        _nayruFade = context.Fade;
        _nayruHud = context.Hud;
        _nayruDatabase = new NayruIntroEventDatabase();
        _nayruRecord = _nayruDatabase.Event;
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
    internal int AudienceMask => _nayruAudienceMask;
    internal IReadOnlyDictionary<string, NpcCharacter> Actors => _nayruActors;
    internal int VisitedVignettes => _nayruVisitedVignettes;
    internal int NoteSpawnCount => _nayruNoteSpawnCount;
    internal int LightningSpawnCount => _nayruLightningSpawnCount;
    internal bool SwordGiftShown => _nayruSwordGiftShown;
    internal Vector2 InitialMoveEnd => _nayruInitialMoveEnd;
    internal bool CollapsedImpaRendered => _nayruCollapsedImpaRendered;
    internal int RalphJumpCount => _nayruRalphJumpCount;
    internal bool DarkPaletteShown => _nayruDarkPaletteShown;
    internal bool AudienceJumpShown => _nayruAudienceJumpShown;
    internal bool VeranReactionMoved => _nayruVeranReactionMoved;
    internal bool PossessionFlashShown => _nayruPossessionFlashShown;
    internal bool RalphSwordShown => _nayruRalphSwordShown;
    internal bool PortalFlightShown => _nayruPortalFlightShown;
    internal bool BoyEscaped => _nayruBoyEscaped;
    internal bool BoyEscapeStarted => _nayruBoyEscapeStarted;
    internal bool GhostTrackingShown => _nayruGhostTrackingShown;
    internal int LinkVeranFacingMask => _nayruLinkVeranFacingMask;
    internal int RalphVeranFacingMask => _nayruRalphVeranFacingMask;
    internal bool BackstepShown => _nayruBackstepShown;
    internal bool GhostHiddenAfterPossession => _nayruGhostHiddenAfterPossession;
    internal bool SwordSpacingShown => _nayruSwordSpacingShown;
    internal bool AftermathLinkWalkShown => _nayruAftermathLinkWalkShown;
    internal bool NoteMotionShown => _nayruNoteMotionMask == 0x03;
    internal bool PossessionBlinkShown => _nayruPossessionBlinkShown;
    internal bool PossessionSwayShown => _nayruPossessionSwayShown;
    internal bool PossessionMovementSyncShown => _nayruPossessionMovementSyncShown;
    internal bool PostChargeFacingShown => _nayruPostChargeFacingShown;
    internal bool GhostEmergenceShown => _nayruGhostEmergenceShown;
    internal bool MovementFacingShown =>
        _nayruMoveFacingMask == (ulong)NayruMoveFacing.All;
    internal bool AftermathRalphFacingShown =>
        (_nayruAftermathRalphFacingMask & 0x07) == 0x07;
    internal bool VignetteDetailShown =>
        _nayruVignetteGirlJumpShown && _nayruVignetteMonkeyHopShown &&
        _nayruVignetteMonkeyPacingShown && _nayruVignetteMonkeyStoneShown &&
        _nayruVignetteMonkeyFlickerShown && _nayruVignetteBoyPaletteShown &&
        _nayruVignetteLadyCadenceShown && _nayruVignetteExclamationCount == 3;

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
        _nayruVisitedVignettes = 0;
        _nayruNoteSpawnCount = 0;
        _nayruLightningSpawnCount = 0;
        _nayruSwordGiftShown = false;
        _nayruInitialMoveEnd = new Vector2(-1, -1);
        _nayruCollapsedImpaRendered = false;
        _nayruGhostRevealFlickerRemaining = 0;
        _nayruRalphJumpCount = 0;
        _nayruRalphSwordAnimation = -1;
        _nayruDarkPaletteShown = false;
        _nayruAudienceJumpShown = false;
        _nayruVeranReactionMoved = false;
        _nayruPossessionFlashShown = false;
        _nayruRalphSwordShown = false;
        _nayruPortalFlightShown = false;
        _nayruBoyEscaped = false;
        _nayruBoyEscapeStarted = false;
        _nayruGhostTrackingShown = false;
        _nayruTrackLinkVeranFacing = false;
        _nayruTrackRalphVeranFacing = false;
        _nayruUpdateVeranFacingTarget = false;
        _nayruVeranFacingTarget = Vector2.Zero;
        _nayruGhostTrackingMask = 0;
        _nayruLinkVeranFacingMask = 0;
        _nayruRalphVeranFacingMask = 0;
        _nayruNayruHeldVeranFacing = false;
        _nayruBackstepShown = false;
        _nayruGhostHiddenAfterPossession = false;
        _nayruSwordSpacingShown = false;
        _nayruAftermathLinkWalkShown = false;
        _nayruNoteMotionMask = 0;
        _nayruPossessionState = null;
        _nayruPossessionPaletteFlips = 0;
        _nayruPossessionBlinkShown = false;
        _nayruPossessionSwayShown = false;
        _nayruPossessionMovementSyncShown = false;
        _nayruPostChargeFacingShown = false;
        _nayruGhostEmergencePhase = 0;
        _nayruGhostEmergenceCounter = 0;
        _nayruGhostEmergenceShown = false;
        _nayruMoveFacingMask = 0;
        _nayruTrackAftermathRalphFacing = false;
        _nayruAftermathRalphFacingMask = 0;
        _nayruVignetteIndex = -1;
        _nayruVignetteElapsed = 0;
        _nayruVignetteExclamationCount = 0;
        _nayruVignetteOldManZ = 0;
        _nayruVignetteOldManSpeedZ = 0;
        _nayruVignetteGirlJumpShown = false;
        _nayruVignetteMonkeyHopShown = false;
        _nayruVignetteMonkeyPacingShown = false;
        _nayruVignetteMonkeyStoneShown = false;
        _nayruVignetteMonkeyFlickerShown = false;
        _nayruVignetteBoyPaletteShown = false;
        _nayruVignetteLadyCadenceShown = false;
        _nayruVignetteMonkeys.Clear();
        _nayruAudienceTalkStates.Clear();
        _nayruStage = NayruStage.Crowd;
        _counter = 0;

        // The positioned bear $5d:$02 and portal-departure Ralph $37:$0d are
        // later story variants. $6b:$01 owns the intro actors instead.
        foreach (NpcCharacter npc in _entities.Entities<NpcCharacter>())
        {
            if ((npc.Record.Id == 0x5d && npc.Record.SubId == 0x02) ||
                (npc.Record.Id == 0x37 && npc.Record.SubId == 0x0d))
                npc.SetActive(false);
        }

        NpcCharacter nayru = SpawnNayruActor("Nayru", "Nayru", solid: true);
        NayruIntroEventDatabase.ActorRecord nayruRecord = _nayruDatabase.Actor("Nayru");
        nayru.SetScriptAnimation(nayruRecord.Animation(nayruRecord.InitialAnimation));
        NpcCharacter ralph = SpawnNayruActor("Ralph", "Ralph", solid: true);
        NayruIntroEventDatabase.ActorRecord ralphRecord = _nayruDatabase.Actor("Ralph");
        ralph.SetScriptAnimation(ralphRecord.Animation(ralphRecord.InitialAnimation));
        NpcCharacter bear = SpawnAudienceActor("Bear", 0x5702);
        if (!_rooms.SaveData.HasRoomFlag(
            _nayruRecord.Group, _nayruRecord.Room, (byte)_nayruRecord.BearRoomFlag))
            bear.Position += Vector2.Down * 16.0f;
        SpawnAudienceActor("Monkey", 0x5704);
        SpawnAudienceActor("Rabbit", 0x5705);
        SpawnAudienceActor("Boy", 0x2510);
        SpawnAudienceActor("Bird", 0x3214);
    }

    private NpcCharacter SpawnAudienceActor(string name, int textId)
    {
        NayruIntroEventDatabase.ActorRecord actor = _nayruDatabase.Actor(name);
        NayruIntroEventDatabase.TextRecord text = _nayruDatabase.Text(textId);
        NpcDatabase.NpcRecord record = actor.ToNpcRecord(_nayruRecord.Group, _nayruRecord.Room) with
        {
            TextId = textId,
            Message = text.Message
        };
        NpcCharacter npc = _entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                record, $"NayruIntro_{name}", Talkable: true, Solid: true));
        if (!string.IsNullOrEmpty(actor.ExtraSprite))
            npc.AppendScriptGraphics(actor.ExtraSprite);
        npc.SetScriptAnimation(actor.Animation(actor.InitialAnimation));
        _nayruActors.Add(name, npc);
        return npc;
    }

    private NpcCharacter SpawnNayruActor(
        string template,
        string name,
        Vector2? position = null,
        bool solid = false)
    {
        NayruIntroEventDatabase.ActorRecord actor = _nayruDatabase.Actor(template);
        NpcCharacter npc = _entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            actor.ToNpcRecord(_rooms.ActiveGroup, _rooms.CurrentRoom.Id),
            $"NayruIntro_{name}", Solid: solid));
        if (!string.IsNullOrEmpty(actor.ExtraSprite))
            npc.AppendScriptGraphics(actor.ExtraSprite);
        if (position.HasValue)
            npc.Position = position.Value;
        _nayruActors[name] = npc;
        return npc;
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_nayruStage != NayruStage.Crowd)
            return false;
        string? name = null;
        foreach ((string actorName, NpcCharacter actor) in _nayruActors)
        {
            if (actor == npc)
            {
                name = actorName;
                break;
            }
        }
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
        bearSetAnimation(1);
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
                SetNayruAnimation(name, _player.Position.X <= npc.Position.X ? 0 : 1);
                resetAnimation = 2;
                resetDelay = 20;
                break;
            case "Bird":
                // birdScript_listeningToNayruGameStart adds two to cplinkx's
                // result and repeatedly applies -$00c0/$0020 Z physics while
                // the textbox is active.
                SetNayruAnimation(name, _player.Position.X <= npc.Position.X ? 2 : 3);
                resetAnimation = 1;
                resetDelay = 10;
                hopping = true;
                break;
            default:
                // The rabbit and boy call scriptHelp.turnToFaceLink, whose
                // diagonal ties use convertAngleToDirection's clockwise round.
                Vector2I facing = FacingForTrackedTarget(
                    OriginalPixelPosition(_player.Position) - OriginalPixelPosition(npc.Position));
                SetNayruAnimation(name, AnimationForFacing(facing));
                resetAnimation = 0;
                resetDelay = 10;
                break;
        }
        _nayruAudienceTalkStates.Add(new NayruAudienceTalkState(
            name, resetAnimation, resetDelay, hopping));
    }

    public void UpdateFrame()
    {
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
                if (!_dialogue.IsOpen &&
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
                    bearSetAnimation(0);
                    _rooms.SaveData.SetRoomFlag(
                        _nayruRecord.Group, _nayruRecord.Room,
                        (byte)_nayruRecord.BearRoomFlag);
                    ShowNayruText(0x5703);
                    _nayruStage = NayruStage.BearText;
                }
                break;
            case NayruStage.BearText:
                if (!_dialogue.IsOpen)
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
                if (!_dialogue.IsOpen)
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
                UpdateNayruCommand();
                break;
        }
    }

    private void UpdateNayruAudienceTalks()
    {
        for (int index = _nayruAudienceTalkStates.Count - 1; index >= 0; index--)
        {
            NayruAudienceTalkState state = _nayruAudienceTalkStates[index];
            if (!_nayruActors.TryGetValue(state.Actor, out NpcCharacter? actor) || !actor.Active)
            {
                _nayruAudienceTalkStates.RemoveAt(index);
                continue;
            }

            if (state.WaitingForText)
            {
                if (state.Hopping)
                    UpdateNayruTalkingBirdHop(state, actor);
                if (_dialogue.IsOpen)
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
            SetNayruAnimation(state.Actor, state.ResetAnimation);
            _nayruAudienceTalkStates.RemoveAt(index);
        }
    }

    private static void UpdateNayruTalkingBirdHop(
        NayruAudienceTalkState state,
        NpcCharacter bird)
    {
        state.ZFixed += state.SpeedZ;
        if (state.ZFixed < 0)
        {
            bird.SetScriptDrawOffset(new Vector2(0, state.ZFixed / 256.0f));
            state.SpeedZ += 0x20;
            return;
        }

        state.ZFixed = 0;
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
        _counter = (int)InventoryMenuController.FastFadeFrames;
        _nayruStage = NayruStage.SingingFadeOut;
    }

    private void BuildNayruScript()
    {
        _nayruCommands.Clear();
        _nayruCommand = null;
        Callback(SetupNayruPossessionScene);
        Fade(11, fadeIn: true);

        Wait(30); Jump("Ralph"); Wait(30); Text(0x2a00); Wait(30);
        Callback(() => _player.Face(Vector2I.Up));
        Animation("Nayru", 2); Wait(10);
        NpcMove("Nayru", Vector2.Down * 8, 32, NayruMoveFacing.NayruApproach);
        Wait(30); Text(0x1d00); Wait(30);
        Callback(() => _player.Face(Vector2I.Right)); Jump("Ralph");
        Wait(10); Text(0x2a22); Wait(30);
        Wait(40); Callback(() => _player.Face(Vector2I.Up)); Text(0x1d22); Wait(30);

        Animation("Impa", 2); Wait(30); Wait(30);
        NpcMove("Impa", Vector2.Right * 32, 32, NayruMoveFacing.ImpaRight); Wait(8);
        NpcMove("Impa", Vector2.Up * 16, 16, NayruMoveFacing.ImpaUp); Wait(30);
        Animation("Impa", 4); Wait(240); Text(0x5600);
        Callback(() => _player.Face(Vector2I.Down));
        Callback(AlarmNayruAudience); Wait(60); Animation("Impa", 0);
        Wait(60); Text(0x5606); Wait(10); Animation("Impa", 7);
        PoseMove(
            "Impa", AngleVector(0x16) * 36.0f, 72, 7,
            NayruMoveFacing.ImpaSpin);
        Callback(SpawnGhostVeran); RoomPalette(_nayruRecord.DarkFadeFrames);
        Callback(BeginNayruAudienceEscape); Wait(58);

        Move("GhostVeran", Vector2.Up * 22.5f, 90); Wait(60);
        Animation("Ralph", 2);
        ParallelMove("Player", Vector2.Left * 33, "Ralph", Vector2.Down * 33, 22);
        Wait(6); MovePlayer(Vector2.Down * 12, 8);
        Wait(84);
        Move("GhostVeran", AngleVector(0x1c) * 68, 17); Wait(8);
        Move("GhostVeran", AngleVector(0x0b) * 148, 37); Wait(8);
        Move("GhostVeran", AngleVector(0x18) * 76, 19); Wait(8);
        Move("GhostVeran", AngleVector(0x02) * 100, 25); Wait(8);
        Move("GhostVeran", AngleVector(0x0a) * 48, 12); Wait(8);
        Move("GhostVeran", AngleVector(0x14) * 68, 17); Wait(30);

        Callback(SpawnHumanVeran); Flicker("GhostVeran", 120); Wait(120);
        Animation("HumanVeran", 1); Wait(30); Text(0x5601); Wait(30);
        Animation("HumanVeran", 0); Wait(60); Flicker("GhostVeran", 120);
        Callback(() => HideNayruActor("HumanVeran")); Wait(30);
        Move("GhostVeran", AngleVector(0x0b) * 40, 80); Wait(30);
        Text(0x5602); Wait(30); Wait(120);
        Move("GhostVeran", Vector2.Down * 10.25f, 41); Wait(60);
        Callback(BeginGhostCharge);
        ParallelMove("GhostVeran", Vector2.Up * 102, 34, "Nayru", Vector2.Up * 8, 32);
        Callback(FinishGhostCharge);
        Fade(_nayruRecord.WhiteFadeOutFrames, fadeIn: false);
        Callback(() => _nayruPossessionFlashShown = _nayruFade.Color.A >= 0.99f);
        Wait(_nayruRecord.PossessionFadeHoldFrames);
        Callback(HideGhostVeranAfterPossession);
        Callback(BeginNayruPossessionRecovery);
        Fade(_nayruRecord.WhiteFadeInFrames, fadeIn: true);
        Wait(549 - _nayruRecord.WhiteFadeInFrames);
        Wait(120);
        NpcMove("Ralph", Vector2.Left * 16, 16, NayruMoveFacing.RalphSwordLeft); Wait(6);
        Callback(SpawnRalphSword);
        NpcMove("Ralph", Vector2.Up * 24, 24, NayruMoveFacing.RalphSwordUp);
        Wait(30); Animation("Ralph", 4);
        Wait(60); Text(0x2a01); Wait(30); Text(0x5603); Wait(60);
        Animation("Ralph", 0);
        PoseMove(
            "Ralph", Vector2.Down * 16, 129, 0,
            NayruMoveFacing.RalphSecondRetreat);
        Wait(30); Text(0x5604); Wait(60);
        Callback(() => SpawnNayruLightning(new Vector2(0x28, 0x24)));
        Wait(2); Callback(ActivateNayruPortal); Wait(1);
        Wait(60);
        Move("GhostVeran", Vector2.Down * 17.5f, 35); Wait(10);
        Callback(() => HideNayruActor("GhostVeran"));
        Wait(60); PortalFlight("Nayru"); Wait(20);
        NpcMove("Ralph", Vector2.Up * 48, 48, NayruMoveFacing.RalphPortalUp);
        Wait(6);
        NpcMove("Ralph", Vector2.Left * 49, 49, NayruMoveFacing.RalphPortalLeft);
        Wait(40); Text(0x5605); Wait(60);
        NpcMove("Nayru", Vector2.Up * 17, 17, NayruMoveFacing.NayruPortalUp);
        Flicker("Nayru", 120); Callback(() => HideNayruActor("Nayru"));
        Wait(120); Wait(90); Text(0x5607); Wait(90);

        Fade(11, fadeIn: false); Callback(() => BeginNayruVignette(0));
        Fade(11, fadeIn: true); BuildNayruVignetteZero();
        Fade(11, fadeIn: false); Callback(() => BeginNayruVignette(1));
        Fade(11, fadeIn: true); BuildNayruVignetteOne();
        Fade(11, fadeIn: false); Callback(() => BeginNayruVignette(2));
        Fade(11, fadeIn: true); BuildNayruVignetteTwo();
        Fade(11, fadeIn: false); Callback(BeginNayruAftermath);
        Fade(11, fadeIn: true);

        Wait(120); Text(0x2a02); Wait(30);
        PoseMove(
            "AftermathRalph", Vector2.Right * 16, 129, 9,
            NayruMoveFacing.AftermathRalphStaggerRight);
        Animation("AftermathRalph", 8);
        Wait(120); Text(0x2a03); Wait(120); Animation("AftermathRalph", 9);
        Wait(10); Animation("AftermathRalph", 10); Wait(60);
        PoseMove(
            "AftermathRalph", Vector2.Left * 17, 102, 10,
            NayruMoveFacing.AftermathRalphCliffLeft);
        Wait(30);
        Text(0x2a04); Wait(120); Wait(60); Animation("AftermathRalph", 2);
        Text(0x2a05); Wait(30);
        NpcMove(
            "AftermathRalph", Vector2.Right * 50, 25,
            NayruMoveFacing.AftermathRalphRight);
        Animation("AftermathRalph", 2);
        Wait(120); Text(0x2a06); Wait(30);
        NpcMove(
            "AftermathRalph", Vector2.Down * 120, 40,
            NayruMoveFacing.AftermathRalphDown);
        Wait(60); Callback(FinishAftermathRalphDeparture);

        Wait(80); MovePlayer(Vector2.Down * 48, 48); Wait(8);
        MovePlayer(Vector2.Left * 16, 16); Wait(60); Wait(120);
        Callback(RestoreAftermathImpa); Wait(60); Animation("AftermathImpa", 3);
        Wait(50); Animation("AftermathImpa", 1); Wait(30);
        Animation("AftermathImpa", 3); Wait(10); Animation("AftermathImpa", 1);
        Wait(60); Text(0x0110); Wait(30); Animation("AftermathImpa", 3);
        Wait(30); Text(0x0112); Wait(30); Animation("AftermathImpa", 1);
        Text(0x0115); Wait(30);
        Callback(BeginNayruSwordGift);
        Callback(GrantNayruSword); Text(0x001c); Callback(RemoveNayruSwordEffect);
        Wait(30); Callback(() => _player.Face(Vector2I.Left));
        Wait(30); Text(0x0117); Wait(30);
        NpcMove(
            "AftermathImpa", Vector2.Right * 65, 65,
            NayruMoveFacing.AftermathImpaRight);
        Wait(8);
        NpcMove(
            "AftermathImpa", Vector2.Down * 33, 33,
            NayruMoveFacing.AftermathImpaDown);
        Wait(60);
        Callback(FinishNayruIntro);
    }

    private void BuildNayruVignetteZero()
    {
        Wait(_nayruDatabase.Vignette(0).Duration - 11);
    }

    private void BuildNayruVignetteOne()
    {
        Wait(_nayruDatabase.Vignette(1).Duration - 11);
    }

    private void BuildNayruVignetteTwo()
    {
        Wait(_nayruDatabase.Vignette(2).Duration - 11);
    }

    private void UpdateNayruCommand()
    {
        if (_nayruCommand is null)
        {
            if (_nayruCommands.Count == 0)
            {
                FinishNayruIntro();
                return;
            }
            _nayruCommand = _nayruCommands.Dequeue();
            _nayruCommand.Counter = Math.Max(
                1, Math.Max(_nayruCommand.Frames, _nayruCommand.Frames2));
        }
        NayruCommand command = _nayruCommand;
        bool finished = command.Kind switch
        {
            NayruCommandKind.Wait => --command.Counter == 0,
            NayruCommandKind.Text => UpdateNayruTextCommand(command),
            NayruCommandKind.Move => UpdateNayruMoveCommand(command),
            NayruCommandKind.ParallelMove => UpdateNayruParallelMoveCommand(command),
            NayruCommandKind.Jump => UpdateNayruJumpCommand(command),
            NayruCommandKind.PortalFlight => UpdateNayruPortalFlightCommand(command),
            NayruCommandKind.RoomPalette => UpdateNayruRoomPaletteCommand(command),
            NayruCommandKind.Animation => UpdateNayruAnimationCommand(command),
            NayruCommandKind.Callback => UpdateNayruCallbackCommand(command),
            NayruCommandKind.Fade => UpdateNayruFadeCommand(command),
            NayruCommandKind.Flicker => UpdateNayruFlickerCommand(command),
            NayruCommandKind.PaletteFlicker => UpdateNayruPaletteFlickerCommand(command),
            _ => true
        };
        if (finished)
            _nayruCommand = null;
    }

    private bool UpdateNayruTextCommand(NayruCommand command)
    {
        if (!command.Started)
        {
            command.Started = true;
            ShowNayruText(command.TextId);
            return false;
        }
        return !_dialogue.IsOpen;
    }

    private bool UpdateNayruMoveCommand(NayruCommand command)
    {
        if (!command.Started)
        {
            command.Started = true;
            if (command.Value >= 0 && command.Actor != "Player")
            {
                if (command.SetFacingOnStart)
                    SetNayruAnimation(command.Actor, command.Value);
                if (_nayruActors.TryGetValue(command.Actor, out NpcCharacter? actor) &&
                    actor.CurrentScriptAnimationSource ==
                        NayruAnimationSource(command.Actor, command.Value))
                {
                    _nayruMoveFacingMask |= command.FacingAuditBit;
                }
            }
            command.StartPosition = command.Actor == "Player"
                ? _player.Position
                : _nayruActors[command.Actor].Position;
        }
        int elapsed = command.Frames - command.Counter + 1;
        Vector2 position = command.StartPosition + command.Delta * elapsed / command.Frames;
        if (command.Actor == "Player")
        {
            _player.AdvanceCutsceneMovement(
                command.Delta / command.Frames, FacingForDelta(command.Delta));
            if (_nayruVisitedVignettes == 0x07)
                _nayruAftermathLinkWalkShown |= _player.Walking;
        }
        else
            _nayruActors[command.Actor].Position = position;
        bool finished = --command.Counter == 0;
        if (finished && command.Actor == "Player")
        {
            _player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Zero);
            if (_nayruVisitedVignettes == 0 &&
                command.StartPosition == new Vector2(0x57, 0x30) &&
                command.Delta == Vector2.Down * 12)
            {
                // linkCutscene3 enters substate 8 only after its 22-update
                // left move, six-update hold, and eight-update down move.
                _nayruTrackLinkVeranFacing = true;
            }
        }
        if (finished && command.Actor == "Nayru" &&
            command.StartPosition == new Vector2(0x78, 0x18) &&
            command.Delta == Vector2.Down * 8)
        {
            _nayruInitialMoveEnd = position;
        }
        return finished;
    }

    private bool UpdateNayruParallelMoveCommand(NayruCommand command)
    {
        if (!command.Started)
        {
            command.Started = true;
            command.StartPosition = ActorPosition(command.Actor);
            command.StartPosition2 = ActorPosition(command.Actor2);
        }
        int totalFrames = Math.Max(command.Frames, command.Frames2);
        int elapsed = totalFrames - command.Counter + 1;
        int frames2 = command.Frames2 > 0 ? command.Frames2 : command.Frames;
        int elapsed1 = Math.Min(elapsed, command.Frames);
        int elapsed2 = Math.Min(elapsed, frames2);
        SetActorPosition(
            command.Actor,
            command.StartPosition + command.Delta * elapsed1 / command.Frames,
            command.Delta,
            elapsed <= command.Frames ? command.Delta / command.Frames : Vector2.Zero);
        SetActorPosition(
            command.Actor2,
            command.StartPosition2 + command.Delta2 * elapsed2 / frames2,
            command.Delta2,
            elapsed <= frames2 ? command.Delta2 / frames2 : Vector2.Zero);
        bool finished = --command.Counter == 0;
        if (finished && (command.Actor == "Player" || command.Actor2 == "Player"))
            _player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Zero);
        if (finished &&
            ((command.Actor == "Player" && command.Actor2 == "Ralph") ||
             (command.Actor == "Ralph" && command.Actor2 == "Player")))
        {
            _nayruVeranReactionMoved =
                _player.Position == new Vector2(0x57, 0x30) &&
                _nayruActors["Ralph"].Position == new Vector2(0x88, 0x51);
            // Ralph reaches @faceVeranGhost as soon as movedown $16 ends,
            // fourteen updates before Link completes his own reaction.
            _nayruTrackRalphVeranFacing = true;
        }
        if (finished &&
            ((command.Actor == "Nayru" && command.Delta == Vector2.Up * 8) ||
             (command.Actor2 == "Nayru" && command.Delta2 == Vector2.Up * 8)))
        {
            _nayruBackstepShown =
                _nayruActors["Nayru"].Position == new Vector2(0x78, 0x18) &&
                _nayruActors["Nayru"].CurrentScriptAnimationSource ==
                    _nayruDatabase.Actor("Nayru").Animation(2);
        }
        return finished;
    }

    private bool UpdateNayruJumpCommand(NayruCommand command)
    {
        NpcCharacter actor = _nayruActors[command.Actor];
        if (!command.Started)
        {
            command.Started = true;
            command.ZFixed = 0;
            command.SpeedZ = _nayruRecord.NpcJumpSpeedZ;
            _nayruRalphJumpCount++;
        }
        command.ZFixed += command.SpeedZ;
        if (command.ZFixed < 0)
        {
            actor.SetScriptDrawOffset(new Vector2(0, command.ZFixed / 256.0f));
            command.SpeedZ += _nayruRecord.NpcJumpGravity;
            return false;
        }
        actor.SetScriptDrawOffset(Vector2.Zero);
        return true;
    }

    private bool UpdateNayruPortalFlightCommand(NayruCommand command)
    {
        NpcCharacter nayru = _nayruActors[command.Actor];
        if (!command.Started)
        {
            command.Started = true;
            command.SpeedZ = _nayruRecord.NayruAscentSpeedZ;
            command.ZFixed = 0;
            command.Phase = 0;
            SetNayruAnimation(command.Actor, 5);
        }

        if (command.Phase == 0)
        {
            command.ZFixed += command.SpeedZ;
            nayru.SetScriptDrawOffset(new Vector2(0, command.ZFixed / 256.0f));
            if (command.ZFixed > _nayruRecord.NayruTransferZ - 0x400)
                return false;
            nayru.Position = new Vector2(0x28, 0x38);
            command.ZFixed = _nayruRecord.NayruTransferZ;
            nayru.SetScriptDrawOffset(new Vector2(0, command.ZFixed / 256.0f));
            command.Counter = _nayruRecord.NayruLandingDelay;
            command.Phase = 1;
            return false;
        }
        if (command.Phase == 1)
        {
            if (--command.Counter > 0)
                return false;
            command.SpeedZ = _nayruRecord.NayruFallSpeedZ;
            command.Phase = 2;
        }

        command.ZFixed += command.SpeedZ;
        if (command.ZFixed < 0)
        {
            nayru.SetScriptDrawOffset(new Vector2(0, command.ZFixed / 256.0f));
            command.SpeedZ += _nayruRecord.NayruFallGravity;
            return false;
        }
        nayru.SetScriptDrawOffset(Vector2.Zero);
        SetNayruAnimation(command.Actor, 2);
        _nayruPortalFlightShown = nayru.Position == new Vector2(0x28, 0x38);
        return true;
    }

    private bool UpdateNayruRoomPaletteCommand(NayruCommand command)
    {
        int elapsed = command.Frames - command.Counter + 1;
        float blend = Mathf.Min(16, (elapsed + 1) / 2) / 16.0f;
        _nayruRoom!.SetTemporaryBackgroundPalette(
            _nayruDatabase.DarkBackgroundPalettes, blend);
        bool finished = --command.Counter == 0;
        if (finished)
            _nayruDarkPaletteShown =
                _nayruRoom.TemporaryBackgroundPaletteBlend >= 1.0f;
        return finished;
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
            if (_nayruVisitedVignettes == 0x07)
                _nayruAftermathLinkWalkShown |= _player.Walking;
        }
        else
        {
            _nayruActors[actor].Position = position;
        }
    }

    private static Vector2I FacingForDelta(Vector2 delta) =>
        Mathf.Abs(delta.X) > Mathf.Abs(delta.Y)
            ? (delta.X > 0 ? Vector2I.Right : Vector2I.Left)
            : (delta.Y > 0 ? Vector2I.Down : Vector2I.Up);

    private static Vector2 OriginalPixelPosition(Vector2 position) =>
        new(Mathf.Floor(position.X), Mathf.Floor(position.Y));

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

    private bool UpdateNayruAnimationCommand(NayruCommand command)
    {
        SetNayruAnimation(command.Actor, command.Value);
        return true;
    }

    private static bool UpdateNayruCallbackCommand(NayruCommand command)
    {
        command.Callback!();
        return true;
    }

    private bool UpdateNayruFadeCommand(NayruCommand command)
    {
        if (!command.Started)
        {
            command.Started = true;
            command.StartAlpha = _nayruFade.Color.A;
        }
        float progress = (command.Frames - command.Counter + 1.0f) / command.Frames;
        _nayruFade.Color = new Color(
            command.FadeColor.R,
            command.FadeColor.G,
            command.FadeColor.B,
            Mathf.Lerp(command.StartAlpha, command.TargetAlpha, progress));
        return --command.Counter == 0;
    }

    private bool UpdateNayruFlickerCommand(NayruCommand command)
    {
        if (_nayruActors.TryGetValue(command.Actor, out NpcCharacter? actor))
            actor.Visible = (_entities.FrameCounter & 1) != 0;
        if (--command.Counter != 0)
            return false;
        if (actor is not null)
            actor.Visible = actor.Active;
        return true;
    }

    private bool UpdateNayruPaletteFlickerCommand(NayruCommand command)
    {
        if (_nayruActors.TryGetValue(command.Actor, out NpcCharacter? actor))
        {
            actor.Modulate = ((command.Frames - command.Counter) & 8) == 0
                ? Colors.White
                : new Color(0.65f, 0.65f, 0.65f, 1.0f);
        }
        if (--command.Counter != 0)
            return false;
        if (actor is not null)
            actor.Modulate = Colors.White;
        return true;
    }

    private void SetupNayruPossessionScene()
    {
        _player.WarpTo(new Vector2(0x78, 0x30), recordSafe: false);
        _player.Face(Vector2I.Right);
        _nayruActors["Nayru"].Position = new Vector2(0x78, 0x18);
        SetNayruAnimation("Nayru", 2);
        _nayruActors["Nayru"].SetScriptPaletteOverride(null);
        _nayruActors["Ralph"].Position = new Vector2(0x88, 0x30);
        SetNayruAnimation("Ralph", 3);
        if (_impa is null || !_impa.Active)
        {
            _impa = SpawnNayruActor("AftermathImpa", "Impa", new Vector2(0x38, 0x68));
            _impa.SetSpritePalette(_impaDatabase.PossessedPalette);
        }
        else
        {
            _nayruActors["Impa"] = _impa;
            _impa.Position = new Vector2(0x38, 0x68);
        }
        _impa.SetBlocksLink(false);
    }

    private void AlarmNayruAudience()
    {
        SetNayruAnimation("Bear", 2);
        SetNayruAnimation("Monkey", 6);
        SetNayruAnimation("Rabbit", 2);
        SetNayruAnimation("Boy", 2);
        // boyRunSubid00 calls interactionAnimate once before its substate
        // dispatch and a second time in shocked substate $01.
        _nayruActors["Boy"].SetAnimationRate(2.0f);
        SetNayruAnimation("Bird", 1);
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
        SetNayruAnimation(actor, record.WaitAnimation);
        _nayruFleeingAudience.Add(new FleeingAudience(
            _nayruActors[actor], record, AngleVector(record.Angle) * record.Speed));
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
            if (!WithinOriginalScreenBoundary(fleeing.Actor.Position))
            {
                fleeing.Actor.SetActive(false);
                if (fleeing.Record.Actor == "Boy")
                    _nayruBoyEscaped = true;
            }
        }
    }

    private void BeginAudienceEscape(FleeingAudience fleeing)
    {
        fleeing.Escaping = true;
        fleeing.ZFixed = 0;
        fleeing.SpeedZ = fleeing.Record.EscapeJumpSpeedZ;
        fleeing.Actor.SetScriptDrawOffset(Vector2.Zero);
        SetNayruAnimation(fleeing.Record.Actor, fleeing.Record.EscapeAnimation);
        if (fleeing.Record.Actor == "Boy")
            _nayruBoyEscapeStarted = true;
    }

    private bool UpdateAudienceJump(
        FleeingAudience fleeing,
        int initialSpeedZ,
        int gravity,
        bool repeat)
    {
        if (initialSpeedZ == 0)
            return true;
        fleeing.ZFixed += fleeing.SpeedZ;
        if (fleeing.ZFixed < 0)
        {
            fleeing.Actor.SetScriptDrawOffset(new Vector2(0, fleeing.ZFixed / 256.0f));
            fleeing.SpeedZ += gravity;
            _nayruAudienceJumpShown = true;
            return false;
        }
        fleeing.ZFixed = 0;
        fleeing.Actor.SetScriptDrawOffset(Vector2.Zero);
        fleeing.SpeedZ = repeat ? initialSpeedZ : 0;
        return true;
    }

    private void UpdateNayruSingingNotes()
    {
        if (_nayruStage is < NayruStage.Crowd or > NayruStage.TriggerPostText ||
            !_nayruActors.TryGetValue("Nayru", out NpcCharacter? nayru) ||
            !nayru.Active)
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
            effect.Actor.Position += effect.Velocity;
            if (effect.Sway && (_entities.FrameCounter & 7) == 0)
                effect.Actor.Position += Vector2.Right *
                    swaySteps[(_entities.FrameCounter >> 3) & 7];
            if (effect.MusicNote && effect.Actor.Position.Y < effect.SpawnPosition.Y)
            {
                if (effect.FloatsLeft && effect.Actor.Position.X < effect.SpawnPosition.X)
                    _nayruNoteMotionMask |= 0x01;
                if (!effect.FloatsLeft && effect.Actor.Position.X > effect.SpawnPosition.X)
                    _nayruNoteMotionMask |= 0x02;
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

        if (_nayruGhostRevealFlickerRemaining > 0 &&
            _nayruActors.TryGetValue("GhostVeran", out NpcCharacter? ghost) && ghost.Active)
        {
            ghost.Visible = (_entities.FrameCounter & 1) != 0;
            _nayruGhostRevealFlickerRemaining--;
            if (_nayruGhostRevealFlickerRemaining == 0)
                ghost.Visible = true;
        }

        UpdateGhostVeranEmergence();
        UpdateNayruPossessionRecovery();

        // runVeranGhostSubid0 writes its integer YX position to cfd5/cfd6
        // before advancing the flight script. Link and Ralph read that cached
        // position independently; neither faces the live object directly.
        if (_nayruUpdateVeranFacingTarget &&
            _nayruActors.TryGetValue("GhostVeran", out NpcCharacter? trackedGhost) &&
            trackedGhost.Active)
        {
            _nayruVeranFacingTarget = OriginalPixelPosition(trackedGhost.Position);
            if (_nayruActors.TryGetValue("Nayru", out NpcCharacter? watchingNayru) &&
                watchingNayru.CurrentScriptAnimationSource !=
                    _nayruDatabase.Actor("Nayru").Animation(2))
            {
                _nayruNayruHeldVeranFacing = false;
            }
        }
        if (_nayruTrackLinkVeranFacing && (_entities.FrameCounter & 7) == 0)
        {
            Vector2I facing = FacingForTrackedTarget(
                _nayruVeranFacingTarget - OriginalPixelPosition(_player.Position));
            if (facing != Vector2I.Zero)
            {
                _player.Face(facing);
                _nayruGhostTrackingMask |= 0x01;
                _nayruLinkVeranFacingMask |= DirectionMask(facing);
            }
            if (!_nayruUpdateVeranFacingTarget)
                _nayruGhostTrackingMask |= 0x08;
        }
        if (_nayruTrackRalphVeranFacing && (_entities.FrameCounter & 15) == 0 &&
            _nayruActors.TryGetValue("Ralph", out NpcCharacter? trackingRalph) &&
            trackingRalph.Active)
        {
            Vector2I facing = FacingForTrackedTarget(
                _nayruVeranFacingTarget - OriginalPixelPosition(trackingRalph.Position));
            if (facing != Vector2I.Zero)
            {
                SetNayruAnimation("Ralph", AnimationForFacing(facing));
                _nayruGhostTrackingMask |= 0x02;
                _nayruRalphVeranFacingMask |= DirectionMask(facing);
            }
        }
        _nayruGhostTrackingShown |= _nayruGhostTrackingMask == 0x3f;

        // linkCutscene4 reads Ralph's cfd5/cfd6 position every eight updates
        // until his subid $02 script signals cfd0=$20 and deletes itself.
        if (_nayruTrackAftermathRalphFacing &&
            (_entities.FrameCounter & 7) == 0 &&
            _nayruActors.TryGetValue("AftermathRalph", out NpcCharacter? aftermathRalph) &&
            aftermathRalph.Active)
        {
            Vector2I facing = FacingForTrackedTarget(
                OriginalPixelPosition(aftermathRalph.Position) -
                OriginalPixelPosition(_player.Position));
            _player.Face(facing);
            if (facing == Vector2I.Up)
                _nayruAftermathRalphFacingMask |= 0x01;
            else if (facing == Vector2I.Right)
                _nayruAftermathRalphFacingMask |= 0x02;
            else if (facing == Vector2I.Down)
                _nayruAftermathRalphFacingMask |= 0x04;
        }

    }

    private void UpdateNayruVignette()
    {
        if (_nayruVignetteIndex < 0)
            return;
        _nayruVignetteElapsed++;
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
            _nayruVignetteGirlJumpShown |= z < 0;
        }
        else if (frame == 756)
        {
            girl.SetScriptDrawOffset(Vector2.Zero);
            girl.SetAnimationRate(2.0f);
        }
        if (frame == 846)
            SetNayruAnimationIfChanged("VignetteGirl", 0);
        if (frame is >= 876 and <= 937)
        {
            if (frame == 876)
            {
                SetNayruAnimationIfChanged("VignetteGirl", 0);
                _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteGirlUp;
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
            }
            _nayruVignetteOldManZ += _nayruVignetteOldManSpeedZ;
            if (_nayruVignetteOldManZ < 0)
            {
                oldMan.SetScriptDrawOffset(
                    new Vector2(0, _nayruVignetteOldManZ / 256.0f));
                _nayruVignetteOldManSpeedZ += 0x30;
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
                _nayruVignetteMonkeyStoneShown = true;
            }
            if (!state.Stone)
                continue;

            int flickerFrame = stoneFrame + 60;
            if (frame >= flickerFrame)
            {
                state.Actor.Visible = (_entities.FrameCounter & 1) != 0;
                _nayruVignetteMonkeyFlickerShown = true;
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
        state.ZFixed += state.SpeedZ;
        if (state.ZFixed < 0)
        {
            state.Actor.SetScriptDrawOffset(new Vector2(0, state.ZFixed / 256.0f));
            state.SpeedZ += 0x10;
            _nayruVignetteMonkeyHopShown = true;
            return;
        }
        state.ZFixed = 0;
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
            SetNayruAnimationIfChanged($"VignetteMonkey{state.Record.Index}", state.Animation);
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
                state.ZFixed += state.SpeedZ;
                if (state.ZFixed < 0)
                {
                    state.Actor.SetScriptDrawOffset(
                        new Vector2(0, state.ZFixed / 256.0f));
                    state.SpeedZ += 0x20;
                    state.Actor.Position += Vector2.Right * state.Direction;
                    _nayruVignetteMonkeyHopShown = true;
                    return;
                }
                state.Actor.SetScriptDrawOffset(Vector2.Zero);
                state.ZFixed = 0;
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
                    _nayruVignetteMonkeyPacingShown = true;
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
        SetNayruAnimationIfChanged($"VignetteMonkey{state.Record.Index}", animation);
    }

    private void UpdateNayruStoneChildVignette()
    {
        int frame = _nayruVignetteElapsed;
        if (!_nayruActors.TryGetValue("VignetteBoy", out NpcCharacter? boy) ||
            !_nayruActors.TryGetValue("VignetteLady", out NpcCharacter? lady))
            return;

        if (frame == 1)
        {
            SetNayruAnimationIfChanged("VignetteBoy", 3);
            boy.SetAnimationRate(2.0f);
            _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteBoyLeft1;
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
                SetNayruAnimationIfChanged("VignetteBoy", 1);
                boy.SetAnimationRate(2.0f);
                _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteBoyRight;
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
                SetNayruAnimationIfChanged("VignetteBoy", 3);
                boy.SetAnimationRate(2.0f);
                _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteBoyLeft2;
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
            _nayruVignetteBoyPaletteShown |= stone;
        }
        if (frame == 365)
        {
            boy.SetScriptPaletteOverride(_nayruDatabase.StoneSpritePalette);
            SetNayruAnimationIfChanged("VignetteBoy", 3);
            _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteBoyStoneLeft;
        }

        if (frame == 459)
        {
            SetNayruAnimationIfChanged("VignetteLady", 2);
            lady.SetAnimationRate(3.0f);
            _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteLadyDown;
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
                SetNayruAnimationIfChanged("VignetteLady", 3);
                lady.SetAnimationRate(3.0f);
                _nayruMoveFacingMask |= (ulong)NayruMoveFacing.VignetteLadyLeft;
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
            _nayruVignetteLadyCadenceShown = true;
        }
        else if (frame >= 586)
        {
            lady.SetAnimationRate(0.0f);
        }
    }

    private void SetNayruAnimationIfChanged(string actorName, int animation)
    {
        if (!_nayruActors.TryGetValue(actorName, out NpcCharacter? actor) || !actor.Active)
            return;
        string source = NayruAnimationSource(actorName, animation);
        if (actor.CurrentScriptAnimationSource != source)
            actor.SetScriptAnimation(source);
    }

    private void SpawnNayruExclamation(Vector2 position, int duration)
    {
        string name = $"VignetteExclamation{_nayruVignetteExclamationCount}";
        NpcCharacter actor = SpawnNayruActor("Exclamation", name, position);
        NayruIntroEventDatabase.ActorRecord record = _nayruDatabase.Actor("Exclamation");
        actor.SetScriptAnimation(record.Animation(record.InitialAnimation));
        actor.SetAnimationRate(1.0f);
        _nayruEffects.Add(new TimedNayruEffect(
            actor, duration, Vector2.Zero, false, false, false, position));
        _nayruVignetteExclamationCount++;
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
            position));
        return actor;
    }

    private void SpawnNayruLightning(Vector2 position)
    {
        SpawnNayruEffect("Lightning", position, $"Lightning{_nayruLightningSpawnCount}");
        _nayruLightningSpawnCount++;
    }

    private void SpawnGhostVeran()
    {
        Vector2 position = _nayruActors["Impa"].Position;
        _nayruActors["Impa"].SetActive(false);
        SpawnCollapsedImpa(position, "CollapsedImpa");
        SpawnNayruActor("GhostVeran", "GhostVeran", position).SetScriptAnimation(
            _nayruDatabase.Actor("GhostVeran").Animation(0));
        _nayruGhostRevealFlickerRemaining = 90;
        _nayruVeranFacingTarget = OriginalPixelPosition(position);
        _nayruUpdateVeranFacingTarget = true;
        _nayruNayruHeldVeranFacing =
            _nayruActors["Nayru"].CurrentScriptAnimationSource ==
            _nayruDatabase.Actor("Nayru").Animation(2);
    }

    private void BeginGhostCharge()
    {
        NpcCharacter ghost = _nayruActors["GhostVeran"];
        ghost.Position = new Vector2(0x78, ghost.Position.Y);
        SetNayruAnimation("Nayru", 2);
        if (_nayruNayruHeldVeranFacing &&
            _nayruActors["Nayru"].CurrentScriptAnimationSource ==
            _nayruDatabase.Actor("Nayru").Animation(2))
        {
            // nayruScript00_part1 never calls turnToFaceSomething. Its
            // explicit animation $02 is held while angle $00 moves backward.
            _nayruGhostTrackingMask |= 0x04;
        }
    }

    private void FinishGhostCharge()
    {
        // Ghost substate 6 stops updating cfd5/cfd6 after this script ends.
        // Link and Ralph continue reading the final cached collision point.
        _nayruUpdateVeranFacingTarget = false;
        SetNayruAnimation("Nayru", 2);
        _nayruPostChargeFacingShown =
            _nayruActors["Nayru"].CurrentScriptAnimationSource ==
            _nayruDatabase.Actor("Nayru").Animation(2);
    }

    private void BeginNayruPossessionRecovery()
    {
        NpcCharacter nayru = _nayruActors["Nayru"];
        NpcCharacter ralph = _nayruActors["Ralph"];
        SetNayruAnimation("Nayru", 2);
        nayru.SetScriptPaletteOverride(null);
        SetNayruAnimation("Ralph", 0);
        _nayruTrackRalphVeranFacing = false;
        if (ralph.CurrentScriptAnimationSource ==
            _nayruDatabase.Actor("Ralph").Animation(0))
        {
            // cfd0=$15 exits @faceVeranGhost and selects animation $00.
            _nayruGhostTrackingMask |= 0x10;
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
                _nayruPossessionSwayShown |= Math.Abs(state.SwayX) >= 3;
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
            SetNayruAnimation("Nayru", 5);
        }
        if (elapsed < 549)
            return;

        SetNayruAnimation("Nayru", 2);
        StartGhostVeranEmergence();
        _nayruPossessionMovementSyncShown =
            state.NayruMoveStart == 150 && state.RalphMoveStart == 220 &&
            nayru.Position == new Vector2(0x78, 0x28) &&
            ralph.Position == state.RalphStart + Vector2.Down * 16.0f &&
            nayru.CurrentScriptAnimationSource ==
                _nayruDatabase.Actor("Nayru").Animation(2) &&
            ralph.CurrentScriptAnimationSource ==
                _nayruDatabase.Actor("Ralph").Animation(0);
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
        _nayruPossessionPaletteFlips++;

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
        _nayruPossessionBlinkShown |=
            _nayruPossessionPaletteFlips > 0 &&
            previous != nayru.CurrentAnimationPixelHash;
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
            !_nayruActors.TryGetValue("GhostVeran", out NpcCharacter? ghost) ||
            !ghost.Active)
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
        _nayruGhostEmergenceShown =
            ghost.Position == new Vector2(0x78, 0x24 - 17.25f) && ghost.Visible;
    }

    private void SpawnHumanVeran()
    {
        NpcCharacter human = SpawnNayruActor(
            "HumanVeran", "HumanVeran", _nayruActors["GhostVeran"].Position);
        human.SetScriptAnimation(_nayruDatabase.Actor("HumanVeran").Animation(0));
    }

    private void HideGhostVeranAfterPossession()
    {
        if (!_nayruActors.TryGetValue("GhostVeran", out NpcCharacter? ghost))
            return;
        ghost.Visible = false;
        _nayruGhostHiddenAfterPossession = ghost.Active && !ghost.Visible;
    }

    private void SpawnRalphSword()
    {
        NpcCharacter sword = SpawnNayruActor(
            "RalphSword", "RalphSword", _nayruActors["Ralph"].Position);
        sword.SetScriptAnimation(_nayruDatabase.Actor("RalphSword").Animation(0));
        sword.SetActive(false);
        _nayruRalphSwordAnimation = -1;
    }

    private void UpdateNayruRalphSword()
    {
        if (!_nayruActors.TryGetValue("RalphSword", out NpcCharacter? sword) ||
            !_nayruActors.TryGetValue("Ralph", out NpcCharacter? ralph) || !ralph.Active)
            return;
        sword.Position = ralph.Position;
        string swing = _nayruDatabase.Actor("Ralph").Animation(4);
        if (ralph.CurrentScriptAnimationSource != swing)
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
        _nayruRalphSwordShown |= sword.CurrentAnimationOpaquePixels > 0;
        _nayruSwordSpacingShown |=
            _nayruActors.TryGetValue("Nayru", out NpcCharacter? nayru) &&
            ralph.Position.DistanceTo(nayru.Position) >= 32.0f;
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
        _nayruActors[name] = npc;
        _nayruCollapsedImpaRendered |=
            record.DownAnimation == actor.Animation(6) &&
            npc.CurrentAnimationOpaquePixels > 0;
        return npc;
    }

    private void ActivateNayruPortal()
    {
        // cfd2 becomes nonzero on this update. linkCutscene3 stops reading
        // cfd5/cfd6 and forces left, while Ralph's script also selects $03.
        _nayruTrackLinkVeranFacing = false;
        _player.Face(Vector2I.Left);
        SetNayruAnimation("Ralph", 3);
        if (_player.FacingVector == Vector2I.Left &&
            _nayruActors.TryGetValue("Ralph", out NpcCharacter? ralph) &&
            ralph.CurrentScriptAnimationSource ==
                _nayruDatabase.Actor("Ralph").Animation(3))
        {
            _nayruGhostTrackingMask |= 0x20;
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
        _nayruVisitedVignettes |= 1 << index;
        _player.Visible = false;
        // loadGfxRegisterStateIndex $02 restores the status bar after each
        // cutscene_loadRoomObjectSetAndFadein room load.
        _nayruHud.Visible = true;
        switch (index)
        {
            case 0:
                NpcCharacter guy = SpawnNayruActor("VignetteGuy", "VignetteGuy");
                guy.SetScriptAnimation(_nayruDatabase.Actor("VignetteGuy").Animation(3));
                NpcCharacter oldMan = SpawnNayruActor("VignetteOldMan", "VignetteOldMan");
                oldMan.SetScriptAnimation(_nayruDatabase.Actor("VignetteOldMan").Animation(4));
                oldMan.SetAnimationRate(0.0f);
                oldMan.SetActive(false);
                NpcCharacter girl = SpawnNayruActor("VignetteGirl", "VignetteGirl");
                girl.SetScriptAnimation(_nayruDatabase.Actor("VignetteGirl").Animation(1));
                break;
            case 1:
                foreach (NayruIntroEventDatabase.VignetteMonkeyRecord record in
                    _nayruDatabase.VignetteMonkeys)
                {
                    NpcCharacter monkey = SpawnNayruActor(
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
                NpcCharacter boy = SpawnNayruActor("VignetteBoy", "VignetteBoy");
                boy.SetScriptAnimation(_nayruDatabase.Actor("VignetteBoy").Animation(1));
                boy.SetScriptPaletteOverride(_nayruDatabase.BoySpritePalette);
                boy.SetAnimationRate(0.0f);
                NpcCharacter lady = SpawnNayruActor("VignetteLady", "VignetteLady");
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
        NpcCharacter ralph = SpawnNayruActor("AftermathRalph", "AftermathRalph");
        // @initSubid02 selects animation $09 before ralphSubid02Script starts.
        ralph.SetScriptAnimation(_nayruDatabase.Actor("AftermathRalph").Animation(9));
        SpawnCollapsedImpa(new Vector2(0x38, 0x68), "AftermathImpaCollapsed");
        _player.WarpTo(new Vector2(0x58, 0x38), recordSafe: false);
        _player.Face(Vector2I.Up);
        _nayruTrackAftermathRalphFacing = true;
    }

    private void FinishAftermathRalphDeparture()
    {
        _nayruTrackAftermathRalphFacing = false;
        HideNayruActor("AftermathRalph");
    }

    private void LoadNayruCutsceneRoom(int group, int room, bool includeTimePortals)
    {
        OracleRoomData loaded = _rooms.LoadCutsceneRoom(group, room);
        _nayruRoom = loaded;
        _roomView.SetRoom(loaded.Texture);
        _entities.LoadCutsceneRoom(group, loaded, includeTimePortals);
        _transitions.UpdateCamera();
    }

    private void RestoreAftermathImpa()
    {
        HideNayruActor("AftermathImpaCollapsed");
        NpcCharacter impa = SpawnNayruActor("AftermathImpa", "AftermathImpa", new Vector2(0x38, 0x68));
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
            _treasures.GetTreasureDisplay(
                sword.TreasureId, sword.Parameter, _inventory));
        _roomView.GetParent().AddChild(_nayruSwordEffect);
    }

    private void GrantNayruSword()
    {
        _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWORD_00"));
        _player.BeginGetItemOneHandPose();
        _nayruHud.Refresh();
        _nayruSwordGiftShown = true;
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
        _nayruCommands.Clear();
        _nayruCommand = null;
        _nayruStage = NayruStage.None;
    }

    private void ClearNayruActors()
    {
        foreach (NpcCharacter actor in _nayruActors.Values)
        {
            actor.SetScriptDrawOffset(Vector2.Zero);
            actor.SetActive(false);
        }
        _nayruActors.Clear();
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
        if (deactivateActors)
        {
            foreach (NpcCharacter actor in _nayruActors.Values)
            {
                actor.SetScriptDrawOffset(Vector2.Zero);
                actor.SetActive(false);
            }
        }
        _nayruActors.Clear();
        _nayruFleeingAudience.Clear();
        _nayruAudienceTalkStates.Clear();
        _nayruVignetteMonkeys.Clear();
        _nayruVignetteIndex = -1;
        ClearNayruEffects(deactivateActors);
        RemoveNayruSwordEffect();
        _nayruCommands.Clear();
        _nayruCommand = null;
        _nayruTrackLinkVeranFacing = false;
        _nayruTrackRalphVeranFacing = false;
        _nayruUpdateVeranFacingTarget = false;
        _nayruTrackAftermathRalphFacing = false;
        _nayruRoom = null;
        _nayruStage = NayruStage.None;
    }

    private void SetNayruAnimation(string actorName, int animation)
    {
        if (!_nayruActors.TryGetValue(actorName, out NpcCharacter? actor) || !actor.Active)
            return;
        actor.SetScriptAnimation(NayruAnimationSource(actorName, animation));
    }

    private string NayruAnimationSource(string actorName, int animation)
    {
        string template = actorName switch
        {
            "Impa" or "AftermathImpa" => "AftermathImpa",
            _ when actorName.StartsWith("VignetteMonkey", StringComparison.Ordinal) => "Monkey",
            _ => actorName
        };
        return _nayruDatabase.Actor(template).Animation(animation);
    }

    private void HideNayruActor(string name)
    {
        if (_nayruActors.TryGetValue(name, out NpcCharacter? actor))
            actor.SetActive(false);
    }

    private void ShowNayruActor(string name)
    {
        if (_nayruActors.TryGetValue(name, out NpcCharacter? actor))
        {
            actor.SetActive(true);
            actor.Visible = true;
        }
    }

    private void ShowNayruText(int textId)
    {
        NayruIntroEventDatabase.TextRecord text = _nayruDatabase.Text(textId);
        if (text.TextboxPosition >= 0)
            _dialogue.ShowMessage(
                text.Message, _worldToScreen(_player.Position).Y, text.TextboxPosition);
        else
            _dialogue.ShowMessage(text.Message, _worldToScreen(_player.Position).Y);
    }

    private void bearSetAnimation(int animation) =>
        _nayruActors["Bear"].SetScriptAnimation(
            _nayruDatabase.Actor("Bear").Animation(animation));

    private static Vector2 AngleVector(int angle)
    {
        float radians = angle * Mathf.Pi / 16.0f;
        return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
    }

    private static bool WithinOriginalScreenBoundary(Vector2 position) =>
        position.Y >= -7 && position.Y < 136 &&
        position.X >= -7 && position.X < 168;

    private void Wait(int frames) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.Wait, Frames = frames });
    private void Text(int id) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.Text, TextId = id });
    private void Move(string actor, Vector2 delta, int frames) =>
        _nayruCommands.Enqueue(new NayruCommand
            { Kind = NayruCommandKind.Move, Actor = actor, Delta = delta, Frames = frames });
    private void NpcMove(
        string actor,
        Vector2 delta,
        int frames,
        NayruMoveFacing audit) =>
        _nayruCommands.Enqueue(new NayruCommand
        {
            Kind = NayruCommandKind.Move,
            Actor = actor,
            Delta = delta,
            Frames = frames,
            Value = AnimationForDelta(delta),
            FacingAuditBit = (ulong)audit,
            SetFacingOnStart = true
        });
    private void PoseMove(
        string actor,
        Vector2 delta,
        int frames,
        int animation,
        NayruMoveFacing audit) =>
        _nayruCommands.Enqueue(new NayruCommand
        {
            Kind = NayruCommandKind.Move,
            Actor = actor,
            Delta = delta,
            Frames = frames,
            Value = animation,
            FacingAuditBit = (ulong)audit
        });
    private void MovePlayer(Vector2 delta, int frames) => Move("Player", delta, frames);
    private void ParallelMove(
        string actor,
        Vector2 delta,
        string actor2,
        Vector2 delta2,
        int frames) =>
        _nayruCommands.Enqueue(new NayruCommand
        {
            Kind = NayruCommandKind.ParallelMove,
            Actor = actor,
            Delta = delta,
            Actor2 = actor2,
            Delta2 = delta2,
            Frames = frames,
            Frames2 = frames
        });
    private void ParallelMove(
        string actor,
        Vector2 delta,
        int frames,
        string actor2,
        Vector2 delta2,
        int frames2) =>
        _nayruCommands.Enqueue(new NayruCommand
        {
            Kind = NayruCommandKind.ParallelMove,
            Actor = actor,
            Delta = delta,
            Frames = frames,
            Actor2 = actor2,
            Delta2 = delta2,
            Frames2 = frames2
        });
    private void Jump(string actor) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.Jump, Actor = actor, Frames = 1 });
    private void PortalFlight(string actor) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.PortalFlight, Actor = actor, Frames = 1 });
    private void RoomPalette(int frames) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.RoomPalette, Frames = frames });
    private void Animation(string actor, int animation) =>
        _nayruCommands.Enqueue(new NayruCommand
            { Kind = NayruCommandKind.Animation, Actor = actor, Value = animation });
    private void Callback(Action callback) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.Callback, Callback = callback });
    private void Fade(int frames, bool fadeIn) =>
        FadeTo(frames, fadeIn ? 0.0f : 1.0f, Colors.White);
    private void FadeTo(int frames, float targetAlpha, Color color) =>
        _nayruCommands.Enqueue(new NayruCommand
        {
            Kind = NayruCommandKind.Fade,
            Frames = frames,
            TargetAlpha = targetAlpha,
            FadeColor = color
        });
    private void Flicker(string actor, int frames) => _nayruCommands.Enqueue(new NayruCommand
        { Kind = NayruCommandKind.Flicker, Actor = actor, Frames = frames });
    private void PaletteFlicker(string actor, int frames) =>
        _nayruCommands.Enqueue(new NayruCommand
            { Kind = NayruCommandKind.PaletteFlicker, Actor = actor, Frames = frames });

    private static int AnimationForDelta(Vector2 delta)
    {
        return AnimationForFacing(FacingForDelta(delta));
    }
}

using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Room 3:ae's INTERAC_HARP_OF_AGES_SPAWNER $b3:$00 and the $36:$07
/// post-pickup Nayru vision. The wrapper states remain native while
/// nayruScript07 runs through the typed command runner.
/// </summary>
internal sealed class HarpOfAgesEvent :
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private static readonly int[] NoteSwaySteps =
        [-1, -2, -1, 0, 1, 2, 1, 0];

    private readonly RoomEventContext _context;
    private readonly HarpOfAgesEventDatabase _database = new();
    private readonly CutsceneCommandRunner _runner;
    private readonly List<HarpMusicNoteState> _notes = new();
    private HarpOfAgesEventStage _stage;
    private GroundTreasurePickup? _harp;
    private GroundTreasurePickup? _echoReward;
    private NpcCharacter? _sparkle;
    private NpcCharacter? _nayru;
    private int _stageCounter;
    private int _textboxFlags;
    private int _nayruDirection = 2;
    private int _nayruLastNoteFrame = -1;
    private int _noteSerial;
    private bool _nayruAnimationEnabled = true;
    private bool _harpCollected;
    private Vector2 _harpPosition;
    private Vector2 _fadeOriginalPosition;
    private Vector2 _fadeOriginalSize;
    private int _fadeOriginalZIndex;
    private bool _fadePresentationOwned;

    internal HarpOfAgesEvent(RoomEventContext context)
    {
        _context = context;
        _runner = new CutsceneCommandRunner(this);
        _context.Entities.GroundTreasureCollected += OnGroundTreasureCollected;
    }

    public bool HasState => _stage != HarpOfAgesEventStage.Inactive;
    public bool BlocksGameplay => _stage is
        HarpOfAgesEventStage.AwaitingTextOpen or
        HarpOfAgesEventStage.AwaitingTextClose or
        HarpOfAgesEventStage.FadeOut or
        HarpOfAgesEventStage.BlackHold or
        HarpOfAgesEventStage.NayruFlicker or
        HarpOfAgesEventStage.Script;

    internal HarpOfAgesEventStage Stage => _stage;
    internal GroundTreasurePickup? Harp => _harp;
    internal GroundTreasurePickup? EchoReward => _echoReward;
    internal NpcCharacter? Sparkle => _sparkle;
    internal NpcCharacter? Nayru => _nayru;
    internal int StageCounter => _stageCounter;
    internal int CommandInstruction => _runner.Instruction;
    internal int CommandCounter => _runner.Counter;
    internal int TextboxFlags => _textboxFlags;
    internal int MusicNoteCount => _notes.Count;
    internal int MusicNotesSpawned => _noteSerial;
    internal HarpOfAgesEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        HarpOfAgesEventRecord record = _database.Record;
        return group == record.Group &&
            room.Id == record.Room &&
            !_context.Rooms.SaveData.HasRoomFlag(
                record.Group, record.Room, (byte)record.RoomFlag);
    }

    public void Start(OracleRoomData _)
    {
        Cancel();
        HarpOfAgesEventRecord record = _database.Record;
        TreasureObjectRecord treasure =
            _context.Treasures.GetObject(record.HarpObject);
        if (treasure.TreasureId != record.HarpTreasure ||
            treasure.SubId != 0 || treasure.Parameter != 0)
        {
            throw Unsupported(
                $"{record.HarpObject} no longer represents " +
                $"treasure ${record.HarpTreasure:x2}:$00");
        }
        TreasureObjectVisualRecord visual =
            _context.Treasures.GetObjectVisual(treasure.Graphic);
        var pickupRecord = new GroundTreasureDatabaseRecord(
            record.Group,
            record.Room,
            0,
            record.HarpY,
            record.HarpX,
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            treasure.TextId,
            treasure.Message,
            "harpOfAgesSpawner.s:@state0",
            SpawnMode: 0,
            GrabMode: 2);
        _harp = _context.Entities.Spawn<GroundTreasurePickup>(
            new GroundTreasureSpawn(pickupRecord));
        _harpPosition = _harp.Position;

        HarpOfAgesVisualRecord sparkle = _database.Visual("Sparkle");
        _sparkle = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                sparkle.ToNpcRecord(
                    record.Group,
                    record.Room,
                    record.HarpY,
                    record.HarpX,
                    animation: 0),
                "HarpOfAgesSparkle"));
        _sparkle.SetGraphicsSourceOffset(sparkle.SourceOffset);
        _sparkle.SetScriptAnimation(sparkle.Animation0);
        _sparkle.SetBlocksLink(false);
        _sparkle.SetFixedDrawPriority(NpcCharacter.FixedLowPriorityZIndex);
        _stage = HarpOfAgesEventStage.AwaitingPickup;
    }

    public void UpdateFrame()
    {
        if (!HasState)
            return;

        UpdateMusicNotes();
        UpdateAttachedSparkle();

        switch (_stage)
        {
            case HarpOfAgesEventStage.AwaitingPickup:
                if (!_harpCollected)
                    return;
                // The spawner owns an earlier interaction slot than the
                // treasure it creates, so state 1 observes ROOMFLAG_ITEM on
                // the update after the treasure's collection handler.
                _stage = HarpOfAgesEventStage.PickupDelay;
                return;

            case HarpOfAgesEventStage.PickupDelay:
                BeginPickupCutscene();
                return;

            case HarpOfAgesEventStage.AwaitingTextOpen:
                if (!_context.DialogueOpen)
                    return;
                _context.Player.Face(Vector2I.Up);
                _stage = HarpOfAgesEventStage.AwaitingTextClose;
                return;

            case HarpOfAgesEventStage.AwaitingTextClose:
                if (_context.DialogueOpen)
                    return;
                BeginFadeOut();
                return;

            case HarpOfAgesEventStage.FadeOut:
                UpdateFadeOut();
                return;

            case HarpOfAgesEventStage.BlackHold:
                _stageCounter--;
                if (_stageCounter == 0)
                    SpawnNayru();
                return;

            case HarpOfAgesEventStage.NayruFlicker:
                UpdateNayruFlicker();
                return;

            case HarpOfAgesEventStage.Script:
                _runner.AdvanceFrame();
                UpdateNayruScriptAnimation();
                return;

            case HarpOfAgesEventStage.FadeInTail:
                UpdateFadeInTail();
                return;

            default:
                throw Unsupported($"update stage {_stage}");
        }
    }

    public void Cancel()
    {
        _runner.Clear();
        _context.Player.EndHarpPose();
        _context.Player.EndCutsceneControl();
        _context.Hud.ShowStatusBar();
        _context.RoomView.ClearBackgroundFade();
        RestoreFadePresentation();
        _sparkle?.SetActive(false);
        _nayru?.SetActive(false);
        foreach (HarpMusicNoteState note in _notes)
            note.Actor.SetActive(false);
        _notes.Clear();
        _harp = null;
        _echoReward = null;
        _sparkle = null;
        _nayru = null;
        _stageCounter = 0;
        _textboxFlags = 0;
        _nayruDirection = 2;
        _nayruLastNoteFrame = -1;
        _noteSerial = 0;
        _nayruAnimationEnabled = true;
        _harpCollected = false;
        _harpPosition = Vector2.Zero;
        _stage = HarpOfAgesEventStage.Inactive;
    }

    private void OnGroundTreasureCollected(
        GroundTreasurePickup treasure,
        Player _)
    {
        if (_stage == HarpOfAgesEventStage.AwaitingPickup &&
            ReferenceEquals(treasure, _harp))
        {
            _harpCollected = true;
        }
    }

    private void UpdateAttachedSparkle()
    {
        if (_sparkle is not { Active: true })
            return;
        if (_harp is not null)
        {
            if (GodotObject.IsInstanceValid(_harp))
                _harpPosition = _harp.Position;
            else
                _harp = null;
        }
        _sparkle.Position = _harpPosition;
        _sparkle.Visible = (_context.Entities.FrameCounter & 1) == 0;
    }

    private void BeginPickupCutscene()
    {
        HarpOfAgesEventRecord record = _database.Record;
        if (!_context.Rooms.SaveData.HasRoomFlag(
            record.Group, record.Room, (byte)record.RoomFlag))
        {
            throw Unsupported(
                "begin the pickup cutscene before ROOMFLAG_ITEM was set");
        }
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);
        _context.Player.BeginCutsceneControl();
        _stage = HarpOfAgesEventStage.AwaitingTextOpen;
    }

    private void BeginFadeOut()
    {
        _sparkle?.SetActive(false);
        _stageCounter = 0;
        _context.Hud.HideStatusBar();
        _context.RoomView.SetBackgroundFade(Colors.Black, 0.0f);
        _stage = HarpOfAgesEventStage.FadeOut;
    }

    private void UpdateFadeOut()
    {
        HarpOfAgesEventRecord record = _database.Record;
        int steps = Math.Min(
            32,
            (_stageCounter + 1) / record.FadeDelay);
        _context.RoomView.SetBackgroundFade(
            Colors.Black,
            steps / 32.0f);
        _stageCounter++;
        if (_stageCounter < record.FadeFrames)
            return;
        _stageCounter = record.BlackHold;
        _stage = HarpOfAgesEventStage.BlackHold;
    }

    private void SpawnNayru()
    {
        HarpOfAgesEventRecord record = _database.Record;
        HarpOfAgesVisualRecord visual = _database.Visual("Nayru");
        _nayru = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                visual.ToNpcRecord(
                    record.Group,
                    record.Room,
                    record.SpawnerY,
                    record.SpawnerX,
                    animation: 2),
                "HarpOfAgesNayru"));
        _nayru.AppendScriptGraphics(visual.ExtraSprite);
        _nayru.SetScriptAnimation(visual.Animation(2));
        // The native wrapper calls interactionAnimate itself only after the
        // flicker and only when cfc0 bit 0 is clear.
        _nayru.SetAnimationRate(0.0f);
        _nayru.Visible = true;
        _stageCounter = record.NayruFlicker;
        _stage = HarpOfAgesEventStage.NayruFlicker;
    }

    private void UpdateNayruFlicker()
    {
        _stageCounter--;
        if (_stageCounter != 0)
        {
            _nayru!.Visible = !_nayru.Visible;
            return;
        }

        _nayru!.Visible = true;
        _nayruAnimationEnabled = true;
        _nayruDirection = 2;
        _context.Sound.PlaySound(_database.Record.NayruMusic);
        _runner.Start(_database.Commands);
        _stage = HarpOfAgesEventStage.Script;
    }

    private void UpdateNayruScriptAnimation()
    {
        if (_stage != HarpOfAgesEventStage.Script ||
            _nayru is not { Active: true } ||
            !_nayruAnimationEnabled)
        {
            return;
        }

        int parameter = _nayru.CurrentAnimationParameter;
        int frame = _nayru.CurrentAnimationFrame;
        if (_nayruDirection == 7 && parameter != 0)
        {
            if (_nayruLastNoteFrame != frame)
            {
                float xOffset = parameter == 1 ? -6.0f : 8.0f;
                SpawnMusicNote(
                    _nayru.Position + new Vector2(xOffset, -4),
                    floatsLeft: parameter == 1);
                _nayruLastNoteFrame = frame;
            }
        }
        else
        {
            _nayruLastNoteFrame = -1;
        }
        _nayru.AdvanceAnimationUpdates(1);
    }

    private void SpawnMusicNote(Vector2 position, bool floatsLeft)
    {
        EffectRecord effect = _database.MusicNote;
        NpcCharacter actor = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                effect.ToNpcRecord(
                    _database.Record.Group,
                    _database.Record.Room,
                    Mathf.RoundToInt(position.Y),
                    Mathf.RoundToInt(position.X)),
                $"HarpMusicNote{_noteSerial}"));
        actor.Position = position;
        actor.SetScriptAnimation(effect.Animation);
        actor.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
        float velocityX = effect.VelocityXFixed / 256.0f;
        if (floatsLeft)
            velocityX = -velocityX;
        _notes.Add(new HarpMusicNoteState(
            actor,
            effect.Duration,
            new Vector2(velocityX, effect.VelocityYFixed / 256.0f),
            effect.Sway));
        _noteSerial++;
    }

    private void UpdateMusicNotes()
    {
        for (int index = _notes.Count - 1; index >= 0; index--)
        {
            HarpMusicNoteState note = _notes[index];
            note.Actor.Position += note.Velocity;
            if (note.Sway && (_context.Entities.FrameCounter & 7) == 0)
            {
                note.Actor.Position += Vector2.Right *
                    NoteSwaySteps[(_context.Entities.FrameCounter >> 3) & 7];
            }
            note.Remaining--;
            if (note.Remaining > 0)
                continue;
            note.Actor.SetActive(false);
            _notes.RemoveAt(index);
        }
    }

    private bool UpdatePlayHarpSong(int commandUpdate, int frames)
    {
        HarpOfAgesEventRecord record = _database.Record;
        if (frames != record.SongNativeFrames)
            throw Unsupported($"play a {frames}-update response song");
        if (commandUpdate == 0)
            return false; // state 0
        if (commandUpdate < record.SongInitialDelay)
            return false; // state 1, counter remains nonzero
        if (commandUpdate == record.SongInitialDelay)
        {
            _context.Player.BeginHarpPose();
            _context.Sound.PlaySound(record.SongSound);
            return false;
        }

        int songUpdate = commandUpdate - record.SongInitialDelay - 1;
        int phraseUpdates = record.SongPhaseFrames * record.SongPhases;
        if (songUpdate < phraseUpdates)
        {
            int phrase = songUpdate / record.SongPhaseFrames;
            if ((_context.Entities.FrameCounter & 0x1f) == 0)
            {
                bool floatsLeft = (phrase & 1) == 0;
                SpawnMusicNote(
                    _context.Player.Position +
                        new Vector2(floatsLeft ? -8 : 8, -8),
                    floatsLeft);
            }
            _context.Player.AdvanceHarpPose();
            return false;
        }

        _context.Player.EndHarpPose();
        return commandUpdate + 1 >= frames;
    }

    private void BeginFadeInTail()
    {
        _context.Player.EndHarpPose();
        _context.Player.EndCutsceneControl();
        _context.Sound.PlayRoomMusic(
            _database.Record.Group,
            _database.Record.Room);
        _context.Hud.ShowStatusBar();
        _context.RoomView.ClearBackgroundFade();
        _nayru?.SetActive(false);
        _stageCounter = 0;
        OwnFullScreenFade();
        _context.Fade.Color = Colors.White;
        _stage = HarpOfAgesEventStage.FadeInTail;
    }

    private void UpdateFadeInTail()
    {
        HarpOfAgesEventRecord record = _database.Record;
        int steps = Math.Min(
            32,
            (_stageCounter + 1) / record.FinalFadeDelay);
        _context.Fade.Color = new Color(
            1, 1, 1, 1.0f - steps / 32.0f);
        _stageCounter++;
        if (_stageCounter < record.FinalFadeFrames)
            return;
        RestoreFadePresentation();
        _stage = HarpOfAgesEventStage.Inactive;
    }

    private void OwnFullScreenFade()
    {
        if (_fadePresentationOwned)
            return;
        _fadePresentationOwned = true;
        _fadeOriginalPosition = _context.Fade.Position;
        _fadeOriginalSize = _context.Fade.Size;
        _fadeOriginalZIndex = _context.Fade.ZIndex;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = _context.Hud.ZIndex + 1;
    }

    private void RestoreFadePresentation()
    {
        _context.Fade.Color = new Color(1, 1, 1, 0);
        if (!_fadePresentationOwned)
            return;
        _context.Fade.Position = _fadeOriginalPosition;
        _context.Fade.Size = _fadeOriginalSize;
        _context.Fade.ZIndex = _fadeOriginalZIndex;
        _fadePresentationOwned = false;
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;

    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == "Nayru";

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects=${value:x2}");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw Unsupported($"read '{binding}'=${value:x2}");

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is not (0x1d10 or 0x1d11))
            throw Unsupported($"show text TX_{textId:x4}");
        _context.ShowDialogue(message, textboxFlags: _textboxFlags);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (actor != "Nayru" || _nayru is null ||
            encodedAnimation != _database.Visual("Nayru").Animation(animation))
        {
            throw Unsupported(
                $"set actor '{actor}' animation ${animation:x2}");
        }
        _nayru.SetScriptAnimation(encodedAnimation);
        _nayru.SetAnimationRate(0.0f);
        _nayruLastNoteFrame = -1;
    }

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' movement angle ${angle:x2}");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        throw Unsupported($"set actor '{actor}' collision");

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw Unsupported($"set actor '{actor}' A-button sensitivity");

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        throw Unsupported($"move actor '{actor}'");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set actor '{actor}' Z");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        throw Unsupported($"set actor '{actor}' visibility={visible}");

    void ICutsceneCommandHost.WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        if (actor != "Nayru" || address != 0x08 ||
            value is not (0x02 or 0x07))
        {
            throw Unsupported(
                $"write actor '{actor}' byte ${address:x2}=${value:x2}");
        }
        _nayruDirection = value;
    }

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding != "TextboxFlags" ||
            value != _database.Record.TextboxFlags)
        {
            throw Unsupported($"write '{binding}'=${value:x2}");
        }
        _textboxFlags = value;
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        HarpOfAgesEventRecord record = _database.Record;
        if (treasureId != record.EchoesTreasure || parameter != 0)
            throw Unsupported($"give treasure ${treasureId:x2}:${parameter:x2}");
        _echoReward = _context.GrantScriptTreasure(
            record.Group,
            record.Room,
            treasureId,
            parameter,
            record.EchoesObject,
            "scriptHelper.s:nayruScript07",
            textboxFlags: _textboxFlags);
    }

    void ICutsceneCommandHost.SetMusic(int music) =>
        throw Unsupported($"set music ${music:x2}");

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw Unsupported($"set room flag ${flag:x2}");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        if (handler != "ToggleNayruAnimation")
            throw Unsupported($"run native handler '{handler}'");
        _nayruAnimationEnabled = !_nayruAnimationEnabled;
    }

    bool ICutsceneCommandHost.UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload)
    {
        if (handler != "PlayHarpSong" ||
            actor is not null ||
            !string.IsNullOrEmpty(payload))
        {
            throw Unsupported($"update native handler '{handler}'");
        }
        return UpdatePlayHarpSong(commandUpdate, frames);
    }

    void ICutsceneCommandHost.ScriptEnded() => BeginFadeInTail();

    private InvalidOperationException Unsupported(string operation) =>
        new($"Room {_database.Record.Group:x}:{_database.Record.Room:x2} " +
            $"Harp of Ages event cannot {operation}.");
}

internal sealed class HarpMusicNoteState(
    NpcCharacter actor,
    int remaining,
    Vector2 velocity,
    bool sway)
{
    internal NpcCharacter Actor { get; } = actor;
    internal int Remaining { get; set; } = remaining;
    internal Vector2 Velocity { get; } = velocity;
    internal bool Sway { get; } = sway;
}

internal enum HarpOfAgesEventStage
{
    Inactive,
    AwaitingPickup,
    PickupDelay,
    AwaitingTextOpen,
    AwaitingTextClose,
    FadeOut,
    BlackHold,
    NayruFlicker,
    Script,
    FadeInTail
}

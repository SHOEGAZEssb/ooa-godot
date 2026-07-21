using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs Ralph's pre-Black Tower departure and the linked/unlinked heritage
/// scenes in past room $75. Actor lanes retain original placement order.
/// </summary>
internal sealed class PreBlackTowerEvent : IRoomEntryEvent, ICutsceneCommandHost
{
    internal enum EventStage
    {
        Inactive,
        RalphUnlinkedNative,
        RalphUnlinkedScript,
        WaitingForImpa,
        ImpaUnlinked,
        Linked
    }

    private sealed record TimedEffect(NpcCharacter Actor, int Frames);

    private readonly RoomEventContext _context;
    private readonly PreBlackTowerEventDatabase _database = new();
    private readonly PreBlackTowerEventDatabase.EventRecord _record;
    private readonly CutsceneCommandRunner _ralphRunner;
    private readonly CutsceneCommandRunner _impaRunner;
    private readonly CutsceneCommandRunner _nayruRunner;
    private readonly CutsceneCommandRunner _zeldaRunner;
    private readonly Dictionary<string, NpcCharacter> _actors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _precisePositions =
        new(StringComparer.Ordinal);
    private readonly List<TimedEffect> _effects = new();

    private EventStage _stage;
    private int _sharedSignal;
    private int _sharedBits;
    private int _ralphSubstate;
    private int _impaSubstate;
    private int _impaVar38;
    private int _nativeCounter;
    private int _playerMoveFrames;
    private Vector2 _playerMove;
    private Vector2I _playerMoveDirection;
    private int _impaZFixed;
    private int _impaSpeedZ;
    private bool _impaAirborne;
    private bool _ralphEnded;
    private bool _impaEnded;
    private bool _nayruEnded;
    private bool _zeldaEnded;
    private int _effectSerial;

    public PreBlackTowerEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _ralphRunner = new CutsceneCommandRunner(this);
        _impaRunner = new CutsceneCommandRunner(this);
        _nayruRunner = new CutsceneCommandRunner(this);
        _zeldaRunner = new CutsceneCommandRunner(this);
        if (_record.MakuSeedTreasure != TreasureDatabase.TreasureMakuSeed ||
            _record.CompletionFlag != OracleSaveData.GlobalFlagPreBlackTowerCutsceneDone ||
            _record.RalphEnteredFlag != OracleSaveData.GlobalFlagRalphEnteredBlackTower)
        {
            throw new InvalidOperationException(
                "Pre-Black Tower imported save identifiers no longer match runtime constants.");
        }
    }

    public bool HasState => _stage != EventStage.Inactive;
    public bool BlocksGameplay => _stage is not EventStage.Inactive and not EventStage.WaitingForImpa;
    internal EventStage Stage => _stage;
    internal int SharedSignal => _sharedSignal;
    internal int SharedBits => _sharedBits;
    internal int RalphSubstate => _ralphSubstate;
    internal int ImpaSubstate => _impaSubstate;
    internal int ImpaVar38 => _impaVar38;
    internal int ImpaZFixed => _impaZFixed;
    internal int EffectCount => _effects.Count;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        _context.Rooms.SaveData.HasTreasure(_record.MakuSeedTreasure) &&
        !_context.Rooms.SaveData.HasGlobalFlag(_record.CompletionFlag);

    public void Start(OracleRoomData _)
    {
        Cancel();
        NpcCharacter ralph = Register(
            "Ralph",
            _record.RalphId,
            _record.RalphSubId,
            "INTERAC_RALPH");

        if (_context.Rooms.SaveData.IsLinkedGame)
        {
            Register(
                "Impa",
                _record.ImpaId,
                _record.ImpaLinkedSubId,
                "INTERAC_IMPA_IN_CUTSCENE");
            Register(
                "Nayru",
                _record.NayruId,
                _record.NayruLinkedSubId,
                "INTERAC_NAYRU");
            Register(
                "Zelda",
                _record.ZeldaId,
                _record.ZeldaSubId,
                "INTERAC_ZELDA");
            if (!ralph.Active)
                return;
            StartLinked();
            return;
        }

        Register(
            "Impa",
            _record.ImpaId,
            _record.ImpaUnlinkedSubId,
            "INTERAC_IMPA_IN_CUTSCENE");
        if (ralph.Active)
        {
            _context.Player.BeginCutsceneControl();
            _stage = EventStage.RalphUnlinkedNative;
        }
        else if (_context.Rooms.SaveData.HasGlobalFlag(_record.RalphEnteredFlag))
        {
            _stage = EventStage.WaitingForImpa;
        }
    }

    public void UpdateFrame()
    {
        UpdateEffects();
        switch (_stage)
        {
            case EventStage.RalphUnlinkedNative:
                UpdateUnlinkedRalphNative();
                break;
            case EventStage.RalphUnlinkedScript:
                _ralphRunner.AdvanceFrame();
                if (!_ralphRunner.Active)
                    FinishUnlinkedRalph();
                break;
            case EventStage.WaitingForImpa:
                if (_context.Player.Position.Y >= 0x60)
                    StartUnlinkedImpa();
                break;
            case EventStage.ImpaUnlinked:
                UpdateUnlinkedImpa();
                break;
            case EventStage.Linked:
                UpdateLinked();
                break;
        }
    }

    public void Cancel()
    {
        _ralphRunner.Clear();
        _impaRunner.Clear();
        _nayruRunner.Clear();
        _zeldaRunner.Clear();
        foreach (TimedEffect effect in _effects)
            effect.Actor.SetActive(false);
        _effects.Clear();
        _actors.Clear();
        _precisePositions.Clear();
        _stage = EventStage.Inactive;
        _sharedSignal = 0;
        _sharedBits = 0;
        _ralphSubstate = 0;
        _impaSubstate = 0;
        _impaVar38 = 0;
        _nativeCounter = 0;
        _playerMoveFrames = 0;
        _playerMove = Vector2.Zero;
        _playerMoveDirection = Vector2I.Zero;
        _impaZFixed = 0;
        _impaSpeedZ = 0;
        _impaAirborne = false;
        _ralphEnded = false;
        _impaEnded = false;
        _nayruEnded = false;
        _zeldaEnded = false;
    }

    private void StartLinked()
    {
        _stage = EventStage.Linked;
        _context.Player.BeginCutsceneControl();
        _actors["Ralph"].Position = new Vector2(0x50, _actors["Ralph"].Position.Y);
        _precisePositions["Ralph"] = _actors["Ralph"].Position;
        _sharedSignal = 0;
        _nativeCounter = 0x1e;
        _actors["Impa"].SetScriptAnimation(_actors["Impa"].Record.DownAnimation);
        _actors["Nayru"].SetScriptAnimation(_actors["Nayru"].Record.DownAnimation);

        _ralphRunner.Start(_database.RalphLinked);
        _impaRunner.Start(_database.ImpaLinked);
        _nayruRunner.Start(_database.NayruLinked);
        _zeldaRunner.Start(_database.ZeldaLinked);
        // Zelda's initializer writes SPEED_100 and angle $08 before loading
        // zeldaSubid04Script; its first applyspeed consumes those bytes.
        _zeldaRunner.SetInitialMotionRegisters("Zelda", 0x28, 0x08);
    }

    private void UpdateLinked()
    {
        if (_ralphRunner.Active)
            _ralphRunner.AdvanceFrame();
        UpdateLinkedRalphNative();
        if (!_ralphRunner.Active && !_ralphEnded)
        {
            _ralphEnded = true;
            _actors["Ralph"].SetActive(false);
        }

        if (!UpdateImpaGravity() && _impaRunner.Active)
            _impaRunner.AdvanceFrame();
        UpdateLinkedImpaNative();
        if (!_impaRunner.Active && !_impaEnded)
        {
            _impaEnded = true;
            _actors["Impa"].SetActive(false);
        }

        if (_nayruRunner.Active)
            _nayruRunner.AdvanceFrame();
        if (!_nayruRunner.Active && !_nayruEnded)
        {
            _nayruEnded = true;
            _actors["Nayru"].SetActive(false);
        }

        if (_zeldaRunner.Active)
            _zeldaRunner.AdvanceFrame();
        if (!_zeldaRunner.Active && !_zeldaEnded)
        {
            _zeldaEnded = true;
            _actors["Zelda"].SetActive(false);
        }

        if (!_impaRunner.Active)
            FinishEvent();
    }

    private void UpdateLinkedRalphNative()
    {
        switch (_ralphSubstate)
        {
            case 0:
                if (--_nativeCounter == 0)
                    BeginPlayerHorizontalMove(0x50, nextSubstate: 1);
                break;
            case 1:
                if (AdvancePlayerMove())
                {
                    BeginPlayerMove(0x18, Vector2.Down, Vector2I.Down);
                    _ralphSubstate = 2;
                }
                break;
            case 2:
                if (AdvancePlayerMove())
                {
                    _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Down);
                    _actors["Ralph"].SetScriptAnimation(Animation("Ralph", 0));
                    _ralphSubstate = 3;
                }
                break;
        }
    }

    private void UpdateLinkedImpaNative()
    {
        switch (_impaSubstate)
        {
            case 0 when _sharedSignal == 0x04:
                BeginPlayerMove(0x29, Vector2.Down, Vector2I.Down);
                _impaSubstate = 1;
                break;
            case 1:
                if (AdvancePlayerMove())
                {
                    _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Down);
                    _impaSubstate = 2;
                }
                break;
        }
    }

    private void UpdateUnlinkedRalphNative()
    {
        switch (_ralphSubstate)
        {
            case 0:
                SpawnEffect(
                    _context.Player.Position + new Vector2(-10, 14),
                    0x1e);
                _context.Sound.PlaySound(_record.ClinkSound);
                _nativeCounter = 0x1e;
                _ralphSubstate = 1;
                break;
            case 1:
                if (--_nativeCounter != 0)
                    break;
                _actors["Ralph"].SetScriptAnimation(Animation("Ralph", 0));
                BeginPlayerHorizontalMove(0x50, nextSubstate: 2);
                break;
            case 2:
                if (AdvancePlayerMove())
                {
                    BeginPlayerMove(0x50, Vector2.Down, Vector2I.Down);
                    _ralphSubstate = 3;
                }
                break;
            case 3:
                if (AdvancePlayerMove())
                {
                    _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Right);
                    _actors["Ralph"].SetScriptAnimation(Animation("Ralph", 3));
                    _ralphSubstate = 4;
                    _ralphRunner.Start(_database.RalphUnlinked);
                    // Ralph's native substate $00 wrote SPEED_180 before the
                    // script lane became active.
                    _ralphRunner.SetInitialMotionRegisters("Ralph", 0x3c, 0x10);
                    _stage = EventStage.RalphUnlinkedScript;
                }
                break;
        }
    }

    private void FinishUnlinkedRalph()
    {
        _actors["Ralph"].SetActive(false);
        _context.Player.EndCutsceneControl();
        _stage = EventStage.WaitingForImpa;
    }

    private void StartUnlinkedImpa()
    {
        _context.Player.BeginCutsceneControl();
        _sharedBits = 0;
        _impaSubstate = 1;
        _impaVar38 = 0;
        _impaRunner.Start(_database.ImpaUnlinked);
        _stage = EventStage.ImpaUnlinked;
    }

    private void UpdateUnlinkedImpa()
    {
        if (!UpdateImpaGravity() && _impaRunner.Active)
            _impaRunner.AdvanceFrame();
        UpdateUnlinkedImpaNative();

        if (_nayruRunner.Active)
            _nayruRunner.AdvanceFrame();
        if (!_impaRunner.Active)
            _actors["Impa"].SetActive(false);
        if (_nayruEnded)
            FinishEvent();
    }

    private void UpdateUnlinkedImpaNative()
    {
        switch (_impaVar38)
        {
            case 0 when (_sharedBits & 0x01) != 0:
                _nativeCounter = 0x10;
                _impaVar38 = 1;
                break;
            case 1:
                if (--_nativeCounter != 0)
                    break;
                BeginPlayerHorizontalMove(0x50, nextSubstate: -1);
                _impaVar38 = 2;
                break;
            case 2:
                if (AdvancePlayerMove())
                {
                    int distance = Math.Max(0, Mathf.RoundToInt(_context.Player.Position.Y) - 0x48);
                    BeginPlayerMove(distance, Vector2.Up, Vector2I.Up);
                    _impaVar38 = 3;
                }
                break;
            case 3:
                if (AdvancePlayerMove())
                {
                    _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Up);
                    _impaVar38 = 4;
                }
                break;
        }

        if (!_nayruRunner.Active && _actors.TryGetValue("Nayru", out NpcCharacter? nayru) &&
            !_nayruEnded)
        {
            _nayruEnded = true;
            nayru.SetActive(false);
            _context.Rooms.SaveData.SetGlobalFlag(_record.CompletionFlag);
        }
    }

    private bool UpdateImpaGravity()
    {
        if (!_impaAirborne)
            return false;
        _impaZFixed += _impaSpeedZ;
        _impaSpeedZ += _record.Gravity;
        if (_impaZFixed >= 0)
        {
            _impaZFixed = 0;
            _impaSpeedZ = 0;
            _impaAirborne = false;
        }
        _actors["Impa"].SetScriptDrawOffset(new Vector2(0, _impaZFixed >> 8));
        return _impaAirborne;
    }

    private void BeginPlayerHorizontalMove(int targetX, int nextSubstate)
    {
        int difference = Mathf.RoundToInt(_context.Player.Position.X) - targetX;
        if (difference is >= -2 and <= 2)
        {
            _playerMoveFrames = 0;
            if (nextSubstate >= 0)
                _ralphSubstate = nextSubstate;
            return;
        }
        Vector2 direction = difference < 0 ? Vector2.Right : Vector2.Left;
        Vector2I facing = difference < 0 ? Vector2I.Right : Vector2I.Left;
        BeginPlayerMove(Math.Abs(difference), direction, facing);
        if (nextSubstate >= 0)
            _ralphSubstate = nextSubstate;
    }

    private void BeginPlayerMove(int frames, Vector2 movement, Vector2I direction)
    {
        _playerMoveFrames = frames;
        _playerMove = movement;
        _playerMoveDirection = direction;
    }

    private bool AdvancePlayerMove()
    {
        if (_playerMoveFrames <= 0)
            return true;
        _context.Player.AdvanceCutsceneMovement(_playerMove, _playerMoveDirection);
        _playerMoveFrames--;
        return _playerMoveFrames == 0;
    }

    private void FinishEvent()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.CompletionFlag);
        _context.Player.EndCutsceneControl();
        _stage = EventStage.Inactive;
    }

    private NpcCharacter Register(
        string name,
        int interactionId,
        int subId,
        string interactionName)
    {
        NpcCharacter actor = _context.RequireNpc(
            _record.Group,
            _record.Room,
            interactionId,
            subId,
            interactionName);
        _actors[name] = actor;
        _precisePositions[name] = actor.Position;
        return actor;
    }

    private NpcCharacter Actor(string actor) =>
        _actors.TryGetValue(actor, out NpcCharacter? npc)
            ? npc
            : throw new InvalidOperationException(
                $"Pre-Black Tower actor '{actor}' is not registered.");

    private string Animation(string actor, int animation)
    {
        NpcDatabase.NpcRecord record = Actor(actor).Record;
        return animation switch
        {
            0 => record.UpAnimation,
            1 => record.RightAnimation,
            2 => record.DownAnimation,
            3 => record.LeftAnimation,
            _ => throw new InvalidOperationException(
                $"Pre-Black Tower actor '{actor}' has no animation ${animation:x2}.")
        };
    }

    private void SpawnEffect(Vector2 position, int frames)
    {
        NpcCharacter effect = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _database.CreateEffectRecord(
                _record.Group,
                _record.Room,
                Mathf.RoundToInt(position.Y),
                Mathf.RoundToInt(position.X)),
            $"PreBlackTowerExclamation{_effectSerial++}"));
        effect.Position = position;
        effect.SetScriptAnimation(_record.EffectAnimation);
        _effects.Add(new TimedEffect(effect, frames));
    }

    private void UpdateEffects()
    {
        for (int index = _effects.Count - 1; index >= 0; index--)
        {
            TimedEffect effect = _effects[index];
            int frames = effect.Frames - 1;
            if (frames > 0)
            {
                _effects[index] = effect with { Frames = frames };
                continue;
            }
            effect.Actor.SetActive(false);
            _effects.RemoveAt(index);
        }
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value is "Ralph" or "Impa" or "Nayru" or "Zelda";

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects ${value:x2}");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) => binding switch
    {
        "SharedSignal" => _sharedSignal == value,
        "RalphSubstate" => _ralphSubstate == value,
        "ImpaSubstate" => _impaSubstate == value,
        "ImpaVar38" => _impaVar38 == value,
        _ when binding.StartsWith("SharedBit", StringComparison.Ordinal) &&
            int.TryParse(binding.AsSpan("SharedBit".Length), out int bit) =>
                ((_sharedBits >> bit) & 1) == value,
        _ => throw Unsupported($"read '{binding}'=${value:x2}")
    };

    void ICutsceneCommandHost.ShowText(int textId, string message) =>
        _context.ShowDialogue(message);

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) => Actor(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) => Actor(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) => throw Unsupported("set collision radii");

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw Unsupported("set A-button sensitivity");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        Vector2 precise = _precisePositions[actor] +
            OracleObjectMath.StrictCardinalVector(angle) * (speed / 40.0f);
        precise = new Vector2(Wrap(precise.X), Wrap(precise.Y));
        _precisePositions[actor] = precise;
        Actor(actor).Position = OracleObjectMath.ToPixelPosition(precise);
    }

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        Actor(actor).SetScriptDrawOffset(new Vector2(0, zFixed >> 8));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        Actor(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "SharedSignal":
                _sharedSignal = value;
                break;
            case "PlayerDirection":
                _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Direction(value));
                break;
            case "ToggleSharedBit":
                _sharedBits ^= value;
                break;
            default:
                throw Unsupported($"write '{binding}'=${value:x2}");
        }
    }

    void ICutsceneCommandHost.SetGlobalFlag(int flag)
    {
        if (flag != _record.RalphEnteredFlag)
            throw Unsupported($"set global flag ${flag:x2}");
        _context.Rooms.SaveData.SetGlobalFlag(flag);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw Unsupported($"OR room flag ${flag:x2}");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "CreateLinkedExclamation":
                SpawnEffect(Actor("Ralph").Position + new Vector2(13, -13), 0x1e);
                break;
            case "SpawnNayru09":
                SpawnNayru09();
                break;
            case "BeginImpaJump":
                BeginImpaJump();
                break;
            default:
                throw Unsupported($"run native handler '{handler}'");
        }
    }

    bool ICutsceneCommandHost.UpdateNativeHandler(
        string handler,
        CutsceneActorId? actor,
        int commandUpdate,
        int frames,
        string payload) => throw Unsupported($"block in native handler '{handler}'");

    void ICutsceneCommandHost.ScriptEnded()
    {
    }

    private void SpawnNayru09()
    {
        NpcDatabase.NpcRecord linked = _actors.TryGetValue("Nayru", out NpcCharacter? placed)
            ? placed.Record
            : FindPlacedNayruRecord();
        NpcDatabase.NpcRecord record = linked with
        {
            SubId = _record.NayruSpawnedSubId,
            Y = 0xf8,
            X = 0x48,
            DefaultAnimation = 2,
            UpAnimation = linked.LeftAnimation,
            RightAnimation = linked.LeftAnimation,
            DownAnimation = linked.LeftAnimation,
            LeftAnimation = linked.LeftAnimation,
            TextId = 0,
            Message = string.Empty
        };
        NpcCharacter nayru = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            record,
            "PreBlackTowerNayru09"));
        _actors["Nayru"] = nayru;
        _precisePositions["Nayru"] = nayru.Position;
        _nayruRunner.Start(_database.NayruUnlinked);
    }

    private NpcDatabase.NpcRecord FindPlacedNayruRecord()
    {
        foreach (NpcCharacter npc in _context.Entities.Entities<NpcCharacter>())
        {
            if (npc.Record.Id == _record.NayruId &&
                npc.Record.SubId == _record.NayruLinkedSubId)
            {
                return npc.Record;
            }
        }
        throw new InvalidOperationException(
            "Room 1:75 did not provide the placed Nayru graphics template.");
    }

    private void BeginImpaJump()
    {
        _impaZFixed = 0;
        _impaSpeedZ = -0x180;
        _impaAirborne = true;
    }

    private static float Wrap(float value)
    {
        float whole = Mathf.Floor(value);
        float fraction = value - whole;
        return Mathf.PosMod((int)whole, 0x100) + fraction;
    }

    private static Vector2I Direction(int value) => value switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported pre-Black Tower Link direction ${value:x2}.")
    };

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Pre-Black Tower command stream cannot {operation}.");

}

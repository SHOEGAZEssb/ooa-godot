using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Runs INTERAC_RAFTWRECK_CUTSCENE $9b:$00 in room $1:$a8.</summary>
internal sealed class RaftwreckEvent : CutsceneCommandHost, IRoomEntryEvent,
    ICutsceneCommandHost
{
    private const string Actor = "Raftwreck";
    private const string StateBinding = "wTmpcfc0.genericCutscene.state";
    private readonly RoomEventContext _context;
    private readonly RaftwreckEventDatabase _database = new();
    private readonly CutsceneCommandRunner _runner;
    private readonly List<HelperState> _helpers = [];
    private readonly List<InteractionEffectState> _interactionEffects = [];
    private readonly List<LightningState> _lightningParts = [];
    private OracleRoomData? _room;
    private RaftRoomEntity? _raft;
    private Vector2 _position;
    private int _direction;
    private int _centerCounter;
    private int _state;
    private int _flashPhase;
    private int _flashFrame;
    private int _stateCounter;
    private int _paletteOffset;
    private int _finishCounter;
    private bool _paletteFadeActive;
    private bool _oscillating;
    private bool _initializing;
    private bool _scriptEnded;
    private bool _active;
    private bool _ownsFlashFade;
    private bool _secondFlashQueued;
    private int _effectSerial;

    internal RaftwreckEvent(RoomEventContext context)
    {
        _context = context;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _active;
    public bool BlocksGameplay => HasState;
    internal bool MenusDisabled => HasState;
    internal int State => _state;
    internal int Counter => _runner.Counter;
    internal int FlashFrame => _flashFrame;
    internal int FlashPhase => _flashPhase;
    internal int HelperCount => _helpers.Count;
    internal int WindCount => _interactionEffects.Count(effect => !effect.Debris);
    internal int LightningCount => _lightningParts.Count;
    internal int CenterCounter => _centerCounter;
    internal Vector2 PrecisePosition => _position;
    internal int Direction => _direction;
    internal int PaletteOffset => _paletteOffset;
    internal RaftwreckEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _database.Record.Group && room.Id == _database.Record.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        RaftwreckEventRecord record = _database.Record;
        if (_context.Rooms.SaveData.HasRoomFlag(
            record.Group, record.Room, record.RoomFlag))
            return;

        _room = room;
        _raft = _context.Entities.Entities<RaftRoomEntity>()
            .Concat(_context.Entities.OutgoingEntities<RaftRoomEntity>())
            .SingleOrDefault(entity => entity.LinkRiding);
        float ownerX = _raft?.PrecisePosition.X ?? _context.Player.PrecisePosition.X;
        _position = new Vector2(Mathf.Floor(ownerX), _database.Record.InitialY);
        _context.Player.BeginCutsceneControl();
        _active = true;
        _initializing = true;
    }

    public void UpdateFrame()
    {
        AdvanceLightningParts();
        if (_initializing)
        {
            Initialize();
            return;
        }
        if (_centerCounter > 0)
        {
            _centerCounter--;
            OracleObjectMovement.Shared.ApplySpeed(
                ref _position, _database.Record.InitialSpeed,
                _position.X < _database.Record.CenterX ? 0x08 : 0x18);
            SynchronizeOwner();
            return;
        }
        if (!_runner.Active && !_scriptEnded)
        {
            _runner.Start(_database.Commands);
            _runner.SetInitialMotionRegisters(
                new CutsceneActorId(Actor), _database.Record.InitialSpeed, 0);
        }

        if (_runner.Active)
            _runner.AdvanceFrame();
        AdvancePaletteFade();
        AdvanceScreenFlashes();
        AdvanceFinish();
        if (!HasState)
            return;
        if (_flashPhase >= 5 && _finishCounter == 0 && _state != 3)
            AdvanceOscillation();
        AdvanceHelpers();
        AdvanceInteractionEffects();
        SynchronizeOwner();
    }

    public void Cancel()
    {
        bool releaseControl = _active;
        foreach (InteractionEffectState effect in _interactionEffects)
            effect.Actor.SetActive(false);
        foreach (LightningState part in _lightningParts)
            part.Actor.SetActive(false);
        _interactionEffects.Clear();
        _lightningParts.Clear();
        _helpers.Clear();
        _runner.Clear();
        if (releaseControl)
        {
            _raft?.CancelRaftwreckControl(_context.Player);
            _context.Player.EndCutsceneControl();
        }
        _room?.ClearTemporaryBackgroundPalette(_context.AnimationTick());
        _room = null;
        _raft = null;
        _initializing = false;
        _centerCounter = 0;
        _state = 0;
        _flashPhase = 0;
        _flashFrame = 0;
        _stateCounter = 0;
        _finishCounter = 0;
        _paletteFadeActive = false;
        _oscillating = false;
        _scriptEnded = false;
        _active = false;
        _secondFlashQueued = false;
        if (_ownsFlashFade)
            _context.Fade.Color = new Color(1, 1, 1, 0);
        _ownsFlashFade = false;
    }

    private void Initialize()
    {
        _initializing = false;
        float delta = _position.X - _database.Record.CenterX;
        _direction = delta < 0 ? 1 : 3;
        _centerCounter = Mathf.Abs(Mathf.FloorToInt(delta)) * 2;
        _raft?.BeginRaftwreckControl(_context.Player, _position);
        SynchronizeOwner();
    }

    private void SynchronizeOwner()
    {
        if (_raft is null)
            _context.Player.SetRaftwreckCutscenePosition(_position, _direction);
        else
            _raft.SetRaftwreckPosition(_context.Player, _position, _direction);
    }

    private void AdvancePaletteFade()
    {
        if (!_paletteFadeActive || _room is null)
            return;
        int candidate = _paletteOffset - 1;
        if (candidate < -16)
        {
            _paletteFadeActive = false;
            return;
        }
        _paletteOffset = candidate;
        _room.SetTemporaryBackgroundPaletteOffset(candidate);
    }

    private void RestartDarkenRoom()
    {
        _paletteOffset = -15;
        _room!.SetTemporaryBackgroundPaletteOffset(_paletteOffset);
        _paletteFadeActive = true;
    }

    private void AdvanceScreenFlashes()
    {
        if (_state == 1 && _flashPhase == 0)
        {
            _flashPhase = 1;
            _flashFrame = 0;
            return;
        }
        if (_flashPhase == 2 && _secondFlashQueued)
        {
            _secondFlashQueued = false;
            _flashPhase = 3;
            _flashFrame = 0;
            return;
        }
        if (_flashPhase is 1 or 3)
        {
            _flashFrame++;
            _paletteFadeActive = false;
            bool white = _flashFrame is 1 or 2 or 5 or 6 or 9 or 10;
            _ownsFlashFade = true;
            _context.Fade.Color = new Color(1, 1, 1, white ? 1 : 0);
            if (_flashFrame < 13)
                return;
            _context.Fade.Color = new Color(1, 1, 1, 0);
            _ownsFlashFade = false;
            RestartDarkenRoom();
            _stateCounter = _flashPhase == 1
                ? _database.Record.FirstFlashWait
                : _database.Record.SecondFlashWait;
            _flashPhase++;
            return;
        }
        if (_flashPhase == 2 && _stateCounter > 0 && --_stateCounter == 0)
        {
            _flashPhase = 3;
            _flashFrame = 0;
        }
        else if (_flashPhase == 4 && _stateCounter > 0 && --_stateCounter == 0)
        {
            _flashPhase = 5;
            _state = 2;
        }
    }

    public override void PlaySound(int sound)
    {
        // raftwreckCutsceneScript_body issues its second SND_LIGHTNING before
        // waiting for genericCutscene.state $02. That command is the visual
        // synchronization point for the second flash; do not leave the flash
        // parked on the controller's defensive counter after the sound has
        // already been requested.
        if (sound == OracleSoundEngine.SndLightning && _flashPhase == 2)
            _secondFlashQueued = true;
        base.PlaySound(sound);
    }

    private void AdvanceOscillation()
    {
        if (!_oscillating || (_context.Entities.FrameCounter & 7) != 0)
            return;
        int index = (_context.Entities.FrameCounter & 0x38) >> 3;
        _position.Y += unchecked((sbyte)_database.Record.YOscillation[index]);
    }

    private void AdvanceFinish()
    {
        if (_finishCounter == 0)
        {
            if (_state != 3)
                return;
            _finishCounter = _database.Record.FinishWait;
            return;
        }
        if (--_finishCounter != 0)
            return;

        RaftwreckEventRecord record = _database.Record;
        _context.Sound.PlaySound(OracleSoundEngine.SndCtrlFastFadeOut);
        _context.Rooms.SaveData.SetRoomFlag(
            record.Group, record.Room, record.RoomFlag);
        if (_raft is null)
            _context.Player.SetRaftwreckCutscenePosition(_position, _direction);
        else
            _raft.FinishRaftwreck(_context.Player);
        _context.Player.EndCutsceneControl();
        Warp warp = new(
            record.Group, record.Room, -1, 0, 0,
            record.Group, record.DestinationRoom, record.DestinationPosition,
            record.DestinationParameter, record.DestinationTransition);
        _scriptEnded = false;
        _active = false;
        _context.Transitions.ApplyWarpWithFadeOut(_context.Player, warp);
    }

    private void SpawnHelper(int subId) =>
        _helpers.Add(new HelperState(subId, _database.Helper(subId)));

    private void AdvanceHelpers()
    {
        for (int i = 0; i < _helpers.Count; i++)
        {
            HelperState helper = _helpers[i];
            if (!helper.Initialized)
            {
                helper.Initialized = true;
                continue;
            }
            if (helper.Counter > 0)
            {
                helper.Counter--;
                continue;
            }
            if (helper.Index >= helper.Rows.Count)
            {
                _helpers.RemoveAt(i);
                i--;
                continue;
            }
            RaftwreckHelperRecord row = helper.Rows[helper.Index++];
            helper.Counter = row.Counter;
            if (helper.SubId == 5)
                SpawnLightning(row);
            else
                SpawnWind(row, helper.SubId == 3 ? 0x50 : 0x78);
            if (row.Counter == 0)
            {
                if (helper.SubId == 5)
                {
                    _state = 3;
                    // The zero-counter $64:$05 row is the terminal strike.
                    // Retire the still-blocked $ff applyspeed command before
                    // the 20-update fade handoff can carry Link past the bolt.
                    _runner.Clear();
                    _scriptEnded = true;
                }
                _helpers.RemoveAt(i);
                i--;
            }
        }
    }

    private void SpawnWind(RaftwreckHelperRecord row, int speed)
    {
        RaftwreckEffectRecord record = _database.Effect(row.EffectSubId);
        NpcCharacter actor = SpawnEffect(
            record, row.Y, row.X, "Wind", objectId: 0x64, fixedPriority: null);
        (int angle, int counter) = _database.Record.AnglePreset[0];
        _interactionEffects.Add(new InteractionEffectState(
            actor, new Vector2(row.X, row.Y), speed, angle, counter,
            duration: 0, debris: false));
    }

    private void SpawnLightning(RaftwreckHelperRecord row)
    {
        NpcCharacter actor = SpawnEffect(
            _database.Lightning, row.Y, row.X, "Lightning", objectId: 0x27,
            fixedPriority: NpcCharacter.InFrontOfLinkZIndex);
        actor.Visible = false;
        actor.SetScriptDrawOffset(new Vector2(0, unchecked((sbyte)0xc0)));
        _lightningParts.Add(new LightningState(
            actor, new Vector2(row.X, row.Y),
            _database.Record.LightningFrames[0].Duration));
    }

    private NpcCharacter SpawnEffect(
        RaftwreckEffectRecord effect,
        int y,
        int x,
        string name,
        int objectId,
        int? fixedPriority)
    {
        NpcRecord npc = new(
            1, 0xa8, objectId, effect.SubId, y, x, 0, 0,
            effect.Sprite, effect.TileBase, effect.Palette, 0, false,
            effect.Animation, effect.Animation, effect.Animation, effect.Animation,
            string.Empty, NpcImplementationClassification.EventOwned);
        NpcCharacter actor = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(npc, $"Raftwreck{name}{_effectSerial++}"));
        actor.Position = new Vector2(x, y);
        actor.SetScriptAnimation(effect.Animation);
        actor.SetAnimationRate(0.0f);
        actor.SetBlocksLink(false);
        if (fixedPriority is int zIndex)
            actor.SetFixedDrawPriority(zIndex);
        return actor;
    }

    private void AdvanceLightningParts()
    {
        for (int i = 0; i < _lightningParts.Count; i++)
        {
            LightningState part = _lightningParts[i];
            if (part.State == 0)
            {
                part.RandomOffset = _context.Entities.NextRandomValue() & 6;
                part.State = 1;
                continue;
            }
            if (part.State == 1)
            {
                part.State = 2;
                part.Actor.Visible = true;
                _context.Sound.PlaySound(OracleSoundEngine.SndLightning);
                continue;
            }

            part.Actor.AdvanceAnimationUpdates(1);
            part.FrameCounter--;
            if (part.FrameCounter == 0)
            {
                part.FrameIndex++;
                (int duration, int nextParameter) =
                    _database.Record.LightningFrames[part.FrameIndex];
                if (nextParameter == 0xff)
                {
                    part.Actor.SetActive(false);
                    _lightningParts.RemoveAt(i--);
                    continue;
                }
                part.FrameCounter = duration;
            }

            int parameter =
                _database.Record.LightningFrames[part.FrameIndex].Parameter;
            if ((parameter & 0x80) != 0)
            {
                SpawnLightningDebris(part, parameter);
                parameter &= 0x7f;
            }
            if ((parameter & 1) != 0 &&
                part.ShakeFrames.Add(part.FrameIndex))
            {
                _context.Entities.BeginScreenShake(
                    _database.Record.LightningShake);
            }
            int zIndex = (parameter & 0x70) >> 4;
            part.Actor.SetScriptDrawOffset(new Vector2(
                0, unchecked((sbyte)_database.Record.LightningZ[zIndex])));
            _context.Entities.RuntimeState.SetWramByte(0xcfd2, 0xff);
        }
    }

    private void SpawnLightningDebris(LightningState part, int parameter)
    {
        int key = parameter & 0x7f;
        if (!part.DebrisParameters.Add(key))
            return;
        int baseIndex = (key & 0x0e) - 2;
        int offsetIndex = (part.RandomOffset + baseIndex) >> 1;
        (int y, int x) = _database.Record.DebrisOffsets[offsetIndex];
        Vector2 position = part.Position + new Vector2(
            unchecked((sbyte)x), unchecked((sbyte)y));
        RaftwreckEffectRecord record = _database.Debris;
        NpcCharacter actor = SpawnEffect(
            record, Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X),
            "Debris", objectId: 0x08,
            fixedPriority: NpcCharacter.BehindLinkZIndex);
        _interactionEffects.Add(new InteractionEffectState(
            actor, position, 0, 0, 0, record.Duration, debris: true));
    }

    private void AdvanceInteractionEffects()
    {
        for (int i = 0; i < _interactionEffects.Count; i++)
        {
            InteractionEffectState effect = _interactionEffects[i];
            if (!effect.Initialized)
            {
                effect.Initialized = true;
                if (effect.Debris)
                    _context.Sound.PlaySound(OracleSoundEngine.SndKillEnemy);
                continue;
            }

            if (effect.Debris)
            {
                if (effect.AnimationUpdates >= effect.Duration)
                {
                    RemoveInteractionEffect(i--);
                    continue;
                }
                effect.Actor.AdvanceAnimationUpdates(1);
                effect.AnimationUpdates++;
                continue;
            }

            effect.Actor.AdvanceAnimationUpdates(1);
            OracleObjectMovement.Shared.ApplySpeed(
                ref effect.Position, effect.Speed, effect.Angle);
            effect.Actor.SetStatePosition(
                OracleObjectMath.ToPixelPosition(effect.Position));
            if (effect.Position.X is < 0 or >= 240)
            {
                RemoveInteractionEffect(i--);
                continue;
            }
            effect.AngleCounter--;
            if (effect.AngleCounter != 0)
                continue;
            effect.PresetIndex++;
            if (effect.PresetIndex >= _database.Record.AnglePreset.Length)
            {
                RemoveInteractionEffect(i--);
                continue;
            }
            (effect.Angle, effect.AngleCounter) =
                _database.Record.AnglePreset[effect.PresetIndex];
        }
    }

    private void RemoveInteractionEffect(int index)
    {
        _interactionEffects[index].Actor.SetActive(false);
        _interactionEffects.RemoveAt(index);
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == Actor;
    bool ICutsceneCommandHost.GateOpen(string gate) => gate == "PaletteFade"
        ? !_paletteFadeActive
        : throw new InvalidOperationException($"Unknown raftwreck gate '{gate}'.");
    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        binding == StateBinding
            ? _state == value
            : throw new InvalidOperationException($"Unknown raftwreck memory '{binding}'.");
    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle)
    {
        if (actor != Actor) throw new InvalidOperationException($"Unknown raftwreck actor '{actor}'.");
        OracleObjectMovement.Shared.ApplySpeed(ref _position, speed, angle);
    }
    void ICutsceneCommandHost.WriteObjectByte(string actor, int address, int value)
    {
        if (actor != Actor || address != 0x38 || value is not (0 or 1))
            throw new InvalidOperationException($"Unsupported raftwreck object write {actor}.${address:x2}=${value:x2}.");
        _oscillating = value != 0;
    }
    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding == StateBinding && value == 1) _state = value;
        else if (binding is "hSprPaletteSources" or "hDirtySprPalettes" && value == 0) { }
        else throw new InvalidOperationException($"Unsupported raftwreck memory write '{binding}'=${value:x2}.");
    }
    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SetLinkUp": _direction = 0; break;
            case "SetLinkRight": _direction = 1; break;
            case "DarkenRoom":
                _paletteOffset = 0;
                _room!.SetTemporaryBackgroundPaletteOffset(0);
                _paletteFadeActive = true;
                break;
            case "SpawnHelper03": SpawnHelper(3); break;
            case "SpawnHelper04": SpawnHelper(4); break;
            case "SpawnHelper05": SpawnHelper(5); break;
            default: throw new InvalidOperationException($"Unknown raftwreck native handler '{handler}'.");
        }
    }
    void ICutsceneCommandHost.ScriptEnded() => _scriptEnded = true;

    private sealed class HelperState(int subId, IReadOnlyList<RaftwreckHelperRecord> rows)
    {
        internal int SubId { get; } = subId;
        internal IReadOnlyList<RaftwreckHelperRecord> Rows { get; } = rows;
        internal bool Initialized;
        internal int Index;
        internal int Counter;
    }

    private sealed class InteractionEffectState(
        NpcCharacter actor, Vector2 position, int speed, int angle,
        int angleCounter, int duration, bool debris)
    {
        internal NpcCharacter Actor { get; } = actor;
        internal Vector2 Position = position;
        internal int Speed { get; } = speed;
        internal int Angle = angle;
        internal int AngleCounter = angleCounter;
        internal int Duration { get; } = duration;
        internal bool Debris { get; } = debris;
        internal bool Initialized;
        internal int AnimationUpdates;
        internal int PresetIndex;
    }

    private sealed class LightningState(
        NpcCharacter actor, Vector2 position, int initialFrameCounter)
    {
        internal NpcCharacter Actor { get; } = actor;
        internal Vector2 Position { get; } = position;
        internal HashSet<int> DebrisParameters { get; } = [];
        internal HashSet<int> ShakeFrames { get; } = [];
        internal int State;
        internal int RandomOffset;
        internal int FrameIndex;
        internal int FrameCounter = initialFrameCounter;
    }
}

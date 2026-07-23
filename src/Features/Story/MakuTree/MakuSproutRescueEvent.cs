using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Full room $1:$38 Maku Sprout rescue: four synchronized interaction-script
/// owners, dynamic masked-Moblin combat, and the four-step opening gate.
/// </summary>
internal sealed class MakuSproutRescueEvent : IRoomEntryEvent, ICutsceneCommandHost
{

    private readonly RoomEventContext _context;
    private readonly MakuSproutRescueDatabase _database = new();
    private readonly MakuSproutRescueDatabaseEventRecord _record;
    private readonly CutsceneCommandRunner _sproutRunner;
    private readonly CutsceneCommandRunner _controllerRunner;
    private readonly CutsceneCommandRunner _leftRunner;
    private readonly CutsceneCommandRunner _rightRunner;
    private NpcCharacter? _sprout;
    private NpcCharacter? _leftMoblin;
    private NpcCharacter? _rightMoblin;
    private MakuSproutRescueEventEventStage _stage;
    private int _cutsceneState;
    private int _moblinSync;
    private bool _inputEnabled;
    private bool _buttonSensitive;
    private bool _screenTransitionsDisabled;
    private bool _playerMoveComplete;
    private bool _controllerEnded;
    private bool _gateActive;
    private bool _gateOpen;
    private int _gatePhase;
    private int _gateCounter;
    private byte _closedGateTile;
    private int _shakeCounter;

    public MakuSproutRescueEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _sproutRunner = new CutsceneCommandRunner(this);
        _controllerRunner = new CutsceneCommandRunner(this);
        _leftRunner = new CutsceneCommandRunner(this);
        _rightRunner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _stage is MakuSproutRescueEventEventStage.Running or MakuSproutRescueEventEventStage.NpcLoop;
    public bool BlocksGameplay => HasState && !_inputEnabled;
    internal bool ScreenTransitionsDisabled => _screenTransitionsDisabled;
    internal MakuSproutRescueEventEventStage Stage => _stage;
    internal int CutsceneState => _cutsceneState;
    internal int GatePhase => _gatePhase;
    internal bool GateOpen => _gateOpen;
    internal MakuSproutRescueDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room)
    {
        OracleSaveData save = _context.Rooms.SaveData;
        return group == _record.Group && room.Id == _record.Room &&
            save.MakuTreeState >= _record.StateMin &&
            save.MakuTreeState <= _record.StateMax &&
            !save.HasGlobalFlag(_record.SavedFlag);
    }

    public void Start(OracleRoomData _)
    {
        Cancel();
        _sprout = _context.RequireNpc(
            _record.Group, _record.Room, _record.SproutId, _record.SproutSubId,
            "INTERAC_MAKU_SPROUT");
        _stage = MakuSproutRescueEventEventStage.Running;
        _inputEnabled = true;
        _sproutRunner.Start(_database.Sprout);

        // CUTSCENE_LOADING_ROOM continues updating interactions beneath a
        // warp fade, so the following setanimation $02 has already selected
        // the distressed sprout before the destination becomes visible. Our
        // room-event clock is frozen for the host transition; stage that same
        // imported visual without consuming the runner's command or wait.
        _sprout.SetScriptAnimation(_database.FearfulSproutAnimation);

        // INTERAC_MAKU_SPROUT $88:$01 calls interactionRunScript from its
        // state-0 initializer. That update creates $6b:$04; because the new
        // controller and its two $96 Moblins occupy later interaction slots,
        // they also run their state-0 work before the room is presented. Do
        // this synchronously while a warp destination is still fully faded or
        // a scrolling destination is still at its first hidden edge. The
        // controller stops at the newly-installed 60-update wait, so ordinary
        // event time remains frozen for the rest of the transition.
        UpdateFrame();
    }

    internal void RestoreCompletedSprout(int group, OracleRoomData room)
    {
        if (group != _record.Group || room.Id != _record.Room)
            return;
        OracleSaveData save = _context.Rooms.SaveData;
        if (save.MakuTreeState < _record.StateMin ||
            save.MakuTreeState > _record.StateMax ||
            !save.HasGlobalFlag(_record.SavedFlag))
        {
            return;
        }
        NpcCharacter sprout = _context.RequireNpc(
            group, room.Id, _record.SproutId, _record.SproutSubId,
            "saved INTERAC_MAKU_SPROUT");
        ConfigureSavedSprout(sprout);
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_buttonSensitive || _sprout is null || !ReferenceEquals(npc, _sprout))
            return false;
        _context.ShowDialogue(_record.PostText);
        return true;
    }

    public void UpdateFrame()
    {
        if (!HasState)
            return;

        // Object order is sprout -> controller -> its two dynamically-created
        // Moblins -> gate opener. A lane created by an earlier owner therefore
        // receives its first update in this same interaction pass.
        _sproutRunner.AdvanceFrame();
        _controllerRunner.AdvanceFrame();
        _leftRunner.AdvanceFrame();
        _rightRunner.AdvanceFrame();

        _leftMoblin?.PreventPlayerPassing(_context.Player);
        _rightMoblin?.PreventPlayerPassing(_context.Player);
        _leftMoblin?.UpdateDrawPriority(_context.Player.Position);
        _rightMoblin?.UpdateDrawPriority(_context.Player.Position);

        if (_gateActive)
            UpdateGateOpening();
        UpdateScreenShake();

        if (_controllerEnded)
        {
            _controllerEnded = false;
            _stage = MakuSproutRescueEventEventStage.Completed;
            _sprout = null;
        }
    }

    public void Cancel()
    {
        _sproutRunner.Clear();
        _controllerRunner.Clear();
        _leftRunner.Clear();
        _rightRunner.Clear();
        if (!_inputEnabled)
            _context.Player.EndCutsceneControl();
        _context.RoomCamera.Offset = Vector2.Zero;
        _sprout = null;
        _leftMoblin = null;
        _rightMoblin = null;
        _stage = MakuSproutRescueEventEventStage.Inactive;
        _cutsceneState = 0;
        _moblinSync = 0;
        _inputEnabled = true;
        _buttonSensitive = false;
        _screenTransitionsDisabled = false;
        _playerMoveComplete = false;
        _controllerEnded = false;
        _gateActive = false;
        _gateOpen = false;
        _gatePhase = 0;
        _gateCounter = 0;
        _shakeCounter = 0;
    }

    private void ConfigureSavedSprout(NpcCharacter sprout)
    {
        MakuSproutRescueDatabaseActorRecord actor = _database.Actors["Sprout"];
        sprout.SetScriptAnimation(actor.UpAnimation);
        sprout.SetCollisionRadii(8, 8);
        sprout.SetDialogue(_record.PostTextId, _record.PostText, canFace: false);
    }

    private NpcCharacter SpawnActor(string actorName)
    {
        MakuSproutRescueDatabaseActorRecord actor = _database.Actors[actorName];
        return _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            actor.ToNpcRecord(_record.Group, _record.Room), actorName,
            Talkable: false, Solid: true));
    }

    private static Vector2 PackedCenter(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);

    private void UpdateGateOpening()
    {
        if (_gatePhase == 0)
        {
            OracleRoomData room = _context.Rooms.CurrentRoom;
            _closedGateTile = room.GetMetatile(PackedCenter(_record.GateInnerLeft));
            SetInterleaved(_record.GateInnerLeft, 3);
            SetInterleaved(_record.GateInnerRight, 1);
            GateBurst(0x74, 0x48, 0x58);
            _gateCounter = _record.GateCounter;
            _gatePhase = 1;
            return;
        }
        if (--_gateCounter != 0)
            return;

        switch (_gatePhase)
        {
            case 1:
                SetOrdinaryGateTile(_record.GateInnerLeft);
                SetOrdinaryGateTile(_record.GateInnerRight);
                GateBurst(0x74, 0x40, 0x60);
                _gateCounter = _record.GateCounter;
                _gatePhase = 2;
                break;
            case 2:
                SetInterleaved(_record.GateLeft, 3);
                SetInterleaved(_record.GateRight, 1);
                GateBurst(0x74, 0x38, 0x68);
                _gateCounter = _record.GateCounter;
                _gatePhase = 3;
                break;
            case 3:
                SetOrdinaryGateTile(_record.GateLeft);
                SetOrdinaryGateTile(_record.GateRight);
                GateBurst(0x74, 0x30, 0x70);
                _context.Rooms.SaveData.SetRoomFlag(
                    _record.Group, _record.Room, (byte)_record.RoomFlag);
                _gateActive = false;
                _gateOpen = true;
                _gatePhase = 4;
                break;
        }
    }

    private void SetInterleaved(int packed, int type) =>
        _context.Rooms.CurrentRoom.SetInterleavedMetatile(
            PackedCenter(packed), (byte)_record.ClearTile, _closedGateTile,
            type, _context.AnimationTick());

    private void SetOrdinaryGateTile(int packed) =>
        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
            PackedCenter(packed), (byte)_record.ClearTile, null,
            _context.AnimationTick());

    private void GateBurst(int y, int x1, int x2)
    {
        _context.Sound.PlaySound(OracleSoundEngine.SndKillEnemy);
        foreach (Vector2 position in new[]
        {
            new Vector2(x1, y), new Vector2(x2, y),
            new Vector2(x1, y + 8), new Vector2(x2, y + 8)
        })
        {
            _context.Entities.Spawn<PuzzlePuffEffect>(
                new PuzzlePuffSpawn(position, OracleSoundEngine.SndPoof));
        }
        _shakeCounter = _record.ShakeCounter;
        _context.Sound.PlaySound(OracleSoundEngine.SndDoorClose);
    }

    private void UpdateScreenShake()
    {
        if (_shakeCounter <= 0)
        {
            _context.RoomCamera.Offset = Vector2.Zero;
            return;
        }
        int[] amounts = [-2, -1, 1, 2];
        _context.RoomCamera.Offset = new Vector2(
            amounts[_context.Entities.NextRandomValue() & 3],
            amounts[_context.Entities.NextRandomValue() & 3]);
        _shakeCounter--;
        if (_shakeCounter == 0)
            _context.RoomCamera.Offset = Vector2.Zero;
    }

    private NpcCharacter Actor(string name) => name switch
    {
        "Sprout" when _sprout is not null => _sprout,
        "MoblinLeft" when _leftMoblin is not null => _leftMoblin,
        "MoblinRight" when _rightMoblin is not null => _rightMoblin,
        _ => throw Unsupported($"resolve actor '{name}'")
    };

    private static Vector2I DirectionToward(Vector2 origin, Vector2 target)
    {
        int angle = (OracleObjectMath.AngleToward(origin, target) + 4) & 0x18;
        return angle switch
        {
            0 => Vector2I.Up,
            8 => Vector2I.Right,
            16 => Vector2I.Down,
            _ => Vector2I.Left
        };
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value is "Sprout" or "MoblinLeft" or "MoblinRight";

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (enabled)
            _context.Player.EndCutsceneControl();
        else
            _context.Player.BeginCutsceneControl();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");
    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects=${value:x2}");
    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        binding switch
        {
            "CutsceneState" => _cutsceneState == value,
            "MoblinSync" => _moblinSync == value,
            "RoomEnemyCount" => _context.Entities.RoomEnemyCount == value,
            "PlayerMoveComplete" => (_playerMoveComplete ? 1 : 0) == value,
            "RoomGateOpen" => (_gateOpen ? 1 : 0) == value,
            _ => throw Unsupported($"read '{binding}'=${value:x2}")
        };

    void ICutsceneCommandHost.ShowText(int textId, string message) =>
        _context.ShowDialogue(
            message,
            textId == 0x05d4 ? _record.FinalTextPosition : null);

    void ICutsceneCommandHost.SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        Actor(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation) =>
        Actor(actor).SetScriptAnimation(encodedAnimation);

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        Actor(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        if (actor != "Sprout")
            throw Unsupported($"set {actor} A-button sensitivity");
        _buttonSensitive = true;
        ConfigureSavedSprout(Actor(actor));
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle) =>
        Actor(actor).SetStatePosition(Actor(actor).Position +
            OracleObjectMath.StrictCardinalVector(angle) * (speed / 40.0f));

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        Actor(actor).SetScriptDrawOffset(new Vector2(0, zFixed >> 8));

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        Actor(actor).Visible = visible;

    void ICutsceneCommandHost.WriteObjectByte(
        string actor, int address, int value)
    {
        if (address != 0x3f || value is not (0 or 1))
            throw Unsupported($"write {actor}.${address:x2}=${value:x2}");
        Actor(actor).SetAnimationRate(value == 0 ? 1.0f : 0.0f);
    }

    Vector2 ICutsceneCommandHost.GetActorPosition(CutsceneActorId actor) =>
        Actor(actor.Value).Position;

    void ICutsceneCommandHost.SetActorPosition(
        CutsceneActorId actor, Vector2 position, Vector2 facingDelta, Vector2 movement) =>
        Actor(actor.Value).SetStatePosition(position);

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding != "CutsceneState")
            throw Unsupported($"write '{binding}'=${value:x2}");
        _cutsceneState = value;
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SpawnController":
                _controllerRunner.Start(_database.Controller);
                break;
            case "RestartSound":
                _context.Sound.RestartSound();
                break;
            case "DisableScreenTransitions":
                _screenTransitionsDisabled = true;
                break;
            case "LoadMoblins":
                _leftMoblin = SpawnActor("MoblinLeft");
                _rightMoblin = SpawnActor("MoblinRight");
                _leftRunner.Start(_database.MoblinLeft);
                _rightRunner.Start(_database.MoblinRight);
                _moblinSync = 0;
                break;
            case "SpawnInitialPuff":
                _context.Entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(
                    new Vector2(0x28, 0x58), OracleSoundEngine.SndPoof));
                break;
            case "SetInitialGateTile":
                _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
                    PackedCenter(_record.InitialGatePosition),
                    (byte)_record.ClearTile, null, _context.AnimationTick());
                break;
            case "PlayDisasterMusic":
                _context.Sound.PlaySound(OracleSoundEngine.MusDisaster);
                break;
            case "SetLinkUp":
                _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Up);
                break;
            case "WriteMakuMapText":
                _context.Rooms.SaveData.SetMakuMapTextPast(_record.MapTextLow);
                break;
            case "IncMakuState":
                _context.Rooms.SaveData.SetMakuTreeState(
                    Math.Min(0xff, _context.Rooms.SaveData.MakuTreeState + 1));
                break;
            case "LayoutSwap":
                _context.Rooms.SaveData.SetRoomFlag(
                    0, 0x38, OracleSaveData.RoomFlagLayoutSwap, false);
                _context.Rooms.SaveData.SetRoomFlag(
                    1, 0x48, OracleSaveData.RoomFlagLayoutSwap);
                break;
            case "ResetMusic":
                _context.Sound.PlayRoomMusic(_record.Group, _record.Room);
                break;
            case "SpawnGateOpening":
                _gateActive = true;
                _gatePhase = 0;
                break;
            case "EnableScreenTransitions":
                _screenTransitionsDisabled = false;
                break;
            case "EnterNpcLoop":
                _stage = MakuSproutRescueEventEventStage.NpcLoop;
                break;
            case "FaceMoblinLeft":
                Actor("MoblinLeft").SetFacingDirection(DirectionToward(
                    Actor("MoblinLeft").Position, _context.Player.Position));
                break;
            case "FaceMoblinRight":
                Actor("MoblinRight").SetFacingDirection(DirectionToward(
                    Actor("MoblinRight").Position, _context.Player.Position));
                break;
            case "AddMoblinSync":
                _moblinSync++;
                break;
            case "IncrementCutsceneState":
                _cutsceneState++;
                break;
            case "SpawnMaskedMoblinLeft":
                SpawnMaskedMoblin(ref _leftMoblin);
                break;
            case "SpawnMaskedMoblinRight":
                SpawnMaskedMoblin(ref _rightMoblin);
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
        string payload)
    {
        switch (handler)
        {
            case "WaitForAtMostOneEnemy":
                return _context.Entities.RoomEnemyCount <= 1;
            case "WaitForLinkCollision":
                return Mathf.Abs(_context.Player.Position.Y - _record.ControllerY) <
                        _record.TriggerRadiusY + 6 &&
                    Mathf.Abs(_context.Player.Position.X - _record.ControllerX) <
                        _record.TriggerRadiusX + 6;
            case "MoveLinkToPosition":
                return MoveLinkToMaku(commandUpdate);
            case "WaitForScreenEdge":
            {
                Vector2 p = _context.Player.Position;
                return p.X < 0x14 || p.X >= 0x98 || p.Y < 0x22 || p.Y >= 0x76;
            }
            default:
                throw Unsupported($"update native handler '{handler}'");
        }
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        CutsceneCommand? command = _controllerRunner.CurrentCommand;
        if (command is CutsceneEndCommand &&
            command.Source.Label == "interaction6b_subid04Script")
            _controllerEnded = true;
    }

    private bool MoveLinkToMaku(int commandUpdate)
    {
        if (commandUpdate == 0)
        {
            _playerMoveComplete = false;
            _context.Player.BeginCutsceneControl();
            return false;
        }
        Vector2 position = _context.Player.Position;
        Vector2 movement;
        Vector2I direction;
        if (position.Y != 0x38)
        {
            direction = position.Y < 0x38 ? Vector2I.Down : Vector2I.Up;
            movement = direction;
        }
        else if (position.X != 0x50)
        {
            direction = position.X < 0x50 ? Vector2I.Right : Vector2I.Left;
            movement = direction;
        }
        else
        {
            // linkCutscene_updateAngleOnPath processes path 0's final
            // zero-distance Y=$38 waypoint before seeing $ff. That waypoint
            // deliberately changes Link's direction to DIR_UP.
            _context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Up);
            _context.Player.EndCutsceneControl();
            _playerMoveComplete = true;
            return true;
        }
        _context.Player.AdvanceCutsceneMovement(movement, direction);
        return false;
    }

    private void SpawnMaskedMoblin(ref NpcCharacter? actor)
    {
        if (actor is null)
            throw Unsupported("replace missing scripted Moblin");
        Vector2 position = actor.Position;
        actor.SetActive(false);
        actor = null;
        _context.Entities.Spawn<MaskedMoblinCharacter>(
            new MaskedMoblinSpawn(position));
    }

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Room 1:38 Maku rescue cannot {operation}.");
}

internal enum MakuSproutRescueEventEventStage
{
    Inactive,
    Running,
    NpcLoop,
    Completed
}

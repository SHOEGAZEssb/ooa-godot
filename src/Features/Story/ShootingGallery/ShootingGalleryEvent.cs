using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Room $2:$e9's placed INTERAC_SHOOTING_GALLERY $30:$00. Imported scripts
/// own the attendant, prompts, fades, rewards, and retry loop; the dynamically
/// spawned native controller owns the ten pitches.
/// </summary>
internal sealed class ShootingGalleryEvent :
    CutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost
{
    private const string ActorName = "GalleryKeeper";
    private const string PaletteFadeGate = "PaletteFade";
    private const int TreasureFlute = 0x0e;

    private readonly RoomEventContext _context;
    private readonly ShootingGalleryEventDatabase _database = new();
    private readonly ShootingGalleryEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private ShootingGalleryCharacter? _keeper;
    private OracleRoomData? _room;
    private ShootingGallerySession? _session;
    private ShootingGalleryGameController? _controller;
    private ShootingGalleryScriptKind _scriptKind;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private bool _linkDisabled;
    private bool _menusDisabled;
    private bool _retryPending;
    private int _condition;
    private int _savedEquippedB;
    private int _savedEquippedA;
    private bool _equipsSaved;
    private ShootingGalleryFadeDirection _fadeDirection;
    private int _fadeCounter;
    private bool _ownsFade;
    private Vector2 _originalFadePosition;
    private Vector2 _originalFadeSize;
    private int _originalFadeZ;
    private Color _originalFadeColor;

    internal ShootingGalleryEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _room is not null;
    public bool BlocksGameplay => _linkDisabled;
    internal bool MenusDisabled => _menusDisabled;
    internal ShootingGalleryEventDatabase Database => _database;
    internal ShootingGallerySession? Session => _session;
    internal ShootingGalleryGameController? Controller => _controller;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal ShootingGalleryScriptKind ScriptKind => _scriptKind;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _room = room;
        _keeper = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_SHOOTING_GALLERY") as ShootingGalleryCharacter ??
            throw new InvalidOperationException(
                "Room 2:e9 instantiated INTERAC_SHOOTING_GALLERY without " +
                "its specialized native actor.");
        _session = null;
        _controller = null;
        _scriptKind = ShootingGalleryScriptKind.Main;
        _buttonSensitive = false;
        _buttonPressed = false;
        _linkDisabled = false;
        _menusDisabled = false;
        _retryPending = false;
        _condition = 0;
        _equipsSaved = false;
        _fadeDirection = ShootingGalleryFadeDirection.None;
        _fadeCounter = 0;
        _runner.Clear();
        _runner.Start(_database.MainCommands);

        // interactionCode30 state 0 falls through to the script runner before
        // the native NPC animation update.
        _runner.AdvanceFrame();
        _keeper.UpdateShootingGallery(_context.Player);
    }

    public void UpdateFrame()
    {
        if (_room is null)
            return;

        if (!_runner.Active)
        {
            if (_retryPending)
            {
                _retryPending = false;
                _scriptKind = ShootingGalleryScriptKind.Main;
                _runner.Start(
                    _database.MainCommands,
                    _record.RetryCommand);
            }
            else if (_session is { PendingResult: >= 0 } session)
            {
                int result = session.PendingResult;
                session.PendingResult = -1;
                DisableLinkAndMenus();
                _scriptKind = ShootingGalleryScriptKind.Result;
                _runner.Start(_database.BuildResultCommands(result));
            }
            else if (_session is { GameComplete: true })
            {
                _scriptKind = ShootingGalleryScriptKind.Cleanup;
                _runner.Start(_database.CleanupCommands);
            }
        }

        _runner.AdvanceFrame();
        _keeper?.UpdateShootingGallery(_context.Player);
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_scriptKind != ShootingGalleryScriptKind.Main ||
            !_runner.Active ||
            !_buttonSensitive ||
            _linkDisabled ||
            !ReferenceEquals(npc, _keeper))
        {
            return false;
        }

        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        if (_room is not null)
        {
            RestoreEntrance();
            RemoveTargets();
        }
        if (_equipsSaved)
            RestoreEquips();
        if (_linkDisabled)
            _context.Player.EndCutsceneControl();
        RestoreFadePresentation();
        _keeper?.SetScriptButtonSensitive(false);
        _runner.Clear();
        _keeper = null;
        _room = null;
        _session = null;
        _controller = null;
        _scriptKind = ShootingGalleryScriptKind.None;
        _buttonSensitive = false;
        _buttonPressed = false;
        _linkDisabled = false;
        _menusDisabled = false;
        _retryPending = false;
        _condition = 0;
        _equipsSaved = false;
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;

    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
            EnableLinkAndMenus();
        else
            DisableLinkAndMenus();
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled} independently");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set wDisabledObjects=${value:x2} directly");

    bool ICutsceneCommandHost.GateOpen(string gate)
    {
        if (gate != PaletteFadeGate ||
            _fadeDirection == ShootingGalleryFadeDirection.None)
        {
            throw Unsupported($"read gate '{gate}'");
        }

        _fadeCounter++;
        float progress = Math.Clamp(
            _fadeCounter / (float)_record.FadeFrames,
            0.0f,
            1.0f);
        _context.Fade.Color = new Color(
            1.0f,
            1.0f,
            1.0f,
            _fadeDirection == ShootingGalleryFadeDirection.Out
                ? progress
                : 1.0f - progress);
        if (_fadeCounter < _record.FadeFrames)
            return false;

        if (_fadeDirection == ShootingGalleryFadeDirection.In)
            RestoreFadePresentation();
        _fadeDirection = ShootingGalleryFadeDirection.None;
        _fadeCounter = 0;
        return true;
    }

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    int ICutsceneCommandHost.ReadMemory(string binding) =>
        ReadMemory(binding);

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "Shooting-gallery choice closed without a text-option result.");
        }
        return choice == value;
    }

    bool ICutsceneCommandHost.TryConsumeActorButton(CutsceneActorId actor)
    {
        RequireKeeper(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        string resolved = message.Replace(
            "\\num1",
            (_session?.Score ?? _record.Cost).ToString(),
            StringComparison.Ordinal);
        if (textId is 0x0800 or 0x0801 or 0x0804 or 0x081a)
            _context.ShowChoiceDialogue(resolved);
        else
            _context.ShowDialogue(resolved);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation) =>
        throw Unsupported(
            $"set actor '{actor}' animation ${animation:x2} ({encodedAnimation})");

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw Unsupported(
            $"set actor '{actor}' movement animation ${angle:x2} ({encodedAnimation})");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        RequireKeeper(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        RequireKeeper(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        throw Unsupported($"move actor '{actor}' at ${speed:x2}/${angle:x2}");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set actor '{actor}' Z to ${zFixed:x4}");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireKeeper(actor).Visible = visible;

    void ICutsceneCommandHost.WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        RequireKeeper(actor);
        if (address != 0x31 || value != 0)
        {
            throw Unsupported(
                $"write actor '{actor}' byte ${address:x2}=${value:x2}");
        }
        _buttonPressed = false;
    }

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw Unsupported($"write '{binding}'=${value:x2}");

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        string treasureObject;
        int objectParameter;
        if (treasureId == TreasureFlute && parameter == 0)
        {
            treasureObject = _record.FluteObject;
            objectParameter = _record.FluteObjectParameter;
        }
        else if (treasureId == TreasureDatabase.TreasureGashaSeed &&
            parameter == 0)
        {
            treasureObject = _record.GashaObject;
            objectParameter = _record.GashaObjectParameter;
        }
        else
        {
            throw Unsupported(
                $"give treasure ${treasureId:x2}:${parameter:x2}");
        }

        _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            treasureObject,
            "shootingGalleryScript_humanNpc_gameDone",
            objectParameter: objectParameter);
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music != _record.MinigameMusic)
            throw Unsupported($"set music ${music:x2}");
        _context.Sound.PlayMusicIfChanged(music);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw Unsupported($"OR room flag ${flag:x2}");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "CheckRupees10":
                _condition = _context.Inventory.Rupees >= _record.Cost ? 1 : 0;
                break;
            case "RemoveRupees10":
                if (_context.Inventory.Rupees < _record.Cost)
                {
                    throw new InvalidOperationException(
                        "Shooting-gallery rupee debit followed a failed check.");
                }
                _context.Inventory.AddRupees(-_record.Cost);
                break;
            case "BeginFadeOutWhite":
                BeginFade(ShootingGalleryFadeDirection.Out);
                break;
            case "EquipSword":
                EquipSword();
                break;
            case "ClearItems":
                // disableinput / BeginCutsceneControl already clears all
                // active Link parent-item actions at each source call site.
                break;
            case "InitLinkForGame":
                SetLink(
                    new Vector2(_record.ControllerX, 0x60),
                    Vector2I.Up);
                break;
            case "RemoveEntrance":
                SetEntrance(open: false);
                break;
            case "BeginFadeInWhite":
                BeginFade(ShootingGalleryFadeDirection.In);
                break;
            case "SpawnGame":
                SpawnGame();
                break;
            case "EnableAllObjects":
                EnableLinkKeepMenusDisabled();
                break;
            case "RestoreEquips":
                RestoreEquips();
                break;
            case "RestoreEntrance":
                RestoreEntrance();
                break;
            case "RemoveTargets":
                RemoveTargets();
                break;
            case "InitLinkAfterGame":
                SetLink(new Vector2(0x68, 0x68), Vector2I.Right);
                break;
            case "ResetMusic":
                _context.Sound.PlayRoomMusic(_record.Group, _record.Room);
                break;
            case "CheckNotLinked":
                _condition = _context.Rooms.SaveData.IsLinkedGame ? 0 : 1;
                break;
            case "CheckScore0":
                CheckScore(_record.RingScore);
                break;
            case "CheckScore1":
                CheckScore(_record.GashaScore);
                break;
            case "CheckScore2":
                CheckScore(_record.RupeeScore);
                break;
            case "CheckScore3":
                CheckScore(_record.HeartScore);
                break;
            case "GiveRandomRing":
                _context.Inventory.GiveUnappraisedRing(
                    _database.Ring(_context.Entities.NextRandomValue() & 0x0f));
                break;
            case "GiveThirtyRupees":
                _context.Inventory.AddRupees(30);
                break;
            case "GiveOneHeart":
                _context.Inventory.Heal(4);
                break;
            default:
                throw Unsupported($"run native handler '{handler}'");
        }
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        switch (_scriptKind)
        {
            case ShootingGalleryScriptKind.Main:
                if (_session is null || _controller is null)
                {
                    throw new InvalidOperationException(
                        "Shooting-gallery main script ended without its game.");
                }
                break;
            case ShootingGalleryScriptKind.Result:
                if (_controller is null)
                {
                    throw new InvalidOperationException(
                        "Shooting-gallery result lost its controller.");
                }
                _controller.CompleteResultScript();
                break;
            case ShootingGalleryScriptKind.Cleanup:
                RequireSession().GameComplete = false;
                _retryPending = true;
                break;
            default:
                throw new InvalidOperationException(
                    "Shooting-gallery ended an unknown script stream.");
        }
    }

    private int ReadMemory(string binding) => binding switch
    {
        "Condition" => _condition,
        "HasFlute" => _context.Inventory.HasTreasure(TreasureFlute) ? 1 : 0,
        "CanBuyFlute" => _context.Rooms.SaveData.HasGlobalFlag(
            _record.CanBuyFluteFlag) ? 1 : 0,
        "FinalRound" => RequireSession().Round == _record.Rounds ? 1 : 0,
        _ => throw Unsupported($"read memory binding '{binding}'")
    };

    private void DisableLinkAndMenus()
    {
        if (!_linkDisabled)
            _context.Player.BeginCutsceneControl();
        _linkDisabled = true;
        _menusDisabled = true;
    }

    private void EnableLinkAndMenus()
    {
        if (_linkDisabled)
            _context.Player.EndCutsceneControl();
        _linkDisabled = false;
        _menusDisabled = false;
    }

    private void EnableLinkKeepMenusDisabled()
    {
        if (_linkDisabled)
            _context.Player.EndCutsceneControl();
        _linkDisabled = false;
        _menusDisabled = true;
    }

    private void EquipSword()
    {
        if (_equipsSaved)
        {
            throw new InvalidOperationException(
                "Shooting-gallery equips were saved twice.");
        }
        _savedEquippedB = _context.Inventory.EquippedB;
        _savedEquippedA = _context.Inventory.EquippedA;
        _equipsSaved = true;
        if (_savedEquippedA == TreasureDatabase.TreasureSword)
        {
            _context.Inventory.SetScriptedEquippedItems(
                InventoryState.ItemNone,
                TreasureDatabase.TreasureSword);
        }
        else
        {
            _context.Inventory.SetScriptedEquippedItems(
                TreasureDatabase.TreasureSword,
                InventoryState.ItemNone);
        }
    }

    private void RestoreEquips()
    {
        if (!_equipsSaved)
            return;
        _context.Inventory.SetScriptedEquippedItems(
            _savedEquippedB,
            _savedEquippedA);
        _equipsSaved = false;
    }

    private void SetLink(Vector2 position, Vector2I facing)
    {
        _context.Player.SetScriptedPosition(position);
        _context.Player.AdvanceCutsceneMovement(Vector2.Zero, facing);
    }

    private void SpawnGame()
    {
        if (_session is not null)
        {
            throw new InvalidOperationException(
                "Shooting-gallery spawned a second active controller.");
        }
        _session = new ShootingGallerySession();
        _controller = _context.Entities.Spawn<ShootingGalleryGameController>(
            new ShootingGalleryGameControllerSpawn(_session));
    }

    private void CheckScore(int threshold) =>
        _condition = RequireSession().Score >= threshold ? 1 : 0;

    private void SetEntrance(bool open)
    {
        SetPackedTile(
            _record.EntrancePosition0,
            open ? _record.EntranceOpenTile0 : _record.EntranceClosedTile0);
        SetPackedTile(
            _record.EntrancePosition1,
            open ? _record.EntranceOpenTile1 : _record.EntranceClosedTile1);
    }

    private void RestoreEntrance() => SetEntrance(open: true);

    private void RemoveTargets()
    {
        if (_room is null)
            return;
        for (int index = 0; index < _database.TargetCount; index++)
        {
            SetPackedTile(
                _database.Target(index).PackedPosition,
                _record.FloorTile);
        }
    }

    private void SetPackedTile(int packed, int tile)
    {
        if (_room is null)
            throw new InvalidOperationException("Shooting-gallery room is unavailable.");
        _room.SetPositionTileAndCollision(
            new Vector2(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8),
            (byte)tile,
            null,
            _context.AnimationTick());
    }

    private ShootingGallerySession RequireSession() =>
        _session ?? throw new InvalidOperationException(
            "Shooting-gallery session is unavailable.");

    private ShootingGalleryCharacter RequireKeeper(string actor)
    {
        if (actor != ActorName || _keeper is null)
            throw new InvalidOperationException(
                $"Unknown shooting-gallery actor '{actor}'.");
        return _keeper;
    }

    private void BeginFade(ShootingGalleryFadeDirection direction)
    {
        OwnFadePresentation();
        _fadeDirection = direction;
        _fadeCounter = 0;
        _context.Fade.Color = new Color(
            1.0f,
            1.0f,
            1.0f,
            direction == ShootingGalleryFadeDirection.Out ? 0.0f : 1.0f);
    }

    private void OwnFadePresentation()
    {
        if (_ownsFade)
            return;
        _ownsFade = true;
        _originalFadePosition = _context.Fade.Position;
        _originalFadeSize = _context.Fade.Size;
        _originalFadeZ = _context.Fade.ZIndex;
        _originalFadeColor = _context.Fade.Color;
        _context.Fade.Position = Vector2.Zero;
        _context.Fade.Size = new Vector2(
            OracleRoomData.ViewportWidth,
            OracleRoomData.ScreenHeight);
        _context.Fade.ZIndex = _context.Hud.ZIndex + 1;
    }

    private void RestoreFadePresentation()
    {
        _fadeDirection = ShootingGalleryFadeDirection.None;
        _fadeCounter = 0;
        if (!_ownsFade)
            return;
        _context.Fade.Position = _originalFadePosition;
        _context.Fade.Size = _originalFadeSize;
        _context.Fade.ZIndex = _originalFadeZ;
        _context.Fade.Color = _originalFadeColor;
        _ownsFade = false;
    }

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Room 2:e9 shooting-gallery script cannot {operation}.");
}

internal enum ShootingGalleryScriptKind
{
    None,
    Main,
    Result,
    Cleanup
}

internal enum ShootingGalleryFadeDirection
{
    None,
    Out,
    In
}

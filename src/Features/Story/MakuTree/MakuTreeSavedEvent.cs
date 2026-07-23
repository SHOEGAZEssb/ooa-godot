using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Adult Maku Tree conversation and Seed Satchel reward selected by
/// wMakuTreeState=$02 in present room $0:$38.
/// </summary>
internal sealed class MakuTreeSavedEvent : IRoomEntryEvent, ICutsceneCommandHost
{
    private const string MakuTreeActor = "MakuTree";
    private const string MapTextBinding = "wMakuMapTextPresent";
    private readonly RoomEventContext _context;
    private readonly MakuTreeSavedDatabase _database = new();
    private readonly SavedEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _makuTree;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private bool _inputDisabled;

    public MakuTreeSavedEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => _inputDisabled;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _buttonSensitive;
    internal MakuTreeSavedDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        _context.Rooms.SaveData.MakuTreeState == 2;

    public void Start(OracleRoomData room)
    {
        _runner.Clear();
        _makuTree = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MAKU_TREE");
        _makuTree.AppendScriptGraphics(_record.ExtraSprite);
        _makuTree.SetScriptAnimation(_record.Animation0);
        _makuTree.SetAnimationRate(0.0f);
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputDisabled = false;
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        _runner.AdvanceFrame();
        _makuTree?.AdvanceAnimationUpdates(1);
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_runner.Active || !_buttonSensitive || _inputDisabled ||
            !ReferenceEquals(npc, _makuTree))
        {
            return false;
        }
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        if (_inputDisabled)
            _context.Player.EndCutsceneControl();
        if (_makuTree is not null)
        {
            _makuTree.SetScriptButtonSensitive(false);
            _makuTree.SetAnimationRate(1.0f);
        }
        _makuTree = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputDisabled = false;
        _runner.Clear();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == MakuTreeActor;

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_inputDisabled)
                _context.Player.EndCutsceneControl();
            _inputDisabled = false;
        }
        else
        {
            if (!_inputDisabled)
                _context.Player.BeginCutsceneControl();
            _inputDisabled = true;
        }
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree script does not set menu enabled={enabled} independently.");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree script does not set wDisabledObjects=${value:x2}.");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree script has no gate named '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree script cannot read '{binding}'=${value:x2}.");

    bool ICutsceneCommandHost.RoomFlagSet(int flag) =>
        (_context.Rooms.SaveData.GetRoomFlags(_record.Group, _record.Room) & flag) != 0;

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "Saved Maku Tree text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    bool ICutsceneCommandHost.TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequireMakuTree(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if ((textId is < 0x0542 or > 0x0550) && textId != 0x0561)
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree command stream requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x054a)
            _context.ShowChoiceDialogue(message, textboxPosition: _record.TextboxPosition);
        else
            _context.ShowDialogue(message, _record.TextboxPosition);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree animation ${animation:x2} payload diverged from metadata.");
        }
        RequireMakuTree(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree actor '{actor}' cannot use movement animation ${angle:x2}.");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireMakuTree(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        _ = RequireMakuTree(actor);
        _buttonSensitive = true;
        _makuTree!.SetScriptButtonSensitive(true);
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree actor '{actor}' cannot move at ${speed:x2}/${angle:x2}.");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree actor '{actor}' cannot set Z to ${zFixed:x4}.");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireMakuTree(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding != MapTextBinding || value != _record.MapTextLow)
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree script cannot write '{binding}'=${value:x2}.");
        }
        _context.Rooms.SaveData.SetMakuMapTextPresent(value);
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music != _record.Music)
            throw new InvalidOperationException($"Unexpected Maku Tree music ${music:x2}.");
        _context.Sound.PlaySound(music);
    }

    void ICutsceneCommandHost.SetGlobalFlag(int flag)
    {
        if (flag != _record.AdviceFlag)
            throw new InvalidOperationException($"Unexpected Maku Tree flag ${flag:x2}.");
        _context.Rooms.SaveData.SetGlobalFlag(flag);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"Saved Maku Tree typed script cannot OR room flag ${flag:x2}.");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "makuTree_checkSpawnSeedSatchel":
                CheckSpawnSeedSatchel();
                break;
            case "makuTree_dropSeedSatchel":
                DropSeedSatchel();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown saved Maku Tree native handler '{handler}'.");
        }
    }

    void ICutsceneCommandHost.ScriptEnded() =>
        throw new InvalidOperationException(
            "makuTree_subid02Script_body must remain in its NPC loop.");

    private void CheckSpawnSeedSatchel()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        byte flags = save.GetRoomFlags(_record.Group, _record.Room);
        if ((flags & OracleSaveData.RoomFlagItem) != 0 ||
            (flags & OracleSaveData.RoomFlag80) == 0)
        {
            return;
        }

        SpawnSeedSatchel(
            _record.RespawnTreasureObject,
            _record.RespawnY,
            save.MakuTreeSeedSatchelXPosition,
            spawnMode: 0);
    }

    private void DropSeedSatchel()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        if (save.HasRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80))
            return;

        int linkX = Mathf.FloorToInt(_context.Player.Position.X);
        int x = _record.DefaultX;
        if (linkX >= _record.LowerBound && linkX < _record.UpperBound)
        {
            x = linkX < _record.MiddleBound
                ? _record.LowerBandX
                : _record.UpperBandX;
        }
        save.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80);
        save.SetMakuTreeSeedSatchelXPosition(x);
        SpawnSeedSatchel(
            _record.FallingTreasureObject,
            _record.DropY,
            x,
            spawnMode: 2);
    }

    private void SpawnSeedSatchel(string objectName, int y, int x, int spawnMode)
    {
        TreasureObjectRecord treasure =
            _context.Treasures.GetObject(objectName);
        if (treasure.TreasureId != TreasureDatabase.TreasureSeedSatchel ||
            treasure.Graphic != 0x20)
        {
            throw new InvalidOperationException(
                $"{objectName} is no longer the imported Seed Satchel graphic.");
        }
        TreasureObjectVisualRecord visual =
            _context.Treasures.GetObjectVisual(treasure.Graphic);
        GroundTreasureDatabaseRecord record = new GroundTreasureDatabaseRecord(
            _record.Group,
            _record.Room,
            0,
            y,
            x,
            objectName,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            0,
            string.Empty,
            $"scriptHelper.s:{(spawnMode == 2 ? "makuTree_dropSeedSatchel" : "makuTree_checkSpawnSeedSatchel")}",
            SpawnMode: spawnMode,
            GrabMode: 1,
            SpawnDelayFrames: spawnMode == 2 ? _record.DropDelayFrames : 0,
            InitialZPixels: spawnMode == 2 ? _record.InitialZPixels : 0,
            BounceCount: spawnMode == 2 ? _record.BounceCount : 0,
            Gravity: spawnMode == 2 ? _record.Gravity : 0,
            BounceSpeed: spawnMode == 2 ? _record.BounceSpeed : 0,
            SpawnSound: spawnMode == 2 ? _record.SpawnSound : 0,
            LandingSound: spawnMode == 2 ? _record.LandingSound : 0);
        _context.Entities.Spawn<GroundTreasurePickup>(
            new GroundTreasureSpawn(record));
    }

    private NpcCharacter RequireMakuTree(string actor)
    {
        if (actor != MakuTreeActor || _makuTree is null)
            throw new InvalidOperationException($"Unknown Maku Tree command actor '{actor}'.");
        return _makuTree;
    }
}

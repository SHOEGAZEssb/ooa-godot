using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Adult Maku Tree conversation and Seed Satchel reward selected by
/// wMakuTreeState=$02 in present room $0:$38.
/// </summary>
internal sealed class MakuTreeSavedEvent :
    InteractiveInfiniteScriptHost<NpcCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private const string MakuTreeActor = "MakuTree";
    private const string MapTextBinding = "wMakuMapTextPresent";
    private readonly MakuTreeSavedDatabase _database = new();
    private readonly SavedEventRecord _record;

    public MakuTreeSavedEvent(RoomEventContext context) :
        base(context, MakuTreeActor)
    {
        _record = _database.Record;
    }

    internal MakuTreeSavedDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        Context.Rooms.SaveData.MakuTreeState == 2;

    public void Start(OracleRoomData room)
    {
        _ = room;
        NpcCharacter makuTree = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MAKU_TREE");
        makuTree.AppendScriptGraphics(_record.ExtraSprite);
        makuTree.SetScriptAnimation(_record.Animation0);
        makuTree.SetAnimationRate(0.0f);
        StartInfiniteScript(makuTree, _database.Commands);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceAnimationUpdates(1);
    }

    public void UpdateDuringDialogueFrame() =>
        ScriptActor?.AdvanceAnimationUpdates(1);

    bool ICutsceneCommandHost.RoomFlagSet(int flag) =>
        (Context.Rooms.SaveData.GetRoomFlags(_record.Group, _record.Room) & flag) != 0;

    bool ICutsceneCommandHost.TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("Saved Maku Tree text-option branch has no completed choice result.") == value;

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if ((textId is < 0x0542 or > 0x0550) && textId != 0x0561)
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree command stream requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x054a)
            Context.ShowChoiceDialogue(message, textboxPosition: _record.TextboxPosition);
        else
            Context.ShowDialogue(message, _record.TextboxPosition);
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
        RequireScriptActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireScriptActor(actor).InitializeCollisionRadii();

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX) =>
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);

    void ICutsceneCommandHost.WriteMemory(string binding, int value)
    {
        if (binding != MapTextBinding || value != _record.MapTextLow)
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree script cannot write '{binding}'=${value:x2}.");
        }
        Context.Rooms.SaveData.SetMakuMapTextPresent(value);
    }

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music != _record.Music)
            throw new InvalidOperationException($"Unexpected Maku Tree music ${music:x2}.");
        Context.Sound.PlaySound(music);
    }

    void ICutsceneCommandHost.SetGlobalFlag(int flag)
    {
        if (flag != _record.AdviceFlag)
            throw new InvalidOperationException($"Unexpected Maku Tree flag ${flag:x2}.");
        Context.Rooms.SaveData.SetGlobalFlag(flag);
    }

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

    private void CheckSpawnSeedSatchel()
    {
        OracleSaveData save = Context.Rooms.SaveData;
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
            spawnMode: TreasureSpawnMode.Instant);
    }

    private void DropSeedSatchel()
    {
        OracleSaveData save = Context.Rooms.SaveData;
        if (save.HasRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag80))
            return;

        int linkX = Mathf.FloorToInt(Context.Player.Position.X);
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
            spawnMode: TreasureSpawnMode.FromScreenTop);
    }

    private void SpawnSeedSatchel(string objectName, int y, int x, int spawnMode)
    {
        TreasureObjectRecord treasure =
            Context.Treasures.GetObject(objectName);
        if (treasure.TreasureId != TreasureId.SeedSatchel ||
            treasure.Graphic != 0x20)
        {
            throw new InvalidOperationException(
                $"{objectName} is no longer the imported Seed Satchel graphic.");
        }
        var request = new GroundTreasureGrantRequest(
            _record.Group,
            _record.Room,
            0,
            y,
            x,
            objectName,
            $"scriptHelper.s:{(spawnMode == TreasureSpawnMode.FromScreenTop ? "makuTree_dropSeedSatchel" : "makuTree_checkSpawnSeedSatchel")}")
        {
            SpawnMode = spawnMode,
            GrabMode = TreasureGrabMode.OneHand,
            SpawnDelayFrames =
                spawnMode == TreasureSpawnMode.FromScreenTop ? _record.DropDelayFrames : 0,
            InitialZPixels = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.InitialZPixels : 0,
            BounceCount = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.BounceCount : 0,
            Gravity = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.Gravity : 0,
            BounceSpeed = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.BounceSpeed : 0,
            SpawnSound = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.SpawnSound : 0,
            LandingSound = spawnMode == TreasureSpawnMode.FromScreenTop ? _record.LandingSound : 0,
            ExpectedTreasureId = TreasureId.SeedSatchel
        };
        Context.Entities.SpawnGroundTreasure(request);
    }
}

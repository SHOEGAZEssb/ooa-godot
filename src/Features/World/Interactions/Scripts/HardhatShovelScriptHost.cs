using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_HARDHAT_WORKER $58:$00 Shovel script.</summary>
internal sealed class HardhatShovelScriptHost : NpcInteractionCommandHost
{
    private const string Var03Binding = "HardhatVar03";
    private readonly TreasureDatabase _treasures;
    private readonly BlackTowerWorkerDatabase _database;
    private GroundTreasurePickup? _treasure;

    public HardhatShovelScriptHost(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands,
        TreasureDatabase treasures,
        BlackTowerWorkerDatabase database)
        : base("Hardhat", rooms, entities, dialogue, commands)
    {
        _treasures = treasures;
        _database = database;
    }

    protected override bool MatchesAndPrepare(NpcCharacter npc) =>
        npc.Record is { Id: 0x58, SubId: 0x00 };

    public override int ReadMemory(string binding)
    {
        if (binding != Var03Binding)
        {
            throw new InvalidOperationException(
                $"hardhatWorkerSubid00Script cannot read '{binding}'.");
        }
        return ScriptActor.Record.Var03;
    }

    public override bool MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    public override bool RoomFlagSet(int flag)
    {
        if (flag != OracleSaveData.RoomFlagItem)
        {
            throw new InvalidOperationException(
                $"hardhatWorkerSubid00Script cannot read room flag ${flag:x2}.");
        }
        return Rooms.SaveData.HasRoomFlag(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlagItem);
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is not (0x1000 or 0x1001 or 0x1002) ||
            message != _database.Text(textId))
        {
            throw new InvalidOperationException(
                $"hardhatWorkerSubid00Script requested invalid TX_{textId:x4}.");
        }
        ScriptActor.SetDialogue(textId, message, canFace: true);
        ShowDialogue(message, choice: false);
    }

    public override void SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        string expected = _database.Visual("hardhat-work").Animation;
        if (animation != 4 || encodedAnimation != expected)
        {
            throw new InvalidOperationException(
                $"Hardhat animation ${animation:x2} diverges from imported " +
                "animation $04.");
        }
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureDatabase.TreasureShovel || parameter != 0)
        {
            throw new InvalidOperationException(
                $"hardhatWorkerSubid00Script requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        TreasureObjectRecord shovel =
            _treasures.GetObject("TREASURE_OBJECT_SHOVEL_00");
        if (shovel.TreasureId != treasureId ||
            shovel.SubId != parameter ||
            shovel.Parameter != 0 ||
            shovel.TextId != 0x25)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_SHOVEL_00 no longer matches giveitem in " +
                "hardhatWorkerSubid00Script.");
        }

        BlackTowerWorkerDatabaseVisualRecord visual =
            _database.Visual("shovel");
        Vector2 position = ScriptPlayer.Position;
        var request = new GroundTreasureGrantRequest(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            shovel.Name,
            "hardhatWorkerSubid00Script:giveitem TREASURE_SHOVEL,$00")
        {
            SpawnMode = 0,
            GrabMode = 2,
            VisualOverride = new GroundTreasureVisualOverride(
                visual.Sprite,
                visual.TileBase,
                visual.Palette,
                visual.Animation),
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            ExpectedTreasureId = treasureId,
            ExpectedSubId = parameter,
            ExpectedObjectParameter = 0
        };
        _treasure = Entities.GrantGroundTreasure(request, ScriptPlayer);
    }

    public override void RunNativeHandler(string handler)
    {
        if (handler != "turnToFaceLink")
        {
            throw new InvalidOperationException(
                $"Unknown hardhat native handler '{handler}'.");
        }
        ScriptActor.FaceToward(ScriptPlayer.Position);
    }

    protected override void BeforeAdvanceFrame() => FinishTreasure();

    protected override void ResetHostState()
    {
        FinishTreasure();
        if (HasState)
        {
            ScriptActor.SetScriptAnimation(
                _database.Visual("hardhat-work").Animation);
        }
    }

    private void FinishTreasure()
    {
        if (_treasure is null)
            return;
        _treasure.Finish(ScriptPlayer);
        _treasure = null;
    }
}

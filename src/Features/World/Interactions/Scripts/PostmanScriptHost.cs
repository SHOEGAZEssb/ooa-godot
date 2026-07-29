using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Room 2:2f INTERAC_POSTMAN $55:$00 and postmanScript.</summary>
internal sealed class PostmanScriptHost : NpcInteractionCommandHost
{
    private const int PoeClock = 0x00;
    private const int Stationery = 0x01;
    private const int Var3f = 0x3f;

    private readonly TreasureDatabase _treasures;
    private readonly InventoryState _inventory;
    private GroundTreasurePickup? _treasure;

    internal PostmanScriptHost(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands,
        TreasureDatabase treasures,
        InventoryState inventory)
        : base("Postman", rooms, entities, dialogue, commands)
    {
        _treasures = treasures;
        _inventory = inventory;
    }

    internal GroundTreasurePickup? Treasure => _treasure;

    protected override bool MatchesAndPrepare(NpcCharacter npc) =>
        npc is PostmanCharacter &&
        npc.Record is
        {
            Group: 2,
            Room: 0x2f,
            Id: 0x55,
            SubId: 0x00,
            Var03: 0x00
        };

    public override bool RoomFlagSet(int flag)
    {
        if (flag != OracleSaveData.RoomFlagItem)
        {
            throw new InvalidOperationException(
                $"postmanScript cannot read room flag ${flag:x2}.");
        }
        return Rooms.SaveData.HasRoomFlag(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlagItem);
    }

    public override bool TradeItemEquals(int value)
    {
        if (value != PoeClock)
        {
            throw new InvalidOperationException(
                $"postmanScript cannot compare trade item ${value:x2}.");
        }
        return _inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            _inventory.TradeItem == PoeClock;
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is < 0x0b03 or > 0x0b06 ||
            string.IsNullOrEmpty(message))
        {
            throw new InvalidOperationException(
                $"postmanScript requested invalid TX_{textId:x4}.");
        }
        ShowDialogue(message, choice: textId == 0x0b04);
    }

    public override void WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        if (address != Var3f || value != 1)
        {
            throw new InvalidOperationException(
                $"postmanScript wrote unexpected object byte " +
                $"${address:x2}=${value:x2}.");
        }
        RequirePostman(actor).SetLeaving();
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        RequirePostman(actor).SetMovementAnimation(
            angle, encodedAnimation, ScriptPlayer);

    public override void MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        RequirePostman(actor).MoveAtSpeed(speed, angle, ScriptPlayer);

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureDatabase.TreasureTradeItem ||
            parameter != Stationery)
        {
            throw new InvalidOperationException(
                $"postmanScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        TreasureObjectRecord stationery =
            _treasures.GetObject("TREASURE_OBJECT_TRADEITEM_01");
        if (stationery.TreasureId != treasureId ||
            stationery.SubId != parameter ||
            stationery.Parameter != Stationery ||
            stationery.TextId != 0x5b ||
            stationery.Graphic != 0x71)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_TRADEITEM_01 no longer matches " +
                "postmanScript's giveitem command.");
        }

        Vector2 position = ScriptPlayer.Position;
        var request = new GroundTreasureGrantRequest(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            stationery.Name,
            "scriptHelper.s:postmanScript giveitem TREASURE_TRADEITEM,$01")
        {
            SpawnMode = 0,
            GrabMode = 2,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            ExpectedTreasureId = treasureId,
            ExpectedSubId = parameter,
            ExpectedObjectParameter = Stationery
        };
        _treasure = Entities.GrantGroundTreasure(request, ScriptPlayer);
    }

    public override void ScriptEnded()
    {
        ScriptActor.SetScriptButtonSensitive(false);
        ScriptActor.SetActive(false);
    }

    protected override void BeforeAdvanceFrame()
    {
        // The final movedown update consumes counter2 before the native tail.
        // The preceding fixed entity update has already applied that frame's
        // SPEED_200 animation calls, so clear it for the following wait.
        if (CurrentCommandIndex == 19 &&
            CurrentCommandUpdates > 0 &&
            Counter == 1)
        {
            RequirePostman("Postman").CompleteMovement();
        }
        FinishTreasure();
    }

    protected override void ResetHostState()
    {
        FinishTreasure();
    }

    private PostmanCharacter RequirePostman(string actor)
    {
        NpcCharacter npc = RequireActor(actor);
        return npc as PostmanCharacter ??
            throw new InvalidOperationException(
                "postmanScript actor is not a PostmanCharacter.");
    }

    private void FinishTreasure()
    {
        if (_treasure is null)
            return;
        _treasure.Finish(ScriptPlayer);
        _treasure = null;
    }
}

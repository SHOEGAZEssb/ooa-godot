using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Past INTERAC_BIPIN $28:$0a and bipinScript3.</summary>
internal sealed class PastBipinScriptHost : NpcInteractionCommandHost
{
    private readonly TreasureDatabase _treasures;
    private readonly BipinBlossomFamilyStateResolver _family;
    private GroundTreasurePickup? _treasure;

    public PastBipinScriptHost(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands,
        TreasureDatabase treasures,
        BipinBlossomFamilyStateResolver family)
        : base("PastBipin", rooms, entities, dialogue, commands)
    {
        _treasures = treasures;
        _family = family;
    }

    internal GroundTreasurePickup? Treasure => _treasure;

    protected override bool MatchesAndPrepare(NpcCharacter npc) =>
        npc.Record is { Id: 0x28, SubId: 0x0a };

    public override bool RoomFlagSet(int flag)
    {
        if (flag != OracleSaveData.RoomFlagItem)
        {
            throw new InvalidOperationException(
                $"bipinScript3 cannot read room flag ${flag:x2}.");
        }
        return Rooms.SaveData.HasRoomFlag(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlagItem);
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is < 0x4311 or > 0x4313)
        {
            throw new InvalidOperationException(
                $"bipinScript3 requested unknown TX_{textId:x4}.");
        }
        Dialogue resolved = _family.Text(textId, Rooms.SaveData);
        if (string.IsNullOrEmpty(message))
        {
            throw new InvalidOperationException(
                $"Imported TX_{textId:x4} is empty.");
        }
        ShowDialogue(resolved.Message, choice: false);
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureDatabase.TreasureGashaSeed ||
            parameter != 0x08)
        {
            throw new InvalidOperationException(
                $"bipinScript3 requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        TreasureObjectRecord seed =
            _treasures.GetObject("TREASURE_OBJECT_GASHA_SEED_08");
        if (seed.TreasureId != treasureId ||
            seed.SubId != parameter ||
            seed.Parameter != 0x01 ||
            seed.TextId != 0x4b ||
            seed.Graphic != 0x0d)
        {
            throw new InvalidOperationException(
                "TREASURE_OBJECT_GASHA_SEED_08 no longer matches " +
                "bipinScript3's giveitem command.");
        }

        _treasure = Entities.GrantGroundTreasure(
            CreateGrantRequest(
                seed,
                "bipinScript3:giveitem TREASURE_GASHA_SEED,$08",
                treasureId,
                parameter,
                expectedObjectParameter: 0x01),
            ScriptPlayer);
    }

    protected override void BeforeAdvanceFrame() => FinishTreasure();

    protected override void ResetHostState() => FinishTreasure();

    private GroundTreasureGrantRequest CreateGrantRequest(
        TreasureObjectRecord treasure,
        string source,
        int expectedTreasure,
        int expectedSubId,
        int expectedObjectParameter)
    {
        Vector2 position = ScriptPlayer.Position;
        return new GroundTreasureGrantRequest(
            Rooms.ActiveGroup,
            Rooms.CurrentRoom.Id,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            treasure.Name,
            source)
        {
            SpawnMode = 0,
            GrabMode = 2,
            DialogueTiming = GroundTreasureDialogueTiming.AfterGrab,
            CompletionOwner = GroundTreasureCompletionOwner.Caller,
            ExpectedTreasureId = expectedTreasure,
            ExpectedSubId = expectedSubId,
            ExpectedObjectParameter = expectedObjectParameter
        };
    }

    private void FinishTreasure()
    {
        if (_treasure is null)
            return;
        _treasure.Finish(ScriptPlayer);
        _treasure = null;
    }
}

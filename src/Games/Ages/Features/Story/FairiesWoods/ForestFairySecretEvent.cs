using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// forestFairy.s:$49:$0b retains its interaction slot while the imported
// forestFairyScript_heartContainerSecret handles A, MENU_SECRET and reward.
internal sealed class ForestFairySecretEvent : InteractiveInfiniteScriptHost<NpcCharacter>, IRoomEntryEvent
{
    private static readonly IReadOnlyList<CutsceneCommand> Commands =
        CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + "cutscenes/forest_fairy_secret_commands.tsv");
    private bool _secretPending;
    private int _secretResult;
    private int _generation;
    private string _returnSecret = string.Empty;

    internal Func<int, Action<bool>, bool>? OpenSecretMenu { get; set; }
    internal int SecretResult => _secretResult;
    public override bool DialogueOpen => Context.DialogueOpen || _secretPending;
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked || _secretPending;
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    public bool ScreenTransitionsDisabled => BlocksGameplay;

    internal ForestFairySecretEvent(RoomEventContext context) : base(context, "ForestFairy") { }
    public bool Matches(int group, OracleRoomData room) => group == 0 && room.Id == 0x70;

    public void Start(OracleRoomData room)
    {
        NpcCharacter actor = Context.RequireNpc(0, 0x70, AgesInteractionId.ForestFairy, 0x0b,
            "INTERAC_FOREST_FAIRY");
        StartInfiniteScript(actor, Commands);
        Context.Entities.EntityAdapters<ForestNpcRoomEntity>().Single(entity =>
            ReferenceEquals(entity.Node, actor)).SecretScript = this;
    }

    // The script runs in the fairy's interaction slot, after Link publishes A.
    public override void UpdateFrame() { }
    internal void AdvanceObjectFrame() => AdvanceInfiniteScript();

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) =>
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);

    public override bool TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("forestFairyScript_heartContainerSecret has no completed choice.") == value;

    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "Global:111" => (Context.Rooms.SaveData.HasGlobalFlag(GlobalFlag.DoneFairySecret) ? 1 : 0) == value,
        "SecretResult" => _secretResult == value,
        _ => throw UnsupportedCommand($"read fairy secret memory '{binding}'")
    };

    public override void ShowText(int textId, string message) => ShowText(textId, message, null);
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        if (textId is not (>= 0x1148 and <= 0x114d))
            throw UnsupportedCommand($"show fairy TX_{textId:x4}");
        string resolved = message.Replace("\\secret1", _returnSecret, StringComparison.Ordinal);
        if (resolved.Contains("\\opt(", StringComparison.Ordinal))
            Context.ShowChoiceDialogue(resolved, textboxPosition: textboxPosition, sourceTextId: textId);
        else
            Context.ShowDialogue(resolved, textboxPosition: textboxPosition, sourceTextId: textId);
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureId.HeartContainer || parameter != 2)
            throw UnsupportedCommand($"give fairy treasure ${treasureId:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(0, 0x70, treasureId, parameter,
            "TREASURE_OBJECT_HEART_CONTAINER_02",
            "scripts/ages/scripts.s:forestFairyScript_heartContainerSecret giveitem TREASURE_HEART_CONTAINER,$02",
            objectParameter: 4);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "AskSecret":
                _secretPending = true;
                int generation = _generation;
                if (OpenSecretMenu is null || !OpenSecretMenu(0x01, valid =>
                    {
                        if (generation != _generation) return;
                        _secretResult = valid ? 0 : 1;
                        _secretPending = false;
                    }))
                    throw UnsupportedCommand("acquire MENU_SECRET for FAIRY_SECRET $01");
                break;
            case "GenerateSecret":
                _returnSecret = new LinkedGameNpcDatabase().GenerateSecret(0x11,
                    Context.Rooms.SaveData);
                break;
            default:
                throw UnsupportedCommand($"forest fairy helper '{handler}'");
        }
    }

    protected override void ResetEventState()
    {
        _generation++;
        _secretPending = false;
        _secretResult = 0;
        _returnSecret = string.Empty;
    }

    protected override void ReleaseScriptActor(NpcCharacter actor)
    {
        foreach (ForestNpcRoomEntity entity in Context.Entities.EntityAdapters<ForestNpcRoomEntity>())
            if (ReferenceEquals(entity.Node, actor)) entity.SecretScript = null;
    }
}

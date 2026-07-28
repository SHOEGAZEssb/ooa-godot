using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>linkedGameNpcScript for Ages linked-secret givers.</summary>
internal sealed class LinkedGameNpcScriptHost : NpcInteractionCommandHost
{
    private const string HasExtraTextBinding = "LinkedNpcHasExtraText";
    private readonly LinkedGameNpcDatabase _database;
    private LinkedGameNpcDatabaseRecord _record;
    private int _loadedTextId;
    private string _loadedMessage = string.Empty;
    private string _secret = string.Empty;
    private int _nextInitialChoice;

    public LinkedGameNpcScriptHost(
        RoomSession rooms,
        RoomEntityManager entities,
        DialogueBox dialogue,
        IReadOnlyList<CutsceneCommand> commands,
        LinkedGameNpcDatabase database)
        : base("LinkedNpc", rooms, entities, dialogue, commands)
    {
        _database = database;
    }

    protected override bool MatchesAndPrepare(NpcCharacter npc)
    {
        if (!_database.TryGet(
                npc.Record, out LinkedGameNpcDatabaseRecord record))
        {
            return false;
        }
        if (HasState && _record != record)
            Cancel();
        _record = record;
        return true;
    }

    public override bool MemoryEquals(string binding, int value)
    {
        if (binding != HasExtraTextBinding)
        {
            throw new InvalidOperationException(
                $"linkedGameNpcScript cannot read '{binding}'.");
        }
        return (_record.HasExtraText ? 1 : 0) == value;
    }

    public override void ShowLoadedText()
    {
        bool choice = _loadedTextId == _record.OfferTextId ||
            _loadedTextId == _record.ExplanationTextId ||
            _loadedTextId == _record.SecretTextId;
        string message = _loadedMessage.Replace(
            "\\secret1",
            _secret,
            StringComparison.OrdinalIgnoreCase);
        ShowDialogue(message, choice, _nextInitialChoice);
        _nextInitialChoice = 0;
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "linkedNpc_initHighTextIndex":
            case "linkedNpc_checkHasExtraTextBox":
                return;
            case "linkedNpc_selectOffer":
                Select(_record.OfferTextId, _record.OfferMessage);
                return;
            case "linkedNpc_selectRefusal":
                Select(_record.RefusalTextId, _record.RefusalMessage);
                return;
            case "linkedNpc_selectExplanation":
                Select(_record.ExplanationTextId, _record.ExplanationMessage);
                return;
            case "linkedNpc_generateSecret":
                Rooms.SaveData.SetGlobalFlag(_record.BeganFlag);
                _secret = _database.GenerateSecret(_record, Rooms.SaveData);
                return;
            case "linkedNpc_selectSecret":
                Select(_record.SecretTextId, _record.SecretMessage);
                return;
            case "linkedNpc_selectFinal":
                Select(_record.FinalTextId, _record.FinalMessage);
                return;
            default:
                throw new InvalidOperationException(
                    $"Unknown linkedGameNpcScript native handler '{handler}'.");
        }
    }

    protected override void OnTextOptionConsumed(int choice)
    {
        if (choice == 1)
            _nextInitialChoice = 1;
    }

    protected override void ResetHostState()
    {
        _record = default;
        _loadedTextId = 0;
        _loadedMessage = string.Empty;
        _secret = string.Empty;
        _nextInitialChoice = 0;
    }

    private void Select(int textId, string message)
    {
        _loadedTextId = textId;
        _loadedMessage = message;
    }
}

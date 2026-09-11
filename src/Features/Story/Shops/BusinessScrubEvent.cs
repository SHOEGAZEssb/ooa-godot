using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Native INTERAC_BUSINESS_SCRUB $ce:$00/$03 shield purchases.
/// </summary>
internal sealed class BusinessScrubEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private RoomEventResources? _resources;
    private RoomEventResources EventResources => _resources ??= new(_context, this);
    private readonly BusinessScrubDatabase _database = new();
    private NpcCharacter? _scrub;
    private BusinessScrubEventStage _stage;
    private BusinessScrubOffer _offer;
    private BusinessScrubRoomEntity? _native;

    public BusinessScrubEvent(RoomEventContext context) => _context = context;

    public bool HasState => _stage != BusinessScrubEventStage.Inactive;
    public bool BlocksGameplay => HasState;
    internal BusinessScrubEventStage Stage => _stage;

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (_stage != BusinessScrubEventStage.Inactive ||
            !_database.Matches(npc.Record))
        {
            return false;
        }

        _scrub = npc;
        npc.SetScriptButtonSensitive(false);
        npc.SetScriptAnimation(_database.Animation(2));
        _native = _context.Entities.EntityAdapters<BusinessScrubRoomEntity>()
            .Single(scrub => ReferenceEquals(scrub.Npc, npc));
        _native.Talking = true;
        _offer = _native.Offer;
        BusinessScrubOffer offer = _offer;
        _context.ShowChoiceDialogue(
            _database.Text(_database.PromptText).Replace(
                "\\num1", offer.Price.ToString(), StringComparison.Ordinal));
        _stage = BusinessScrubEventStage.PurchasePrompt;
        return true;
    }

    public void UpdateFrame()
    {
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case BusinessScrubEventStage.PurchasePrompt:
                ResolvePurchase(TakeChoice());
                break;
            case BusinessScrubEventStage.ResultText:
                FinishTalk();
                break;
        }
    }

    public void Cancel() => FinishTalk();

    private void ResolvePurchase(int choice)
    {
        if (choice != 0)
        {
            ShowResult(_database.DeclineText);
            return;
        }

        BusinessScrubOffer offer = _offer;
        if (_context.Inventory.Rupees < offer.Price)
        {
            ShowResult(_database.InsufficientText);
            return;
        }
        if (_context.Inventory.HasTreasure(offer.Treasure))
        {
            ShowResult(_database.AlreadyOwnedText);
            return;
        }

        _context.Inventory.GiveTreasure(offer.Treasure, offer.Parameter);
        _context.Inventory.AddRupees(-offer.Price);
        _context.Sound.PlaySound(OracleSoundEngine.SndGetSeed);
        ShowResult(_database.SuccessText);
    }

    private int TakeChoice() =>
        EventResources.RequireDialogueChoice("Business Scrub prompt closed without a text-option result.");

    private void ShowResult(int textId)
    {
        _context.ShowDialogue(_database.Text(textId));
        _stage = BusinessScrubEventStage.ResultText;
    }

    private void FinishTalk()
    {
        if (_native is not null) _native.Talking = false;
        _native = null;
        if (_scrub is not null)
        {
            _scrub.SetScriptAnimation(_database.Animation(4));
            _scrub.SetScriptButtonSensitive(true);
        }
        _scrub = null;
        _stage = BusinessScrubEventStage.Inactive;
    }
}

internal enum BusinessScrubEventStage
{
    Inactive,
    PurchasePrompt,
    ResultText
}

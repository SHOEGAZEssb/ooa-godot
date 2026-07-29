using System;

namespace oracleofages;

/// <summary>
/// Native INTERAC_BUSINESS_SCRUB $ce:$03 shield purchase in past room 1:81.
/// </summary>
internal sealed class BusinessScrubEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly BusinessScrubDatabase _database = new();
    private NpcCharacter? _scrub;
    private BusinessScrubEventStage _stage;

    public BusinessScrubEvent(RoomEventContext context) => _context = context;

    public bool HasState => _stage != BusinessScrubEventStage.Inactive;
    public bool BlocksGameplay => HasState;
    internal BusinessScrubEventStage Stage => _stage;
    internal BusinessScrubDatabase Database => _database;

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (_stage != BusinessScrubEventStage.Inactive ||
            _context.Rooms.ActiveGroup != _database.Group ||
            _context.Rooms.CurrentRoom.Id != _database.Room ||
            !_database.Matches(npc.Record))
        {
            return false;
        }

        _scrub = npc;
        npc.SetScriptButtonSensitive(false);
        npc.SetScriptAnimation(_database.Animation(2));
        BusinessScrubOffer offer =
            _database.OfferForShieldLevel(_context.Inventory.ShieldLevel);
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

        BusinessScrubOffer offer =
            _database.OfferForShieldLevel(_context.Inventory.ShieldLevel);
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

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "Business Scrub prompt closed without a text-option result.");
        }
        return choice;
    }

    private void ShowResult(int textId)
    {
        _context.ShowDialogue(_database.Text(textId));
        _stage = BusinessScrubEventStage.ResultText;
    }

    private void FinishTalk()
    {
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

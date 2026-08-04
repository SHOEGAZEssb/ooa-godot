using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// tingleScript's normal-game chart and Seed Satchel paths. The linked-game
/// password-entry opcode remains outside this event until the shared secret
/// input UI exists; choosing that branch follows the script's invalid-secret
/// result without inventing clone-only password state.
/// </summary>
internal sealed class TingleEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TingleDatabase _database = new();
    private readonly TingleRecord _record;
    private TingleRoomEntity? _actor;
    private GroundTreasurePickup? _reward;
    private TingleEventStage _stage;
    private TingleAfterKooloo _afterKooloo;
    private int _counter;

    internal TingleEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
    }

    public bool HasState => _stage != TingleEventStage.Inactive;
    public bool BlocksGameplay => HasState;
    internal TingleEventStage Stage => _stage;
    internal TingleRoomEntity? Actor => _actor;
    internal TingleDatabase Database => _database;
    internal int Counter => _counter;

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        if (HasState)
            Cancel();
        _actor = null;
        if (group != _record.Group || room.Id != _record.Room)
            return;

        _actor = _context.Entities.Entities<TingleRoomEntity>().SingleOrDefault() ??
            throw new InvalidOperationException(
                "Room 0:79 did not instantiate INTERAC_TINGLE `$c8:$00.");
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || _actor is null || !_actor.Grounded ||
            !ReferenceEquals(npc, _actor.Npc) ||
            _context.Rooms.ActiveGroup != _record.Group ||
            _context.Rooms.CurrentRoom.Id != _record.Room)
        {
            return false;
        }

        _actor.SetInteractionEnabled(false);
        if (!_context.Inventory.HasTreasure(_record.IslandChartTreasure))
        {
            bool met = _context.Rooms.SaveData.HasGlobalFlag(_record.MetFlag);
            if (!met)
                _context.Rooms.SaveData.SetGlobalFlag(_record.MetFlag);
            ShowChoice(met ? 0x1e01 : 0x1e00);
            _stage = TingleEventStage.FriendPrompt;
            return true;
        }

        BeginOwnedChartConversation();
        return true;
    }

    public void UpdateFrame()
    {
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TingleEventStage.FriendPrompt:
                ResolveFriendChoice(TakeChoice());
                break;
            case TingleEventStage.FriendAcceptedText:
                GrantChart();
                break;
            case TingleEventStage.ChartReward:
                if (_reward is { Finished: true })
                {
                    _reward = null;
                    Show(0x1e04);
                    _stage = TingleEventStage.PostChartText;
                }
                break;
            case TingleEventStage.PostChartText:
                BeginKooloo(TingleAfterKooloo.PostChartWait);
                break;
            case TingleEventStage.EndText:
                BeginKooloo(TingleAfterKooloo.Finish);
                break;
            case TingleEventStage.KoolooText:
                _stage = TingleEventStage.KoolooAnimation;
                break;
            case TingleEventStage.KoolooAnimation:
                if (_actor is { KoolooComplete: true })
                    FinishKooloo();
                break;
            case TingleEventStage.PostChartWait:
                _counter--;
                if (_counter == 0)
                    BeginRickyDeparture();
                break;
            case TingleEventStage.SatchelPrompt:
                ResolveSatchelChoice(TakeChoice());
                break;
            case TingleEventStage.UpgradeAcceptedText:
                BeginUpgradeAnimation();
                break;
            case TingleEventStage.UpgradeAnnouncement:
                _stage = TingleEventStage.UpgradeAnimation;
                break;
            case TingleEventStage.UpgradeAnimation:
                if (_actor is { KoolooComplete: true })
                {
                    _counter = _record.UpgradeGlowWait;
                    _stage = TingleEventStage.UpgradeGlowWait;
                }
                break;
            case TingleEventStage.UpgradeGlowWait:
                _counter--;
                if (_counter == 0)
                    GrantSatchelUpgrade();
                break;
            case TingleEventStage.UpgradeReward:
                if (_reward is { Finished: true })
                {
                    _reward = null;
                    RefillSeedSatchel();
                    Finish();
                }
                break;
            case TingleEventStage.PostgamePrompt:
                ResolvePostgameChoice(TakeChoice());
                break;
        }
    }

    public void Cancel()
    {
        _reward?.Finish(_context.Player);
        _reward = null;
        _actor?.SetInteractionEnabled(true);
        _stage = TingleEventStage.Inactive;
        _counter = 0;
    }

    private void BeginOwnedChartConversation()
    {
        if (_actor is null)
            throw new InvalidOperationException("Tingle interaction lost its actor.");
        if (!_actor.HasEnoughSeedTypes)
        {
            ShowEndText(0x1e04);
            return;
        }

        int level = _context.Inventory.SeedSatchelLevel;
        if (level >= 3)
        {
            // The non-postgame branch is exactly @alreadyGotSatchelUpgrade.
            // Return-secret generation shares the unresolved secret subsystem.
            ShowEndText(0x1e08);
            return;
        }
        if (level == 2 && _context.Rooms.SaveData.IsCompleted)
        {
            ShowChoice(0x1e09);
            _stage = TingleEventStage.PostgamePrompt;
            return;
        }
        if (_context.Rooms.SaveData.HasGlobalFlag(_record.UpgradeFlag))
        {
            ShowEndText(0x1e08);
            return;
        }

        ShowChoice(0x1e06);
        _stage = TingleEventStage.SatchelPrompt;
    }

    private void ResolveFriendChoice(int choice)
    {
        if (choice != 0)
        {
            ShowEndText(0x1e03);
            return;
        }
        Show(0x1e02);
        _stage = TingleEventStage.FriendAcceptedText;
    }

    private void GrantChart()
    {
        _reward = _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            _record.IslandChartTreasure,
            0,
            _record.IslandChartObject,
            "scripts/ages/scripts.s:tingleScript giveitem TREASURE_OBJECT_ISLAND_CHART_00");
        _stage = TingleEventStage.ChartReward;
    }

    private void ResolveSatchelChoice(int choice)
    {
        if (choice != 0)
        {
            ShowEndText(0x1e03);
            return;
        }
        _context.Rooms.SaveData.SetGlobalFlag(_record.UpgradeFlag);
        Show(0x1e07);
        _stage = TingleEventStage.UpgradeAcceptedText;
    }

    private void BeginUpgradeAnimation()
    {
        RequireActor().StartKooloo();
        Show(0x1e0c);
        _stage = TingleEventStage.UpgradeAnnouncement;
    }

    private void GrantSatchelUpgrade()
    {
        _reward = _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            _record.SatchelTreasure,
            0,
            _record.SatchelUpgradeObject,
            "scripts/ages/scripts.s:tingleScript giveitem TREASURE_OBJECT_SEED_SATCHEL_UPGRADE");
        _stage = TingleEventStage.UpgradeReward;
    }

    private void RefillSeedSatchel()
    {
        for (int treasure = TreasureDatabase.TreasureEmberSeeds;
             treasure <= TreasureDatabase.TreasureEmberSeeds + 4;
             treasure++)
        {
            if (_context.Inventory.HasTreasure(treasure))
                _context.Inventory.GiveTreasure(treasure, 0x99);
        }
    }

    private void ResolvePostgameChoice(int choice)
    {
        if (choice != 0)
        {
            ShowEndText(0x1e0a);
            return;
        }

        // askforsecret TINGLE_SECRET cannot be represented until the shared
        // text-input mode is implemented. Preserve the original invalid-input
        // result instead of accepting or mutating a fabricated secret.
        ShowEndText(0x1e0d);
    }

    private void BeginKooloo(TingleAfterKooloo after)
    {
        _afterKooloo = after;
        RequireActor().StartKooloo();
        Show(0x1e05);
        _stage = TingleEventStage.KoolooText;
    }

    private void FinishKooloo()
    {
        if (_afterKooloo == TingleAfterKooloo.PostChartWait)
        {
            _counter = _record.PostChartWait;
            _stage = TingleEventStage.PostChartWait;
        }
        else
        {
            Finish();
        }
    }

    private void BeginRickyDeparture()
    {
        RickyCompanionRoomEntity? ricky =
            _context.Entities.Entities<RickyCompanionRoomEntity>().SingleOrDefault();
        ricky?.BeginTingleDeparture(_database.Text(0x2006));
        Finish();
    }

    private void ShowEndText(int textId)
    {
        Show(textId);
        _stage = TingleEventStage.EndText;
    }

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(_database.Text(textId));

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException("Tingle prompt closed without a choice result.");
        return choice;
    }

    private TingleRoomEntity RequireActor() =>
        _actor ?? throw new InvalidOperationException("Tingle interaction lost its actor.");

    private void Finish()
    {
        _actor?.SetInteractionEnabled(true);
        _stage = TingleEventStage.Inactive;
        _counter = 0;
    }
}

internal enum TingleEventStage
{
    Inactive,
    FriendPrompt,
    FriendAcceptedText,
    ChartReward,
    PostChartText,
    EndText,
    KoolooText,
    KoolooAnimation,
    PostChartWait,
    SatchelPrompt,
    UpgradeAcceptedText,
    UpgradeAnnouncement,
    UpgradeAnimation,
    UpgradeGlowWait,
    UpgradeReward,
    PostgamePrompt
}

internal enum TingleAfterKooloo
{
    Finish,
    PostChartWait
}

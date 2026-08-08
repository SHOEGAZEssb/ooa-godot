using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Owns both eras of the Wild Tokay game: manager prompts, source RNG,
/// participants, meat, temporary equipment, round results, and prizes.
/// </summary>
internal sealed class WildTokayGameEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayIslandDatabase _database;
    private readonly GashaSpotDatabase _ringDatabase = new();
    private readonly List<WildParticipantState> _participants = new();
    private readonly List<WildTokayMeat> _meats = new();
    private WildTokayGameStage _stage;
    private WildTokayGameStage _nextStage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private bool _inputLocked;
    private int _savedEquippedA;
    private int _savedEquippedB;
    private int _wildLevel;
    private int _wildCycles;
    private int _wildColumn;
    private int _wildRandomIndex;
    private int _wildSpawnCounter;
    private bool _present;
    private bool _won;
    private bool _inventoryOverridden;
    private bool _prizePrepared;
    private bool _ringPrize;

    internal WildTokayGameEvent(
        RoomEventContext context,
        TokayIslandDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != WildTokayGameStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal WildTokayGameStage Stage => _stage;
    internal int Counter => _counter;

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        Cancel();
        if (group == _database.PastGameGroup &&
            room.Id == _database.PastGameRoom &&
            FindActor(0x48, 0x0d) is { Active: true } &&
            _context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet))
        {
            PreparePrize();
        }
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record.Id != 0x48)
            return false;

        switch (npc.Record.SubId)
        {
            case 0x0d:
                _actor = npc;
                BeginPastManager();
                return true;
            case 0x19:
                _actor = npc;
                BeginPresentManager();
                return true;
            default:
                return false;
        }
    }

    public void UpdateFrame()
    {
        if (_stage == WildTokayGameStage.Inactive)
            return;
        if (_stage == WildTokayGameStage.Playing)
        {
            UpdateGame();
            return;
        }
        if (_stage == WildTokayGameStage.Wait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (_stage is WildTokayGameStage.Prize or WildTokayGameStage.PresentBombReward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                AdvanceRewardStage();
            }
            return;
        }
        if (_context.DialogueOpen)
            return;

        AdvanceClosedDialogue();
    }

    public void Cancel()
    {
        _reward?.Finish(_context.Player);
        _reward = null;
        foreach (WildParticipantState participant in _participants)
            participant.Actor.SetActive(false);
        foreach (WildTokayMeat meat in _meats)
            meat.Finish();
        _participants.Clear();
        _meats.Clear();
        RestoreInventory();
        UnlockInput();
        _actor = null;
        _counter = 0;
        _wildCycles = 0;
        _prizePrepared = false;
        _ringPrize = false;
        _stage = WildTokayGameStage.Inactive;
    }

    private void BeginPastManager()
    {
        if (!_context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet))
        {
            ShowDialogueOnly(0x0a1c);
            return;
        }
        _present = false;
        PreparePrize();
        LockInput();
        Show(0x0a10);
        _stage = WildTokayGameStage.PastManagerPrizeIntro;
    }

    private void BeginPresentManager()
    {
        OracleSaveData save = _context.Rooms.SaveData;
        if (!save.HasGlobalFlag(_database.FinishedGameFlag))
        {
            ShowDialogueOnly(0x0a67);
            return;
        }
        if (save.HasGlobalFlag(_database.DoneSecretFlag))
        {
            ShowDialogueOnly(0x0a53);
            return;
        }
        LockInput();
        if (!save.HasGlobalFlag(_database.BeganSecretFlag))
        {
            ShowChoice(0x0a45);
            _stage = WildTokayGameStage.PresentSecretPrompt;
            return;
        }
        ShowChoice(0x0a51);
        _stage = WildTokayGameStage.PresentPlayPrompt;
    }

    private void AdvanceClosedDialogue()
    {
        switch (_stage)
        {
            case WildTokayGameStage.DialogueOnly:
                FinishInteraction();
                break;
            case WildTokayGameStage.PastManagerPrizeIntro:
                BeginWait(50, WildTokayGameStage.PastManagerPlayPrompt);
                break;
            case WildTokayGameStage.PastManagerPlayPrompt:
                ResolvePastPlayPrompt();
                break;
            case WildTokayGameStage.PastManagerRulesPrompt:
                ResolvePastRulesPrompt();
                break;
            case WildTokayGameStage.PastManagerDeclined:
            case WildTokayGameStage.PastManagerNoRupees:
                _prizePrepared = false;
                FinishInteraction();
                break;
            case WildTokayGameStage.IntroText:
                BeginWait(20, WildTokayGameStage.Begin);
                break;
            case WildTokayGameStage.StartText:
                UnlockInput();
                SpawnMeat();
                _context.Sound.PlaySound(_database.Constant("sound-whistle"));
                _wildSpawnCounter = _database.GameSpawnDelay;
                _stage = WildTokayGameStage.Playing;
                break;
            case WildTokayGameStage.ResultText:
                BeginWait(_present ? 60 : 20, WildTokayGameStage.Finish);
                break;
            case WildTokayGameStage.LossPrompt:
                ResolveLossPrompt();
                break;
            case WildTokayGameStage.PresentSecretPrompt:
                if (TakeChoice() == 0)
                {
                    // Shared secret-entry UI is not yet available; retain the
                    // original invalid-secret result without inventing state.
                    Show(0x0a48);
                }
                else
                {
                    Show(0x0a46);
                }
                _stage = WildTokayGameStage.DialogueOnly;
                break;
            case WildTokayGameStage.PresentPlayPrompt:
                if (TakeChoice() != 0)
                {
                    Show(0x0a52);
                    _stage = WildTokayGameStage.DialogueOnly;
                }
                else
                {
                    ShowChoice(0x0a4a);
                    _stage = WildTokayGameStage.PresentRulesPrompt;
                }
                break;
            case WildTokayGameStage.PresentRulesPrompt:
                if (TakeChoice() == 0)
                {
                    Show(0x0a4c);
                    _present = true;
                    _wildLevel = 2;
                    _stage = WildTokayGameStage.IntroText;
                }
                else
                {
                    ShowChoice(0x0a4b);
                }
                break;
            case WildTokayGameStage.PresentWinText:
                BeginWait(30, WildTokayGameStage.PresentGiveBombUpgrade);
                break;
            default:
                throw new InvalidOperationException(
                    $"Wild Tokay stage {_stage} closed an unexpected dialogue.");
        }
    }

    private void EnterStage(WildTokayGameStage stage)
    {
        _stage = stage;
        switch (stage)
        {
            case WildTokayGameStage.PastManagerPlayPrompt:
                ShowChoice(_wildLevel == 0 ? 0x0a13 : 0x0a11);
                break;
            case WildTokayGameStage.Begin:
                BeginGame();
                break;
            case WildTokayGameStage.StartText:
                Show(0x0a16);
                break;
            case WildTokayGameStage.ResultText:
                Show(_won ? 0x0a18 : 0x0a17);
                break;
            case WildTokayGameStage.Finish:
                FinishGame();
                break;
            case WildTokayGameStage.PresentGiveBombUpgrade:
                GivePresentBombUpgrade();
                break;
            default:
                throw new InvalidOperationException(
                    $"Wild Tokay wait entered unsupported stage {stage}.");
        }
    }

    private void AdvanceRewardStage()
    {
        switch (_stage)
        {
            case WildTokayGameStage.Prize:
                FinishInteraction();
                break;
            case WildTokayGameStage.PresentBombReward:
                _context.Rooms.SaveData.SetGlobalFlag(_database.DoneSecretFlag);
                // TX_0a50 embeds the generated Holodrum return secret. The
                // shared return-secret subsystem is not yet available, so end
                // after the source Bomb Upgrade instead of fabricating it.
                FinishInteraction();
                break;
            default:
                throw new InvalidOperationException(
                    $"Wild Tokay reward stage {_stage} completed unexpectedly.");
        }
    }

    private void ResolvePastPlayPrompt()
    {
        if (TakeChoice() != 0)
        {
            Show(0x0a1a);
            _stage = WildTokayGameStage.PastManagerDeclined;
            return;
        }
        if (_context.Inventory.Rupees < 10)
        {
            Show(0x0a1b);
            _stage = WildTokayGameStage.PastManagerNoRupees;
            return;
        }
        _context.Inventory.AddRupees(-10);
        ShowChoice(0x0a14);
        _stage = WildTokayGameStage.PastManagerRulesPrompt;
    }

    private void ResolvePastRulesPrompt()
    {
        if (TakeChoice() != 0)
        {
            ShowChoice(0x0a26);
            return;
        }
        Show(0x0a15);
        _stage = WildTokayGameStage.IntroText;
    }

    private void BeginGame()
    {
        _savedEquippedA = _context.Inventory.EquippedA;
        _savedEquippedB = _context.Inventory.EquippedB;
        _context.Inventory.SetScriptedEquippedItems(
            InventoryState.ItemNone, InventoryState.ItemBracelet);
        _inventoryOverridden = true;
        _context.Player.SetScriptedCoordinateHigh(
            horizontal: false, coordinate: _database.GameLinkY);
        _context.Player.SetScriptedCoordinateHigh(
            horizontal: true, coordinate: _database.GameLinkX);
        _wildCycles = _wildLevel < 3 ? 5 : _wildLevel == 3 ? 6 : 7;
        _wildColumn = 0;
        SelectPattern();
        _participants.Clear();
        _meats.Clear();
        foreach (NpcCharacter statue in _context.Entities.Entities<NpcCharacter>())
        {
            if (_present && statue.Record is { Id: 0x48, SubId: >= 0x1a and <= 0x1c })
                statue.SetActive(false);
        }
        LockInput();
        BeginWait(_database.GameStartDelay, WildTokayGameStage.StartText);
    }

    private void UpdateGame()
    {
        for (int index = _participants.Count - 1; index >= 0; index--)
        {
            WildParticipantState participant = _participants[index];
            if (!participant.Actor.Active)
                continue;
            participant.Actor.SetStatePosition(
                participant.Actor.Position + Vector2.Up * participant.Speed);
            if (!participant.HoldingMeat)
            {
                foreach (WildTokayMeat meat in _meats)
                {
                    if (!meat.Finished && meat.Thrown &&
                        (meat.Position - participant.Actor.Position).LengthSquared() < 100)
                    {
                        meat.Catch();
                        participant.HoldingMeat = true;
                        participant.Actor.SetScriptAnimation(
                            _database.Animation(participant.FromRight ? 8 : 7));
                        _context.Sound.PlaySound(_database.Constant("sound-open-chest"));
                        break;
                    }
                }
            }
            if (participant.Actor.Position.Y >= -8)
                continue;
            participant.Actor.SetActive(false);
            if (!participant.HoldingMeat)
            {
                EndRound(won: false);
                return;
            }
            if (participant.Red)
            {
                EndRound(won: true);
                return;
            }
        }

        if (_meats.All(meat => meat.Finished || meat.Thrown))
            SpawnMeat();

        if (--_wildSpawnCounter > 0)
            return;
        _wildSpawnCounter = _database.GameSpawnDelay;
        if (_wildCycles == 0)
            return;

        WildTokayPatternRecord pattern =
            _database.WildPattern(_wildLevel, _wildRandomIndex);
        int code = _wildColumn switch
        {
            0 => pattern.LeftBlue,
            1 => pattern.LeftRed,
            2 => pattern.RightBlue,
            _ => pattern.RightRed
        };
        bool finalParticipant =
            _wildCycles == 1 && IsLastOccupiedColumn(pattern, _wildColumn);
        SpawnParticipants(code, finalParticipant);
        _wildColumn++;
        if (_wildColumn < 4)
            return;
        _wildColumn = 0;
        _wildCycles--;
        if (_wildCycles > 0)
            SelectPattern();
    }

    private void SpawnParticipants(int code, bool final)
    {
        if (code is 1 or 3)
            SpawnParticipant(fromRight: false, red: final && code == 1);
        if (code is 2 or 3)
            SpawnParticipant(fromRight: true, red: final);
    }

    private void SpawnParticipant(bool fromRight, bool red)
    {
        NpcCharacter manager = RequireActor();
        int x = fromRight ? _database.ParticipantRightX : _database.ParticipantLeftX;
        NpcRecord source = manager.BaseRecord;
        var record = source with
        {
            SubId = 0x0c,
            Y = _database.ParticipantStartY,
            X = x,
            TextId = 0,
            Message = string.Empty,
            CanFace = false,
            Implementation = NpcImplementationClassification.EventOwned
        };
        NpcCharacter actor = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(record, $"WildTokay_{_participants.Count}"));
        actor.SetScriptAnimation(_database.Animation(0));
        actor.SetBlocksLink(false);
        if (red)
            actor.SetBasePalette(2);
        float speed = _wildLevel >= 3 ? 0.625f : 0.5f;
        _participants.Add(new WildParticipantState(actor, fromRight, red, speed));
    }

    private void SpawnMeat()
    {
        WildTokayMeat meat =
            _context.Entities.Spawn<WildTokayMeat>(new WildTokayMeatSpawn());
        _meats.Add(meat);
    }

    private void EndRound(bool won)
    {
        _won = won;
        LockInput();
        _context.Sound.PlaySound(won ? _database.SoundSuccess : _database.SoundError);
        BeginWait(30, WildTokayGameStage.ResultText);
    }

    private void FinishGame()
    {
        RestoreInventory();
        _context.Rooms.SaveData.SetRoomFlag(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlag40,
            value: false);
        if (_won)
        {
            if (_present)
            {
                Show(0x0a4f);
                _stage = WildTokayGameStage.PresentWinText;
            }
            else
            {
                GivePrize();
            }
            return;
        }
        ShowChoice(_present ? 0x0a4d : 0x0a19);
        _stage = WildTokayGameStage.LossPrompt;
    }

    private void ResolveLossPrompt()
    {
        if (TakeChoice() != 0)
        {
            if (_present)
                Show(0x0a4e);
            else
            {
                _prizePrepared = false;
                UnlockInput();
                FinishInteraction();
                return;
            }
            _stage = WildTokayGameStage.DialogueOnly;
            return;
        }
        if (!_present)
        {
            if (_context.Inventory.Rupees < 10)
            {
                Show(0x0a1b);
                _stage = WildTokayGameStage.PastManagerNoRupees;
                return;
            }
            _context.Inventory.AddRupees(-10);
        }
        Show(_present ? 0x0a4c : 0x0a15);
        _stage = WildTokayGameStage.IntroText;
    }

    private void GivePrize()
    {
        if (_wildLevel == 4 && _ringPrize)
        {
            int ring = _ringDatabase.SelectRing(
                2, (byte)_context.Entities.NextRandomValue());
            _context.Inventory.GiveUnappraisedRing(ring);
            _prizePrepared = false;
            FinishInteraction();
            return;
        }

        (int treasure, int parameter, string name) = _wildLevel switch
        {
            0 => (0x4d, 0, "TREASURE_OBJECT_SCENT_SEEDLING_00"),
            1 => (TreasureDatabase.TreasureRupees, 0x0e, "TREASURE_OBJECT_RUPEES_0e"),
            2 => (TreasureDatabase.TreasureRupees, 0x0f, "TREASURE_OBJECT_RUPEES_0f"),
            3 => (TreasureDatabase.TreasureGashaSeed, 0, "TREASURE_OBJECT_GASHA_SEED_00"),
            _ => (TreasureDatabase.TreasureRupees, 0x10, "TREASURE_OBJECT_RUPEES_10")
        };
        _reward = Grant(treasure, parameter, name, "tokayGame_givePrizeToLink");
        if (_wildLevel < 4)
        {
            _wildLevel++;
            WriteSaveByte(_database.WildLevelAddress, _wildLevel);
        }
        _prizePrepared = false;
        _stage = WildTokayGameStage.Prize;
    }

    private void GivePresentBombUpgrade()
    {
        _context.Inventory.ApplyTokayBombCapacityUpgrade();
        _reward = Grant(
            0x61, 0, "TREASURE_OBJECT_BOMB_UPGRADE_00", "tokayGiveBombUpgrade");
        _stage = WildTokayGameStage.PresentBombReward;
    }

    private void PreparePrize()
    {
        if (_prizePrepared)
            return;
        _wildLevel = Math.Clamp(
            (int)_context.Rooms.SaveData.ReadWramByte(_database.WildLevelAddress), 0, 4);
        _ringPrize =
            _wildLevel == 4 && (_context.Entities.NextRandomValue() & 0x07) == 0;
        _prizePrepared = true;
    }

    private void SelectPattern() =>
        _wildRandomIndex = _context.Entities.NextRandomValue() & 0x0f;

    private void RestoreInventory()
    {
        if (!_inventoryOverridden)
            return;
        _context.Inventory.SetScriptedEquippedItems(_savedEquippedB, _savedEquippedA);
        _inventoryOverridden = false;
        _wildCycles = 0;
    }

    private static bool IsLastOccupiedColumn(
        WildTokayPatternRecord pattern,
        int column)
    {
        int[] codes =
        [
            pattern.LeftBlue,
            pattern.LeftRed,
            pattern.RightBlue,
            pattern.RightRed
        ];
        if (codes[column] == 0)
            return false;
        for (int next = column + 1; next < codes.Length; next++)
            if (codes[next] != 0)
                return false;
        return true;
    }

    private void BeginWait(int frames, WildTokayGameStage next)
    {
        _counter = frames;
        _nextStage = next;
        _stage = WildTokayGameStage.Wait;
    }

    private NpcCharacter? FindActor(int id, int subId) =>
        _context.Entities.Entities<NpcCharacter>()
            .FirstOrDefault(npc => npc.Record.Id == id && npc.Record.SubId == subId);

    private NpcCharacter RequireActor() => _actor ??
        throw new InvalidOperationException("Wild Tokay game lost its manager actor.");

    private GroundTreasurePickup Grant(
        int treasure,
        int parameter,
        string objectName,
        string source)
    {
        TreasureObjectRecord rewardObject =
            _context.Treasures.GetObject(objectName);
        return _context.GrantScriptTreasure(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            treasure,
            parameter,
            objectName,
            $"scripts/ages:{source}",
            objectParameter: rewardObject.Parameter);
    }

    private void ShowDialogueOnly(int textId)
    {
        Show(textId);
        _stage = WildTokayGameStage.DialogueOnly;
    }

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(_database.Text(textId));

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                "Wild Tokay prompt closed without a text-option result.");
        return choice;
    }

    private void LockInput()
    {
        if (_inputLocked)
            return;
        _context.Player.BeginCutsceneControl();
        _inputLocked = true;
    }

    private void UnlockInput()
    {
        if (!_inputLocked)
            return;
        _context.Player.EndCutsceneControl();
        _inputLocked = false;
    }

    private void FinishInteraction()
    {
        UnlockInput();
        _actor = null;
        _stage = WildTokayGameStage.Inactive;
    }

    private void WriteSaveByte(int address, int value)
    {
        OracleSaveData save = _context.Rooms.SaveData;
        if (save.WriteWramByte(address, (byte)value))
            save.CommitInventoryChange();
    }

    private sealed class WildParticipantState(
        NpcCharacter actor,
        bool fromRight,
        bool red,
        float speed)
    {
        internal NpcCharacter Actor { get; } = actor;
        internal bool FromRight { get; } = fromRight;
        internal bool Red { get; } = red;
        internal float Speed { get; } = speed;
        internal bool HoldingMeat { get; set; }
    }
}

internal enum WildTokayGameStage
{
    Inactive,
    DialogueOnly,
    Wait,
    PastManagerPrizeIntro,
    PastManagerPlayPrompt,
    PastManagerRulesPrompt,
    PastManagerDeclined,
    PastManagerNoRupees,
    IntroText,
    Begin,
    StartText,
    Playing,
    ResultText,
    Finish,
    LossPrompt,
    Prize,
    PresentSecretPrompt,
    PresentPlayPrompt,
    PresentRulesPrompt,
    PresentWinText,
    PresentGiveBombUpgrade,
    PresentBombReward
}

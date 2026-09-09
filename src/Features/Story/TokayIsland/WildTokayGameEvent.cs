using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Owns both eras of the Wild Tokay game: manager prompts, source RNG,
/// participants, meat, temporary equipment, round results, and prizes.
/// </summary>
internal sealed class WildTokayGameEvent : TokayScriptEvent, IRoomEvent
{
    private readonly WildTokayGameDatabase _database;
    private readonly WildTokaySpawnSchedule _wildSchedule;
    private readonly GashaSpotDatabase _ringDatabase = new();
    private readonly List<WildParticipantState> _participants = new();
    private readonly List<WildTokayMeat> _meats = new();
    private readonly Dictionary<int, byte> _originalGameTiles = new();
    private NpcCharacter? _prizeAccessory;
    private OracleRoomData? _gameRoom;
    private WildTokayGameStage _stage;
    private WildTokayGameStage _nextStage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private int _pendingChoice;
    private readonly LinkedGameNpcDatabase _secrets = new();
    private Func<int, Action<bool>, bool>? _openSecretMenu;
    private bool _validSecret;
    internal void SetSecretMenuOpener(Func<int, Action<bool>, bool> opener) => _openSecretMenu = opener;
    private int _savedEquippedA;
    private int _savedEquippedB;
    private int _wildLevel;
    private int _wildSpawnCounter;
    private bool _present;
    private bool _won;
    private int _roundResult;
    private bool _roundEntitiesActive;
    private bool _inventoryOverridden;
    private bool _prizePrepared;
    private bool _canAffordRound;
    private bool _ringPrize;
    private int _fadeCounter;

    internal WildTokayGameEvent(
        RoomEventContext context,
        TokayInteractionDatabase interactions,
        WildTokayGameDatabase database)
        : base(context, interactions)
    {
        _database = database;
        _wildSchedule = new WildTokaySpawnSchedule(
            database, () => context.Entities.NextRandomValue());
    }

    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => ScreenTransitionsDisabled;
    public bool HasState => _stage != WildTokayGameStage.Inactive;
    internal WildTokayGameStage Stage => _stage;
    internal int Counter => _counter;
    public bool ScreenTransitionsDisabled =>
        _stage == WildTokayGameStage.Playing;

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        Cancel();
        if (group == _database.PastGameGroup &&
            room.Id == _database.PastGameRoom &&
            FindActor(0x48, 0x0d) is { Active: true } &&
            Context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet))
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
                ((TokayCharacter)npc).ScriptOwnsNativeUpdate = true;
                BeginPastManager();
                return true;
            case 0x19:
                _actor = npc;
                ((TokayCharacter)npc).ScriptOwnsNativeUpdate = true;
                BeginPresentManager();
                return true;
            default:
                return false;
        }
    }

    public void UpdateFrame()
    {
        int existingParticipants = _participants.Count;
        var actor = _actor as TokayCharacter;
        UpdateController();
        actor?.RunNativeUpdate(Context.Player);
        if (_roundEntitiesActive)
            UpdateParticipants(existingParticipants);
    }

    private void UpdateController()
    {
        if (_stage == WildTokayGameStage.Inactive)
            return;
        if (_stage == WildTokayGameStage.Playing)
        {
            UpdateGame();
            return;
        }
        if (_stage is WildTokayGameStage.FadeOut or WildTokayGameStage.FadeIn or
            WildTokayGameStage.ReturnFadeOut or WildTokayGameStage.ReturnFadeIn)
        {
            UpdateFade();
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
        if (Context.DialogueOpen)
            return;

        AdvanceClosedDialogue();
    }

    public void Cancel()
    {
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
        _reward?.Finish(Context.Player);
        _reward = null;
        ClearGameEntities();
        RemovePrizeAccessory();
        RestoreFadePresentation();
        RestoreGameTiles();
        RestoreInventory();
        UnlockInput();
        _actor = null;
        _counter = 0;
        _wildSchedule.Clear();
        _prizePrepared = false;
        _ringPrize = false;
        _stage = WildTokayGameStage.Inactive;
    }

    private void BeginPastManager()
    {
        if (!Context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet))
        {
            ShowDialogueOnly(0x0a1c);
            return;
        }
        _present = false;
        PreparePrize();
        LockInput(onlyIfUnlocked: true);
        Show(0x0a10);
        _stage = WildTokayGameStage.PastManagerPrizeIntro;
    }

    private void BeginPresentManager()
    {
        _present = true;
        OracleSaveData save = Context.Rooms.SaveData;
        if (!save.HasGlobalFlag(_database.FinishedGameFlag))
        {
            ShowDialogueOnly(0x0a67);
            return;
        }
        if (save.HasGlobalFlag(_database.DoneSecretFlag))
        {
            ShowReturnSecret(0x0a53);
            return;
        }
        LockInput(onlyIfUnlocked: true);
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
                BeginWait(10, WildTokayGameStage.PastManagerRaisePrize);
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
                BeginWait(_present ? 40 : 20, WildTokayGameStage.Begin);
                break;
            case WildTokayGameStage.StartText:
                UnlockInput();
                SpawnMeat();
                Context.Sound.PlaySound(_database.SoundWhistle);
                _wildSpawnCounter = _database.GameSpawnDelay;
                _stage = WildTokayGameStage.Playing;
                break;
            case WildTokayGameStage.ResultText:
                BeginWait(_present ? 60 : 20, WildTokayGameStage.Finish);
                break;
            case WildTokayGameStage.LossPrompt:
                if (_present)
                {
                    _pendingChoice = TakeChoice();
                    BeginWait(20, WildTokayGameStage.PresentResolveLoss);
                }
                else ResolveLossPrompt(TakeChoice());
                break;
            case WildTokayGameStage.PresentSecretPrompt:
                _pendingChoice = TakeChoice();
                BeginWait(20, WildTokayGameStage.PresentResolveSecretPrompt);
                break;
            case WildTokayGameStage.PresentEnteringSecret:
                break;
            case WildTokayGameStage.PresentPlayPrompt:
                _pendingChoice = TakeChoice();
                BeginWait(2, WildTokayGameStage.PresentResolvePlay);
                break;
            case WildTokayGameStage.PresentRulesPrompt:
                _pendingChoice = TakeChoice();
                BeginWait(2, WildTokayGameStage.PresentResolveRules);
                break;
            case WildTokayGameStage.PresentRulesExplanation:
                _pendingChoice = TakeChoice();
                BeginWait(20, WildTokayGameStage.PresentResolveRules);
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
            case WildTokayGameStage.PastManagerRaisePrize:
                RaisePrize();
                BeginWait(40, WildTokayGameStage.PastManagerPlayPrompt);
                break;
            case WildTokayGameStage.PastManagerRulesPrompt:
                LowerPrize();
                ShowChoice(0x0a14);
                break;
            case WildTokayGameStage.PastRulesExplanation:
                ShowChoice(0x0a26);
                _stage = WildTokayGameStage.PastManagerRulesPrompt;
                break;
            case WildTokayGameStage.BeginText:
                if (_present) _wildLevel = 2;
                Show(_present ? 0x0a4c : 0x0a15);
                _stage = WildTokayGameStage.IntroText;
                break;
            case WildTokayGameStage.ReleaseAfterPrize:
                FinishInteraction();
                break;
            case WildTokayGameStage.PastManagerDeclined:
                LowerPrize();
                Show(0x0a1a);
                break;
            case WildTokayGameStage.PastManagerNoRupees:
                LowerPrize();
                Show(0x0a1b);
                break;
            case WildTokayGameStage.Begin:
                BeginGameFade();
                break;
            case WildTokayGameStage.FadeIn:
                Context.Sound.PlaySound(OracleSoundEngine.MusMinigame);
                BeginFade(WildTokayGameStage.FadeIn);
                break;
            case WildTokayGameStage.StartText:
                Show(0x0a16);
                break;
            case WildTokayGameStage.ResultText:
                if (_present)
                    BeginWait(60, WildTokayGameStage.Finish);
                else
                    Show(_won ? 0x0a18 : 0x0a17);
                break;
            case WildTokayGameStage.Finish:
                BeginGameReturn();
                break;
            case WildTokayGameStage.PastGivePrize:
                GivePrize();
                break;
            case WildTokayGameStage.PresentGiveBombUpgrade:
                GivePresentBombUpgrade();
                break;
            case WildTokayGameStage.PresentResolvePlay:
                BeginWait(20, _pendingChoice == 0
                    ? WildTokayGameStage.PresentRulesPrompt
                    : WildTokayGameStage.PresentDeclined);
                break;
            case WildTokayGameStage.PresentDeclined:
                ShowDialogueOnly(0x0a52);
                break;
            case WildTokayGameStage.PresentRulesPrompt:
                ShowChoice(0x0a4a);
                break;
            case WildTokayGameStage.PresentResolveRules:
                BeginWait(20, _pendingChoice == 0
                    ? WildTokayGameStage.BeginText
                    : WildTokayGameStage.PresentRulesExplanation);
                break;
            case WildTokayGameStage.PresentRulesExplanation:
                ShowChoice(0x0a4b);
                break;
            case WildTokayGameStage.PresentResolveSecretPrompt:
                if (_pendingChoice != 0)
                {
                    BeginWait(30, WildTokayGameStage.PresentSecretDeclined);
                    break;
                }
                _stage = WildTokayGameStage.PresentEnteringSecret;
                if (_openSecretMenu is null || !_openSecretMenu(0x05, valid =>
                    {
                        _validSecret = valid;
                        BeginWait(20, WildTokayGameStage.PresentSecretResult);
                    }))
                    throw new InvalidOperationException("tokayGameManagerScript_present: askforsecret TOKAY_SECRET could not acquire MENU_SECRET.");
                break;
            case WildTokayGameStage.PresentSecretDeclined:
                ShowDialogueOnly(0x0a46);
                break;
            case WildTokayGameStage.PresentSecretResult:
                if (!_validSecret) ShowDialogueOnly(0x0a48);
                else
                {
                    Context.Rooms.SaveData.SetGlobalFlag(_database.BeganSecretFlag);
                    ShowChoice(0x0a47);
                    _stage = WildTokayGameStage.PresentPlayPrompt;
                }
                break;
            case WildTokayGameStage.PresentSecretReward:
                Context.Rooms.SaveData.SetGlobalFlag(_database.DoneSecretFlag);
                ShowReturnSecret(0x0a50);
                break;
            case WildTokayGameStage.PresentResolveLoss:
                ResolveLossPrompt(_pendingChoice);
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
                BeginWait(30, WildTokayGameStage.ReleaseAfterPrize);
                break;
            case WildTokayGameStage.PresentBombReward:
                BeginWait(30, WildTokayGameStage.PresentSecretReward);
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
            BeginWait(20, WildTokayGameStage.PastManagerDeclined);
            return;
        }
        if (!_canAffordRound)
        {
            BeginWait(20, WildTokayGameStage.PastManagerNoRupees);
            return;
        }
        Context.Inventory.AddRupees(-10);
        BeginWait(20, WildTokayGameStage.PastManagerRulesPrompt);
    }

    private void ResolvePastRulesPrompt()
    {
        if (TakeChoice() != 0)
        {
            BeginWait(20, WildTokayGameStage.PastRulesExplanation);
            return;
        }
        BeginWait(20, WildTokayGameStage.BeginText);
    }

    private void BeginGame()
    {
        _roundEntitiesActive = true;
        _roundResult = 0;
        // tokayRunSubid0d/tokayRunSubid19 delete the manager after the start
        // fade, immediately before INTERAC_WILD_TOKAY_CONTROLLER initializes.
        RequireActor().SetActive(false);
        _savedEquippedA = Context.Inventory.EquippedA;
        _savedEquippedB = Context.Inventory.EquippedB;
        Context.Inventory.SetScriptedEquippedItems(
            InventoryState.ItemNone, InventoryState.ItemBracelet);
        _inventoryOverridden = true;
        Context.Player.SetScriptedCoordinateHigh(
            horizontal: false, coordinate: _database.GameLinkY);
        Context.Player.SetScriptedCoordinateHigh(
            horizontal: true, coordinate: _database.GameLinkX);
        ApplyGameTiles();
        _wildSchedule.Begin(_wildLevel);
        _participants.Clear();
        _meats.Clear();
        foreach (NpcCharacter statue in Context.Entities.Entities<NpcCharacter>())
        {
            if (_present && statue.Record is { Id: 0x48, SubId: >= 0x1a and <= 0x1c })
                statue.SetActive(false);
        }
        LockInput(onlyIfUnlocked: true);
        BeginWait(_database.GameStartDelay, WildTokayGameStage.FadeIn);
    }

    private void BeginGameFade()
    {
        Context.Sound.PlaySound(OracleSoundEngine.SndCtrlMediumFadeOut);
        BeginFade(WildTokayGameStage.FadeOut);
    }

    private void BeginFade(WildTokayGameStage stage)
    {
        CaptureFullScreenFade(Context.Hud.ZIndex + 1);
        _fadeCounter = 0;
        _stage = stage;
        Context.Fade.Color = new Color(
            1.0f, 1.0f, 1.0f,
            IsFadeOutStage(stage) ? 0.0f : 1.0f);
    }

    private void UpdateFade()
    {
        _fadeCounter++;
        float progress = Math.Min(
            _fadeCounter,
            RoomTransitionController.WarpFadeMaximumOffset) /
            RoomTransitionController.WarpFadeMaximumOffset;
        bool fadingOut = IsFadeOutStage(_stage);
        bool returning = _stage is WildTokayGameStage.ReturnFadeOut or
            WildTokayGameStage.ReturnFadeIn;
        Context.Fade.Color = new Color(
            1.0f, 1.0f, 1.0f, fadingOut ? progress : 1.0f - progress);
        if (_fadeCounter < RoomTransitionController.WarpFadeFrames)
            return;

        _fadeCounter = 0;
        if (fadingOut)
        {
            if (returning)
            {
                CompleteGameReturnBoundary();
                BeginFade(WildTokayGameStage.ReturnFadeIn);
                return;
            }
            BeginGame();
            return;
        }

        RestoreFadePresentation();
        if (returning)
        {
            ContinueGameResultAfterReturn();
            return;
        }
        BeginWait(_database.GameFadeInDelay, WildTokayGameStage.StartText);
    }

    private static bool IsFadeOutStage(WildTokayGameStage stage) =>
        stage is WildTokayGameStage.FadeOut or WildTokayGameStage.ReturnFadeOut;

    private void RestoreFadePresentation()
    {
        _fadeCounter = 0;
        ReleaseFullScreenFade();
    }

    private void UpdateGame()
    {
        // $70's slot precedes its spawned meat and participant slots. A
        // participant result is consumed on the following controller update.
        if (_roundResult != 0)
        {
            EndRound(_roundResult == 1);
            return;
        }
        if (_meats.All(meat => meat.Finished || meat.Thrown || meat.Lifted))
            SpawnMeat();
        if (--_wildSpawnCounter > 0) return;
        _wildSpawnCounter = _database.GameSpawnDelay;
        WildTokaySpawnInstruction instruction = _wildSchedule.Advance();
        if (instruction.Code != 0) SpawnParticipants(instruction.Code, instruction.Final);
    }

    private void UpdateParticipants(int count)
    {
        for (int index = 0; index < count; index++)
        {
            WildParticipantState participant = _participants[index];
            if (!participant.Actor.Active)
                continue;
            if (participant.CatchPause > 0)
            {
                participant.CatchPause--;
                UpdateParticipantAccessory(participant);
                continue;
            }
            if (!participant.HoldingMeat)
            {
                foreach (WildTokayMeat meat in _meats)
                {
                    if (!meat.Finished && meat.Thrown &&
                        CanParticipantCatch(participant.Actor.Position, meat.Position))
                    {
                        meat.Catch();
                        participant.HoldingMeat = true;
                        participant.Actor.SetScriptAnimation(
                            Interactions.Animation(participant.FromRight ? 8 : 7));
                        participant.CatchPause = 6;
                        CreateParticipantAccessory(participant);
                        Context.Sound.PlaySound(_database.SoundOpenChest);
                        break;
                    }
                }
            }
            participant.Actor.SetStatePosition(
                participant.Actor.Position + Vector2.Down * participant.Speed);
            UpdateParticipantAccessory(participant);
            // wildTokayParticipantSubstate2 keeps the participant while
            // (yh + $08) < $90, then handles its result at Y $88.
            if (participant.Actor.Position.Y < 0x88)
            {
                participant.Actor.AdvanceAnimationUpdates(1);
                continue;
            }
            participant.Actor.SetActive(false);
            RemoveParticipantAccessory(participant);
            if (!participant.HoldingMeat)
            {
                _roundResult = 0xff;
            }
            else if (participant.Red)
            {
                _roundResult = 1;
            }
        }
    }

    internal static bool CanParticipantCatch(Vector2 actor, Vector2 meat) =>
        ((Mathf.FloorToInt(meat.X) - Mathf.FloorToInt(actor.X) + 10) & 0xff) <= 20 &&
        ((Mathf.FloorToInt(meat.Y) - Mathf.FloorToInt(actor.Y) + 10) & 0xff) <= 20;

    private void SpawnParticipants(int code, bool final)
    {
        if (code is 1 or 3)
            SpawnParticipant(fromRight: false, red: final);
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
        NpcCharacter actor = Context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(record, $"WildTokay_{_participants.Count}"));
        // Object data stores the above-screen spawn as byte coordinate $f8;
        // render it at -8 while retaining $f8 in the source-derived record.
        actor.SetStatePosition(
            OracleObjectMath.NormalizeSourceScreenPosition(actor.Position));
        // interactionInitGraphics selects `$48's default animation `$02;
        // interactionAnimateBasedOnSpeed then preserves that downward facing.
        actor.SetScriptAnimation(
            Interactions.Animation(_database.ParticipantAnimation));
        actor.SetBlocksLink(false);
        actor.SetAnimationRate(0);
        if (red)
            actor.SetBasePalette(2);
        float speed = _wildLevel >= 3 ? 0.625f : 0.5f;
        _participants.Add(new WildParticipantState(actor, fromRight, red, speed));
    }

    private void SpawnMeat()
    {
        WildTokayMeat meat =
            Context.Entities.Spawn<WildTokayMeat>(new WildTokayMeatSpawn());
        _meats.Add(meat);
    }

    private void ApplyGameTiles()
    {
        if (_gameRoom is not null)
            throw new InvalidOperationException(
                "Wild Tokay tried to apply its arena tiles twice.");

        _gameRoom = Context.Rooms.CurrentRoom;
        var writes = new Dictionary<int, byte>();
        foreach (WildTokayStartTileRecord record in _database.StartTiles)
        {
            Vector2 point = PackedPositionCenter(record.PackedPosition);
            _originalGameTiles.Add(
                record.PackedPosition, _gameRoom.GetMetatile(point));
            writes.Add(record.PackedPosition, (byte)record.Tile);
        }
        _gameRoom.ApplyRoomInitializationChanges(
            writes, Context.AnimationTick());
    }

    private void RestoreGameTiles()
    {
        if (_gameRoom is null)
            return;
        _gameRoom.ApplyRoomInitializationChanges(
            _originalGameTiles, Context.AnimationTick());
        _originalGameTiles.Clear();
        _gameRoom = null;
    }

    private void CreateParticipantAccessory(WildParticipantState participant)
    {
        WildTokayMeatAccessoryRecord visual = _database.MeatAccessory(
            participant.Actor.CurrentAnimationParameter);
        Vector2 position = participant.Actor.Position +
            new Vector2(visual.XOffset, visual.YOffset);
        NpcRecord parent = participant.Actor.BaseRecord;
        var record = new NpcRecord(
            parent.Group, parent.Room, 0x63, 0x73,
            Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X),
            0, 0, visual.Sprite, visual.TileBase, visual.Palette, 0, false,
            visual.EncodedAnimation, visual.EncodedAnimation,
            visual.EncodedAnimation, visual.EncodedAnimation, string.Empty,
            NpcImplementationClassification.EventOwned);
        participant.Accessory = Context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                record, $"WildTokayMeatAccessory_{_participants.IndexOf(participant)}"));
        participant.Accessory.SetScriptAnimation(visual.EncodedAnimation);
        participant.Accessory.SetAnimationRate(0.0f);
        participant.Accessory.SetBlocksLink(false);
        participant.Accessory.SetFixedDrawPriority(
            NpcCharacter.InFrontOfLinkZIndex);
        UpdateParticipantAccessory(participant);
    }

    private void UpdateParticipantAccessory(WildParticipantState participant)
    {
        if (participant.Accessory is not { Active: true } accessory)
            return;
        WildTokayMeatAccessoryRecord visual = _database.MeatAccessory(
            participant.Actor.CurrentAnimationParameter);
        accessory.SetStatePosition(
            participant.Actor.Position +
            new Vector2(visual.XOffset, visual.YOffset));
    }

    private static void RemoveParticipantAccessory(
        WildParticipantState participant)
    {
        if (participant.Accessory is { } accessory &&
            GodotObject.IsInstanceValid(accessory))
        {
            accessory.SetActive(false);
        }
        participant.Accessory = null;
    }

    private void ClearGameEntities()
    {
        _roundEntitiesActive = false;
        _roundResult = 0;
        foreach (WildParticipantState participant in _participants)
        {
            if (GodotObject.IsInstanceValid(participant.Actor))
                participant.Actor.SetActive(false);
            RemoveParticipantAccessory(participant);
        }
        foreach (WildTokayMeat meat in _meats)
        {
            if (GodotObject.IsInstanceValid(meat))
                meat.Finish();
        }
        _participants.Clear();
        _meats.Clear();
    }

    private static Vector2 PackedPositionCenter(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);

    private void RaisePrize()
    {
        NpcCharacter manager = RequireActor();
        manager.SetScriptAnimation(Interactions.Animation(0x06));
        WildTokayPrizeRecord visual =
            _database.Prize(_ringPrize ? 5 : _wildLevel);
        Vector2 position = manager.Position + new Vector2(0, -12);
        var record = new NpcRecord(
            manager.Record.Group, manager.Record.Room, 0x63,
            visual.AccessorySubId,
            Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X),
            0, 0, visual.Sprite, visual.TileBase, visual.Palette, 0, false,
            visual.Animation, visual.Animation, visual.Animation,
            visual.Animation, string.Empty,
            NpcImplementationClassification.EventOwned);
        _prizeAccessory = Context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(record, "WildTokayPrizeAccessory"));
        _prizeAccessory.SetStatePosition(position);
        _prizeAccessory.SetScriptAnimation(visual.Animation);
        _prizeAccessory.SetAnimationRate(0.0f);
        _prizeAccessory.SetBlocksLink(false);
                Context.Sound.PlaySound(Interactions.SoundGetSeed);
    }

    private void LowerPrize()
    {
        RequireActor().SetScriptAnimation(Interactions.Animation(0x02));
        RemovePrizeAccessory();
    }

    private void RemovePrizeAccessory()
    {
        if (_prizeAccessory is { } accessory &&
            GodotObject.IsInstanceValid(accessory))
        {
            accessory.SetActive(false);
        }
        _prizeAccessory = null;
    }

    private void EndRound(bool won)
    {
        _won = won;
        LockInput(onlyIfUnlocked: true);
        Context.Sound.PlaySound(won ? _database.SoundSuccess : _database.SoundError);
        BeginWait(30, WildTokayGameStage.ResultText);
    }

    private void BeginGameReturn()
    {
        // substate 6 restores B/A, sets ROOMFLAG_40, invalidates active music,
        // and installs a same-room warp whose wWarpTransition2 `$03 selects
        // the ordinary 32-update white fade.
        RestoreInventory();
        Context.Rooms.SaveData.SetRoomFlag(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlag40,
            value: true);
        BeginFade(WildTokayGameStage.ReturnFadeOut);
    }

    private void CompleteGameReturnBoundary()
    {
        // The source controller warps back to the room, recreating the manager
        // before its result/prize script runs. Keep the room identity stable,
        // but reproduce the same entity, tile, flag, and music reload boundary.
        ClearGameEntities();
        RequireActor().SetActive(true);
        RestoreGameTiles();
        // The hardcoded warp initially decodes packed `$57, but during room
        // initialization the recreated `$48:$0d/`$19 manager sees ROOMFLAG_40
        // and overwrites Link's high coordinates with `$48,$50. Reproduce the
        // final observable position while the screen is fully white.
        Context.Player.WarpTo(new Vector2(
            _database.GameLinkX, _database.GameLinkY));
        Context.Player.Face(Vector2I.Up);
        Context.Rooms.SaveData.SetRoomFlag(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlag40,
            value: false);
        Context.Sound.PlayRoomMusic(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
            Context.Rooms.SaveData);
    }

    private void ContinueGameResultAfterReturn()
    {
        if (_won)
        {
            if (_present)
            {
                Show(0x0a4f);
                _stage = WildTokayGameStage.PresentWinText;
            }
            else
            {
                // The recreated past manager waits 30 updates before calling
                // tokayGame_givePrizeToLink.
                BeginWait(30, WildTokayGameStage.PastGivePrize);
            }
            return;
        }
        _canAffordRound = Context.Inventory.Rupees >= 10;
        ShowChoice(_present ? 0x0a4d : 0x0a19);
        _stage = WildTokayGameStage.LossPrompt;
    }

    private void ResolveLossPrompt(int choice)
    {
        if (choice != 0)
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
            if (!_canAffordRound)
            {
                BeginWait(20, WildTokayGameStage.PastManagerNoRupees);
                return;
            }
            Context.Inventory.AddRupees(-10);
        }
        BeginWait(20, WildTokayGameStage.BeginText);
    }

    private void GivePrize()
    {
        if (_wildLevel == 4 && _ringPrize)
        {
            int ring = _ringDatabase.SelectRing(
                2, (byte)Context.Entities.NextRandomValue());
            Vector2 position = Context.Player.Position;
            _reward = Context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
                Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, 0,
                Mathf.FloorToInt(position.Y), Mathf.FloorToInt(position.X),
                "TREASURE_OBJECT_RING_00", "scriptHelper.s:tokayGame_givePrizeToLink@giveRingToLink")
            {
                SpawnMode = 0,
                GrabMode = 2,
                InventoryWrite = GroundTreasureInventoryWrite.UnappraisedRing,
                InventoryParameter = ring,
                RoomFlagTiming = GroundTreasureRoomFlagTiming.Never
            }, Context.Player);
            _prizePrepared = false;
            _stage = WildTokayGameStage.Prize;
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
        Context.Inventory.ApplyTokayBombCapacityUpgrade();
        _reward = Grant(
            0x61, 0, "TREASURE_OBJECT_BOMB_UPGRADE_00", "tokayGiveBombUpgrade");
        _stage = WildTokayGameStage.PresentBombReward;
    }

    private void PreparePrize()
    {
        if (_prizePrepared)
            return;
        _wildLevel = Math.Clamp(
            (int)Context.Rooms.SaveData.ReadWramByte(_database.WildLevelAddress), 0, 4);
        _ringPrize =
            _wildLevel == 4 && (Context.Entities.NextRandomValue() & 0x07) == 0;
        _canAffordRound = Context.Inventory.Rupees >= 10;
        _prizePrepared = true;
    }

    private void RestoreInventory()
    {
        if (!_inventoryOverridden)
            return;
        Context.Inventory.SetScriptedEquippedItems(_savedEquippedB, _savedEquippedA);
        _inventoryOverridden = false;
        _wildSchedule.Clear();
    }

    private void BeginWait(int frames, WildTokayGameStage next)
    {
        _counter = frames;
        _nextStage = next;
        _stage = WildTokayGameStage.Wait;
    }

    private NpcCharacter? FindActor(int id, int subId) =>
        Context.Entities.Entities<NpcCharacter>()
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
            Context.Treasures.GetObject(objectName);
        return Context.GrantScriptTreasure(
            Context.Rooms.ActiveGroup,
            Context.Rooms.CurrentRoom.Id,
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

    private void ShowReturnSecret(int textId)
    {
        string secret = _secrets.GenerateSecret(0x15, Context.Rooms.SaveData);
        Context.ShowDialogue(Interactions.Text(textId).Replace("\\secret1", secret, StringComparison.Ordinal));
        _stage = WildTokayGameStage.DialogueOnly;
    }

    private int TakeChoice() =>
        RequireDialogueChoice("Wild Tokay prompt closed without a text-option result.");

    private void FinishInteraction()
    {
        if (_actor is TokayCharacter actor) actor.ScriptOwnsNativeUpdate = false;
        if (_actor?.Record.SubId == 0x0d &&
            Context.Inventory.HasTreasure(TreasureDatabase.TreasureBracelet))
            PreparePrize();
        RemovePrizeAccessory();
        RestoreFadePresentation();
        UnlockInput();
        _actor = null;
        _stage = WildTokayGameStage.Inactive;
    }

    private void WriteSaveByte(int address, int value)
    {
        OracleSaveData save = Context.Rooms.SaveData;
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
        internal int CatchPause { get; set; }
        internal NpcCharacter? Accessory { get; set; }
    }
}

internal enum WildTokayGameStage
{
    Inactive,
    PastRulesExplanation,
    BeginText,
    ReleaseAfterPrize,
    DialogueOnly,
    Wait,
    PastManagerPrizeIntro,
    PastManagerRaisePrize,
    PastManagerPlayPrompt,
    PastManagerRulesPrompt,
    PastManagerDeclined,
    PastManagerNoRupees,
    IntroText,
    Begin,
    FadeOut,
    FadeIn,
    StartText,
    Playing,
    ResultText,
    Finish,
    ReturnFadeOut,
    ReturnFadeIn,
    LossPrompt,
    PastGivePrize,
    Prize,
    PresentSecretPrompt,
    PresentResolveSecretPrompt,
    PresentSecretDeclined,
    PresentEnteringSecret,
    PresentSecretResult,
    PresentSecretReward,
    PresentResolveLoss,
    PresentResolvePlay,
    PresentDeclined,
    PresentResolveRules,
    PresentRulesExplanation,
    PresentPlayPrompt,
    PresentRulesPrompt,
    PresentWinText,
    PresentGiveBombUpgrade,
    PresentBombReward
}

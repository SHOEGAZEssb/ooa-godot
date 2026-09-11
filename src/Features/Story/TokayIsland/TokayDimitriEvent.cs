using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// tokayWithDimitri1Script and tokayWithDimitri2Script for the coordinated
/// INTERAC_TOKAY $48:$0f-$10 pair.
/// </summary>
internal sealed class TokayDimitriEvent : TokayScriptEvent, IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent
{
    private readonly TokayRescueEmberDatabase _embers = new();
    private readonly List<TokayRescueEmberRoomEntity> _seedEffects = new();
    private TokayDimitriStage _stage;
    private TokayDimitriStage _nextStage;
    private NpcCharacter? _actor;
    private int _counter;
    private NpcCharacter? _departingFirst;
    private NpcCharacter? _departingSecond;
    private int _firstMoveCounter;
    private int _secondMoveCounter;
    private Vector2 _firstPosition;
    private Vector2 _secondPosition;
    private bool _departureMenusEnabled;

    internal TokayDimitriEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool HasState => _stage != TokayDimitriStage.Inactive || _seedEffects.Count != 0;
    internal TokayDimitriStage Stage => _stage;
    public bool MenusDisabled => _stage != TokayDimitriStage.Inactive && !_departureMenusEnabled;

    public bool Matches(int group, OracleRoomData room) =>
        group == 0 && room.Id == 0xaa &&
        FindActor(0x0f) is { Active: true } &&
        (Context.Rooms.SaveData.ReadWramByte(Interactions.DimitriStateAddress) & 0x01) == 0;

    public void Start(OracleRoomData room)
    {
        if (!Matches(Context.Rooms.ActiveGroup, room))
            throw new InvalidOperationException(
                $"Tokay Dimitri introduction cannot start in " +
                $"{Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        _actor = FindActor(0x0f) ?? throw new InvalidOperationException(
            "tokayWithDimitri1Script lost INTERAC_TOKAY $48:$0f on room entry.");
        // Destination objects are preloaded before Link reaches the room.
        // Run the script's disableinput/showtext on the first unfrozen object
        // update, so initTextbox observes Link's destination coordinates.
        _stage = TokayDimitriStage.PendingIntro;
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record.Id != 0x48 ||
            npc.Record.SubId is not (0x0f or 0x10))
        {
            return false;
        }

        _actor = npc;
        if (npc.Record.SubId == 0x10)
        {
            FaceActorToLink(npc);
            Show(0x0a1e);
            _stage = TokayDimitriStage.DialogueOnly;
            return true;
        }

        EventResources.LockInput();
        FaceActorToLink(npc);
        Show(0x0a1f);
        _stage = TokayDimitriStage.TradeIntro;
        return true;
    }

    public void UpdateFrame()
    {
        UpdateScriptFrame();
        UpdateEffects();
    }

    public void UpdateDuringDialogueFrame() => UpdateEffects();

    private void UpdateEffects()
    {
        foreach (var effect in _seedEffects)
            if (GodotObject.IsInstanceValid(effect)) effect.UpdateNative(Context.DialogueOpen);
        _seedEffects.RemoveAll(effect => !GodotObject.IsInstanceValid(effect) || effect.Finished);
    }

    private void UpdateScriptFrame()
    {
        if (_stage == TokayDimitriStage.Inactive)
            return;
        if (_stage == TokayDimitriStage.PendingIntro)
        {
            EventResources.LockInput();
            _actor!.SetFacingDirection(Vector2I.Down);
            Show(0x0a1d);
            _stage = TokayDimitriStage.IntroFirstText;
            return;
        }
        if (_stage == TokayDimitriStage.Wait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (Context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayDimitriStage.DialogueOnly:
                FinishInteraction();
                break;
            case TokayDimitriStage.IntroSecondText:
                Context.Entities.Entities<DimitriCompanionRoomEntity>().SingleOrDefault()?.BeginIntroResponse();
                FinishInteraction();
                break;
            case TokayDimitriStage.IntroFirstText:
                FindActor(0x0f)!.SetFacingDirection(Vector2I.Right);
                BeginWait(30, TokayDimitriStage.IntroSecondText);
                break;
            case TokayDimitriStage.TradeIntro:
                if (!Context.Inventory.HasTreasure(0x20) || Context.Inventory.EmberSeeds == 0)
                    FinishInteraction();
                else
                {
                    ShowChoice(0x0a20);
                    _stage = TokayDimitriStage.TradePrompt;
                }
                break;
            case TokayDimitriStage.TradePrompt:
                if (TakeChoice() != 0)
                {
                    Show(0x0a22);
                    _stage = TokayDimitriStage.DialogueOnly;
                }
                else
                {
                    Show(0x0a23);
                    _stage = TokayDimitriStage.TradeAccepted;
                }
                break;
            case TokayDimitriStage.TradeAccepted:
                Context.Inventory.TryConsumeSeedsFromScript(0x20, 1);
                foreach (int subid in new[] { 0x0f, 0x10 })
                    ((TokayCharacter)FindActor(subid)!).NativeAnimation = TokayAnimationMode.Still;
                FindActor(0x10)!.SetFacingDirection(Vector2I.Down);
                foreach (Vector2 position in _embers.Positions)
                    _seedEffects.Add(Context.Entities.Spawn<TokayRescueEmberRoomEntity>(
                        new TokayRescueEmberSpawn(_embers.Record, position)));
                BeginWait(30, TokayDimitriStage.TradeSecondText);
                break;
            case TokayDimitriStage.TradeSecondText:
                BeginWait(60, TokayDimitriStage.TradeThirdText);
                break;
            case TokayDimitriStage.TradeThirdText:
                BeginDeparture();
                break;
            case TokayDimitriStage.Departing:
                UpdateDeparture();
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay Dimitri stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        foreach (var effect in _seedEffects)
            if (GodotObject.IsInstanceValid(effect)) effect.Finish();
        _seedEffects.Clear();
        foreach (int subid in new[] { 0x0f, 0x10 })
            if (FindActor(subid) is TokayCharacter { Active: true } tokay)
            {
                tokay.NativeAnimation = TokayAnimationMode.Animate;
                tokay.SetFacingDirection(subid == 0x0f ? Vector2I.Right : Vector2I.Up);
            }
        EventResources.UnlockInput();
        _actor = null;
        _counter = 0;
        ClearDeparture();
        _stage = TokayDimitriStage.Inactive;
    }

    private void EnterStage(TokayDimitriStage stage)
    {
        _stage = stage;
        switch (stage)
        {
            case TokayDimitriStage.IntroSecondText:
                FindActor(0x10)!.SetFacingDirection(Vector2I.Down);
                Show(0x0a1e);
                break;
            case TokayDimitriStage.TradeSecondText:
                Show(0x0a24);
                break;
            case TokayDimitriStage.TradeThirdText:
                Show(0x0a25);
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay Dimitri wait entered unsupported stage {stage}.");
        }
    }

    private void BeginDeparture()
    {
        _departingFirst = FindActor(0x0f) ?? throw new InvalidOperationException(
            "tokayWithDimitri1Script lost $48:$0f before moveleft.");
        _departingSecond = FindActor(0x10) ?? throw new InvalidOperationException(
            "tokayWithDimitri2Script lost $48:$10 before moveleft.");
        _firstPosition = _departingFirst.Position;
        _secondPosition = _departingSecond.Position;
        _firstMoveCounter = Interactions.DimitriFirstMoveCounter;
        _secondMoveCounter = Interactions.DimitriSecondMoveCounter;
        _departingFirst.SetScriptAnimation(Interactions.Animation(3));
        _departingSecond.SetScriptAnimation(Interactions.Animation(3));
        _stage = TokayDimitriStage.Departing;
        // scriptCmd_moveNpcLeft sets angle/animation/counter2 and yields.
        // Both slots observe var3e bit $08 in this source-ordered pass.
    }

    private void UpdateDeparture()
    {
        if (_departingFirst is { } first &&
            AdvanceDeparture(first, ref _firstPosition, ref _firstMoveCounter))
        {
            first.SetActive(false);
            _departingFirst = null;
            _departureMenusEnabled = true; // enablemenu precedes scriptend.
        }
        if (_departingSecond is not { } second ||
            !AdvanceDeparture(second, ref _secondPosition, ref _secondMoveCounter))
            return;

        second.SetActive(false);
        OracleSaveData save = Context.Rooms.SaveData;
        byte state = save.ReadWramByte(Interactions.DimitriStateAddress);
        if (save.WriteWramByte(Interactions.DimitriStateAddress, (byte)(state | 0x02)))
            save.CommitInventoryChange();
        FinishInteraction();
    }

    private bool AdvanceDeparture(NpcCharacter actor, ref Vector2 position, ref int counter)
    {
        if (counter == 0) return true;
        if (--counter != 0)
        {
            actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                ref position, Interactions.DimitriDepartureSpeed, 0x18));
            // Preserve unsigned 8.8 room coordinates; OAM's left-edge wrap is
            // presentation. At x=$fa the actor is still partly visible at -6.
            actor.SetScriptDrawOffset(position.X >= 0xf0 ? new Vector2(-256, 0) : Vector2.Zero);
        }
        // No interactionAnimateAsNpc after var3e bit $04: retain the moveleft
        // pose while the counter runs. The zero update also yields.
        return false;
    }

    private void ClearDeparture()
    {
        _departingFirst?.SetScriptDrawOffset(Vector2.Zero);
        _departingSecond?.SetScriptDrawOffset(Vector2.Zero);
        _departingFirst = null;
        _departingSecond = null;
        _firstMoveCounter = 0;
        _secondMoveCounter = 0;
        _departureMenusEnabled = false;
    }

    private void BeginWait(int frames, TokayDimitriStage next)
    {
        _counter = frames;
        _nextStage = next;
        _stage = TokayDimitriStage.Wait;
    }

    private NpcCharacter? FindActor(int subId) =>
        Context.Entities.Entities<NpcCharacter>()
            .FirstOrDefault(npc => npc.Record.Id == 0x48 && npc.Record.SubId == subId);

    private void FaceActorToLink(NpcCharacter actor) =>
        actor.SetFacingDirection(RoomEventResources.DirectionToward(actor.Position, Context.Player.Position));

    private int TakeChoice() =>
        EventResources.RequireDialogueChoice("tokayWithDimitri1Script prompt closed without a text-option result.");

    private void FinishInteraction()
    {
        foreach (int subid in new[] { 0x0f, 0x10 })
        {
            if (FindActor(subid) is TokayCharacter { Active: true } tokay)
            {
                tokay.SetFacingDirection(subid == 0x0f ? Vector2I.Right : Vector2I.Up);
                tokay.NativeAnimation = TokayAnimationMode.Animate;
            }
        }
        EventResources.UnlockInput();
        _actor = null;
        ClearDeparture();
        _stage = TokayDimitriStage.Inactive;
    }
}

internal enum TokayDimitriStage
{
    Inactive,
    PendingIntro,
    DialogueOnly,
    Wait,
    IntroFirstText,
    IntroSecondText,
    TradeIntro,
    TradePrompt,
    TradeAccepted,
    TradeSecondText,
    TradeThirdText,
    Departing
}

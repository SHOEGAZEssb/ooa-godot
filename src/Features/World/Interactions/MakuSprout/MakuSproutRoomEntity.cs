using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Placed INTERAC_MAKU_SPROUT $88:$00 outside the active rescue script.
/// </summary>
internal sealed class MakuSproutRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IOrdinaryNpcEntity, INpcTalkLifecycle,
        IAlwaysUpdateDuringScreenTransitionRoomEntity,
        IUpdatesDuringDialogueRoomEntity
{
    private readonly MakuSproutRoomDatabase _database;
    private readonly OracleSaveData _save;
    private MakuSproutAdviceRecord? _advice;
    private int _talkCount;
    private int _happyRestoreUpdates;

    public MakuSproutRoomEntity(
        NpcCharacter npc,
        MakuSproutRoomDatabase database,
        OracleSaveData save)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        _save = save;
        if (!database.MatchesSprout(npc.Record))
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} is not the " +
                "imported room 1:38 Maku Sprout.");
        }
        ConfigureInitialState();
    }

    public NpcCharacter Npc => Entity;
    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.AnimateAsNpcOneUpdate(frame.Player);
        AdvancePostTalkWait();
    }

    public void UpdateDuringScreenTransition()
    {
        Entity.AdvanceAnimationUpdates(1);
        AdvancePostTalkWait();
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void OnNpcTalkStarted()
    {
        int state = _save.MakuTreeState;
        MakuSproutRoomRecord room = _database.Record;
        if (state is 1 or 2)
        {
            if (!_save.HasGlobalFlag(room.SavedFlag))
            {
                throw new InvalidOperationException(
                    "Room 1:38 rescue sprout entered ordinary NPC talk " +
                    "before GLOBALFLAG_MAKU_TREE_SAVED.");
            }
            ConfigureSavedLoop();
            return;
        }

        MakuSproutAdviceRecord advice = _advice is { } configured &&
            configured.State == state
                ? configured
                : _database.GetAdvice(state);
        _advice = advice;
        bool linked = _save.IsLinkedGame;
        MakuSproutDialogue dialogue = advice.Dialogue(
            linked, first: _talkCount == 0);
        Entity.SetDialogue(
            dialogue.TextId,
            dialogue.Message,
            canFace: false,
            dialogue.TextPosition);
        _save.SetMakuMapTextPast(dialogue.TextId & 0xff);
        if (advice.Mode(linked) == 1)
            Entity.SetScriptAnimation(room.SproutAnimation1);
        _talkCount++;
    }

    public void OnNpcTalkEnded()
    {
        if (_advice is { } advice &&
            advice.Mode(_save.IsLinkedGame) == 1)
        {
            // The source script waits one update after text closes before
            // restoring animation $00.
            _happyRestoreUpdates = 1;
        }
    }

    private void ConfigureInitialState()
    {
        MakuSproutRoomRecord room = _database.Record;
        int state = _save.MakuTreeState;
        if (state is 1 or 2)
        {
            // The unsaved rescue event takes ownership immediately after the
            // room entity list is parsed. A completed rescue instead resumes
            // the source script's TX_05d5 NPC loop.
            if (_save.HasGlobalFlag(room.SavedFlag))
                ConfigureSavedLoop();
            return;
        }

        _advice = _database.GetAdvice(state);
        Entity.SetCollisionRadii(room.SproutRadiusY, room.SproutRadiusX);
        Entity.SetScriptAnimation(
            _advice.Value.Mode(_save.IsLinkedGame) == 0
                ? room.SproutAnimation2
                : room.SproutAnimation0);
        MakuSproutDialogue first = _advice.Value.Dialogue(
            _save.IsLinkedGame, first: true);
        Entity.SetDialogue(
            first.TextId,
            first.Message,
            canFace: false,
            first.TextPosition);
    }

    private void ConfigureSavedLoop()
    {
        MakuSproutRoomRecord room = _database.Record;
        _advice = null;
        Entity.SetScriptAnimation(room.SproutAnimation0);
        Entity.SetCollisionRadii(room.SproutRadiusY, room.SproutRadiusX);
        Entity.SetDialogue(
            room.SavedTextId,
            room.SavedText,
            canFace: false,
            room.SavedTextPosition);
    }

    private void AdvancePostTalkWait()
    {
        if (_happyRestoreUpdates <= 0)
            return;
        _happyRestoreUpdates--;
        if (_happyRestoreUpdates == 0)
        {
            Entity.SetScriptAnimation(
                _database.Record.SproutAnimation0);
        }
    }
}

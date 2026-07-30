using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class Room20eNpcRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IRoomSaveStateEntity
{
    private readonly Room20eNpcDatabase _database;
    private readonly OracleSaveData? _save;
    private readonly string _upAnimation;
    private readonly string _rightAnimation;
    private readonly string _downAnimation;
    private readonly string _leftAnimation;
    private string _phase = string.Empty;
    private Room20eNpcBehavior _behavior;

    public Room20eNpcRoomEntity(
        NpcCharacter npc,
        Room20eNpcDatabase database,
        OracleSaveData? save)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        _save = save;
        _upAnimation = npc.Record.UpAnimation;
        _rightAnimation = npc.Record.RightAnimation;
        _downAnimation = npc.Record.DownAnimation;
        _leftAnimation = npc.Record.LeftAnimation;
        RefreshSaveState();
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        switch (_behavior)
        {
            case Room20eNpcBehavior.Push:
                Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
                break;
            case Room20eNpcBehavior.Animate:
                Entity.AnimateAsNpcOneUpdate(frame.Player);
                break;
            case Room20eNpcBehavior.FaceAnimate:
                Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unhandled room 2:0e NPC behavior {_behavior}.");
        }
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void RefreshSaveState()
    {
        bool savedNayru = _save?.HasGlobalFlag(
            OracleSaveData.GlobalFlagSavedNayru) == true;
        Room20eNpcStateRecord state =
            _database.State(Entity.Record, savedNayru);
        string phase = state.SavedNayru
            ? "after-saved-nayru"
            : "before-saved-nayru";
        if (_phase == phase)
            return;

        _phase = phase;
        _behavior = state.Behavior;
        Entity.ResetNativeNpcFacingState();
        Entity.SetStatePosition(new Vector2(state.X, state.Y));
        Entity.SetScriptPaletteOverride(_database.Palette(state));
        Entity.SetDialogue(
            state.TextId,
            state.Message,
            canFace: state.Behavior == Room20eNpcBehavior.FaceAnimate);
        if (state.AnimationMode == Room20eAnimationMode.Fixed)
        {
            Entity.SetScriptAnimation(state.Animation);
        }
        else
        {
            Entity.SetDirectionalAnimations(
                _upAnimation,
                _rightAnimation,
                _downAnimation,
                _leftAnimation);
            Entity.SetFacingDirection(state.InitialAnimation switch
            {
                0 => Vector2I.Up,
                1 => Vector2I.Right,
                2 => Vector2I.Down,
                3 => Vector2I.Left,
                _ => throw new InvalidOperationException(
                    $"Room 2:0e directional animation " +
                    $"${state.InitialAnimation:x2} is invalid in {state.Source}.")
            });
        }
    }
}

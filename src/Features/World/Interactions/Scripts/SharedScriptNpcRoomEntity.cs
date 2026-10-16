using Godot;
using System.Collections.Generic;

namespace oracleofages;

// The actor slot owns interactionRunScript and its native animation/collision tail.
internal sealed class SharedScriptNpcRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private bool _initialized, _storyInitialized;
    internal SharedNpcScriptHost? Script { get; set; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_storyInitialized;
    public void CompleteStoryInitialization()
    {
        _storyInitialized = true;
        Entity.ShowInitializedPresentation();
    }
    private readonly RoomEntityManager _entities;
    internal SharedScriptNpcRoomEntity(NpcCharacter npc, SharedNpcDatabase data, RoomEntityManager entities)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _entities = entities;
        npc.SetScriptButtonSensitive(true);
        if (npc.Record.Id == 0x36) npc.AppendScriptGraphics(data.ExtraGraphics(0x36));
        if (npc.Record.Id is 0x36 or 0x37 or 0x3c)
        {
            npc.SetDirectionalAnimations(data.Animation(npc.Record.Id, 0), data.Animation(npc.Record.Id, 1),
                data.Animation(npc.Record.Id, 2), data.Animation(npc.Record.Id, 3));
            npc.SetScriptAnimation(data.Animation(npc.Record.Id, npc.Record.Id == 0x36 ? 4 : npc.Record.Id == 0x37 ? 3 : 0));
        }
        if (npc.Record.Id == 0x39) npc.SetScriptAnimation(data.Animation(0x39, 5));
        if (npc.Record.Id == 0x5d) npc.SetDialogue(0x5714, data.Text(0x5714), false);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        if (!_initialized)
        {
            _initialized = true;
            if (Entity.Record.Id == 0x36)
                _entities.RequestNativeMusicSelection(SoundId.MusOverworld, 0xff);
            // These state-zero handlers return after graphics/script setup.
            if (Entity.Record.Id is not (0x37 or 0x2e)) return;
        }
        if (Entity.Record.Id == 0x36)
        {
            int parameter = Entity.ConsumeAnimationParameter();
            if (parameter != 0)
                NayruRescueMusicNoteRoomEntity.Spawn(_entities,
                    Entity.Position + new Vector2(parameter == 1 ? -6 : 8, -4), parameter == 1);
        }
        Script?.Advance(frame.Player);
        if (Entity.Record.Id != 0x2e && Script?.AnimationDisabled != true) Entity.AdvanceAnimationUpdates(1);
        if (Entity.Record.Id == 0x2e) Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        else if (Entity.Record.Id is 0x36 or 0x37)
        {
            Entity.PreventPlayerPassing(frame.Player);
            Entity.UpdateDrawPriority(frame.Player.Position, withTerrainEffects: true);
        }
        else Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}

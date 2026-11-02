
namespace Oracles;

// The actor slot owns interactionRunScript and its native animation/collision tail.
internal sealed class AgesScriptNpcRoomEntity : NpcRoomEntity
{
    public override void CompleteStoryInitialization()
    {
        base.CompleteStoryInitialization();
        Entity.ShowInitializedPresentation();
    }
    private readonly RoomEntityManager _entities;
    internal AgesScriptNpcRoomEntity(NpcCharacter npc, AgesNpcScriptDatabase data, RoomEntityManager entities)
        : base(npc)
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
    protected override bool CanUpdateNpc => Entity.Active;
    protected override bool InitializeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Record.Id == 0x36)
            _entities.RequestNativeMusicSelection(SoundId.MusOverworld, 0xff);
        return Entity.Record.Id is 0x37 or 0x2e;
    }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.Record.Id == 0x36)
        {
            int parameter = Entity.ConsumeAnimationParameter();
            if (parameter != 0)
                FloatingMusicNoteRoomEntity.Spawn(_entities,
                    Entity.Position + new Vector2(parameter == 1 ? -6 : 8, -4), parameter == 1);
        }
        return false;
    }
    protected override void AdvanceNpc(RoomEntityFrame frame)
    {
        if (Entity.Record.Id != 0x2e && (Script as AgesNpcScriptHost)?.AnimationDisabled != true) Entity.AdvanceAnimationUpdates(1);
        if (Entity.Record.Id == 0x2e) Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
        else if (Entity.Record.Id is 0x36 or 0x37)
        {
            Entity.PreventPlayerPassing(frame.Player);
            Entity.UpdateDrawPriority(frame.Player.Position, withTerrainEffects: true);
        }
        else Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
    }
    public override NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}

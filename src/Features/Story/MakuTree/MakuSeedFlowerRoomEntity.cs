using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MakuSeedFlowerRoomEntity : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly MakuTreeAdviceDatabase _data;
    private readonly MakuSeedEvent _parent;
    private readonly NpcCharacter _actor;
    public Node2D Node => _actor;
    public bool Finished => !_actor.Active;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal MakuSeedFlowerRoomEntity(MakuTreeAdviceDatabase data, MakuSeedEvent parent)
    {
        _data = data; _parent = parent;
        _actor = NpcCharacter.CreateFromRecord(data.Flower);
        _actor.SetBlocksLink(false); _actor.SetAnimationRate(0);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        _actor.SetScriptAnimation(data.Flower.UpAnimation);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_parent.HasTree) { _actor.SetActive(false); return; }
        // Subid $06 uses direct setanimation commands, which never write
        // the tree's var3b. The new parent's cleared var3b stays $00 even
        // while its face uses animation $04 after the reveal.
        _actor.SetScriptAnimation(_data.Flower.UpAnimation);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}

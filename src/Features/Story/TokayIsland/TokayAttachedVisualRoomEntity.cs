using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Room-owned $63 accessory or $6b:$09 shovel, linked to its source actor.</summary>
internal sealed partial class TokayAttachedVisualRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly NpcCharacter _parent;
    private readonly Texture2D _texture;
    private readonly Vector2 _frameOffset;
    internal Vector2 ParentOffset { get; set; }
    internal bool FollowParent { get; set; } = true;
    internal bool Retired { get; set; }
    public bool Finished => Retired || !GodotObject.IsInstanceValid(_parent) || !_parent.Active;
    public Node2D Node => this;
    public new void SetTransitionDrawOffset(Vector2 offset) => base.SetTransitionDrawOffset(offset);

    internal TokayAttachedVisualRoomEntity(NpcCharacter parent, NpcRecord record, Vector2 offset)
    {
        _parent = parent;
        ParentOffset = offset;
        Name = $"TokayChild_{record.Id:x2}_{record.SubId:x2}";
        var frame = OracleGraphicsCache.GetAnimationDefinition(record.DownAnimation).Frames[0];
        (_texture, _frameOffset) = NpcCharacter.BuildPositionedOamTexture(
            OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{record.SpriteName}.png"),
            frame.EncodedOam, record.TileBase, record.Palette, null, true);
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Finished) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished) return;
        Vector2 position = FollowParent ? _parent.Position + ParentOffset : ParentOffset;
        DrawTexture(_texture, position + _frameOffset + _parent.SourceOamWrapOffset + TransitionDrawOffset);
    }
}

internal sealed record TokayAttachedVisualSpawn(NpcCharacter Parent, NpcRecord Record, Vector2 Offset)
    : RoomEntitySpawn;

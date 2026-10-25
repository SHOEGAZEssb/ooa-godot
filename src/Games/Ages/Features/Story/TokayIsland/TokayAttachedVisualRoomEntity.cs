using Godot;
using System.Collections.Generic;

namespace Oracles;

/// <summary>Room-owned $63 accessory or $6b:$09 shovel, linked to its source actor.</summary>
internal sealed partial class TokayAttachedVisualRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _parent;
    private readonly Texture2D _texture;
    private readonly Vector2 _frameOffset;
    // INTERAC_EXCLAMATION_MARK $9f allocates an independent checked slot;
    // carried $63/$6b sprites have separate producer ownership paths.
    internal bool IsNativeExclamation { get; }
    internal bool NativeAllocationEnabled { get; }
    private bool _finished;
    private bool _initialized;
    private bool IsStatueAccessory=>NativeAllocationEnabled&&!IsNativeExclamation;
    public bool UpdatesDuringDialogue=>IsStatueAccessory&&!_initialized;
    public bool UpdatesDuringRoomEntityFreeze=>UpdatesDuringDialogue;
    internal Vector2 ParentOffset { get; set; }
    internal System.Func<Vector2>? NativeOffset { get; set; }
    internal bool FollowParent { get; set; } = true;
    internal bool Retired { get; set; }
    internal void DeleteNative(){Retired=true;_finished=true;}
    public bool Finished => NativeAllocationEnabled && !IsNativeExclamation ? _finished : Retired || !GodotObject.IsInstanceValid(_parent) || !_parent.Active;
    public Node2D Node => this;
    public new void SetTransitionDrawOffset(Vector2 offset) => base.SetTransitionDrawOffset(offset);

    internal TokayAttachedVisualRoomEntity(NpcCharacter parent, NpcRecord record, Vector2 offset)
    {
        _parent = parent;
        IsNativeExclamation = record.Id == InteractionId.ExclamationMark;
        NativeAllocationEnabled=IsNativeExclamation || parent.Record is { Id:0x48,SubId:0x0c or 0x0d or 0x19 or 0x1c or 0x1d };
        ParentOffset = offset;
        Name = $"TokayChild_{record.Id:x2}_{record.SubId:x2}";
        var frame = OracleGraphicsCache.GetAnimationDefinition(record.DownAnimation).Frames[0];
        (_texture, _frameOffset) = OracleOamCompositor.BuildPositionedOamTexture(
            OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{record.SpriteName}.png"),
            frame.EncodedOam, record.TileBase, record.Palette, null, true);
        ZIndex = IsStatueAccessory ? ObjectDrawPriority.FixedHighPriorityZIndex : ObjectDrawPriority.InFrontOfLinkZIndex;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (NativeAllocationEnabled && (Retired || !GodotObject.IsInstanceValid(_parent) || !_parent.Active))
            _finished=true;
        if (IsStatueAccessory && !Finished)
        {
            _initialized=true;
            Position=_parent.Position+(NativeOffset?.Invoke()??ParentOffset);
            Visible=_parent.ScriptVisible;
        }
        if (!Finished) QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished) return;
        Vector2 position = IsStatueAccessory ? Vector2.Zero : FollowParent ? _parent.Position + ParentOffset : ParentOffset;
        DrawTexture(_texture, position + _frameOffset + _parent.SourceOamWrapOffset + TransitionDrawOffset);
    }
}

internal sealed record TokayAttachedVisualSpawn(NpcCharacter Parent, NpcRecord Record, Vector2 Offset)
    : RoomEntitySpawn;

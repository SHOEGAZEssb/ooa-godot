using Godot;

namespace oracleofages;

/// <summary>Native $48 animation and initialization bytes, distinct from its script.</summary>
internal partial class TokayCharacter : NpcCharacter
{
    internal TokayAnimationMode NativeAnimation { get; set; } = TokayAnimationMode.Animate;
    internal int ReturnedItemDialogue { get; set; }
    internal bool ScriptOwnsNativeUpdate { get; set; }
    internal TokayAttachedVisualRoomEntity? Accessory { get; set; }

    internal void UpdateNative(Player player)
    {
        if (!Active || ScriptOwnsNativeUpdate) return;
        RunNativeUpdate(player);
    }
    internal void RunNativeUpdate(Player player)
    {
        if (!Active) return;
        switch (NativeAnimation)
        {
            case TokayAnimationMode.FaceLink:
                FaceLinkAndAnimateOneUpdate(player);
                break;
            case TokayAnimationMode.Animate:
                AnimateAsNpcOneUpdate(player);
                break;
            case TokayAnimationMode.Still:
                break;
        }
    }
}

internal enum TokayAnimationMode { Animate, FaceLink, Still }

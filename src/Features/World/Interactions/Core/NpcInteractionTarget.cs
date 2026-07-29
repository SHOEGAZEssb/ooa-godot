using System;

namespace oracleofages;

/// <summary>
/// One source-ordered A-button target and its optional native talk lifecycle.
/// The lifecycle owner is resolved together with the target so begin, end, and
/// cancellation never rescan a potentially changed room-entity list.
/// </summary>
internal sealed class NpcInteractionTarget
{
    private readonly INpcTalkLifecycle? _lifecycle;
    private bool _lifecycleStarted;

    public NpcInteractionTarget(
        NpcCharacter npc,
        INpcTalkLifecycle? lifecycle)
    {
        Npc = npc;
        _lifecycle = lifecycle;
        if (lifecycle is not null &&
            !ReferenceEquals(lifecycle.TalkNpc, npc))
        {
            throw new InvalidOperationException(
                $"{lifecycle.GetType().Name} registered a talk lifecycle for " +
                "a different NPC.");
        }
    }

    public NpcCharacter Npc { get; }

    public void Begin()
    {
        if (_lifecycleStarted || _lifecycle is null)
            return;
        _lifecycle.OnNpcTalkStarted();
        _lifecycleStarted = true;
    }

    public void End()
    {
        if (!_lifecycleStarted || _lifecycle is null)
            return;
        _lifecycle.OnNpcTalkEnded();
        _lifecycleStarted = false;
    }

    public void Cancel() => End();
}

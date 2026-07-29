using System;

namespace oracleofages;

/// <summary>
/// One explicitly ordered A-button interaction route. NPC handlers receive the
/// source-ordered target selected by RoomEntityManager; player handlers run
/// only when no NPC target claimed the probe.
/// </summary>
internal sealed class NpcInteractionHandler
{
    private readonly Func<NpcInteractionTarget, Player, bool>? _npcHandler;
    private readonly Func<Player, bool>? _playerHandler;

    private NpcInteractionHandler(
        string source,
        NpcInteractionTargetKind targetKind,
        Func<NpcInteractionTarget, Player, bool>? npcHandler,
        Func<Player, bool>? playerHandler)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException(
                "An NPC interaction handler requires a source identity.",
                nameof(source));
        Source = source;
        TargetKind = targetKind;
        _npcHandler = npcHandler;
        _playerHandler = playerHandler;
    }

    public string Source { get; }
    public NpcInteractionTargetKind TargetKind { get; }

    public static NpcInteractionHandler ForNpc(
        string source,
        Func<NpcInteractionTarget, Player, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new NpcInteractionHandler(
            source,
            NpcInteractionTargetKind.Npc,
            handler,
            playerHandler: null);
    }

    public static NpcInteractionHandler ForPlayer(
        string source,
        Func<Player, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new NpcInteractionHandler(
            source,
            NpcInteractionTargetKind.Player,
            npcHandler: null,
            handler);
    }

    public bool TryBegin(NpcInteractionTarget? target, Player player)
    {
        if (TargetKind == NpcInteractionTargetKind.Npc)
            return target is not null && _npcHandler!(target, player);
        return target is null && _playerHandler!(player);
    }
}

internal enum NpcInteractionTargetKind
{
    Npc,
    Player
}

using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Applies the complete registered handler sequence and stops at the first
/// owner, matching the original A-sensitive object-list walk.
/// </summary>
internal sealed class NpcInteractionRouter
{
    private readonly NpcInteractionHandler[] _handlers;
    private readonly string[] _sources;

    public NpcInteractionRouter(
        IReadOnlyList<NpcInteractionHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = new NpcInteractionHandler[handlers.Count];
        _sources = new string[handlers.Count];
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < handlers.Count; index++)
        {
            NpcInteractionHandler handler = handlers[index] ??
                throw new InvalidOperationException(
                    $"NPC interaction handler {index} is null.");
            if (!seenSources.Add(handler.Source))
            {
                throw new InvalidOperationException(
                    $"Duplicate NPC interaction handler source " +
                    $"'{handler.Source}'.");
            }
            _handlers[index] = handler;
            _sources[index] = handler.Source;
        }
    }

    public IReadOnlyList<string> Sources => _sources;

    public bool TryBegin(
        NpcInteractionTarget? target,
        Player player)
    {
        foreach (NpcInteractionHandler handler in _handlers)
        {
            if (handler.TryBegin(target, player))
                return true;
        }
        return false;
    }
}

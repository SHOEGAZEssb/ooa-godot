using System.Collections.Generic;

namespace Oracles;

internal interface INpcInteractionScripts
{
    bool BlocksGameplay { get; }
    IReadOnlyList<NpcInteractionHandler> Handlers { get; }
    void Update(double delta);
}

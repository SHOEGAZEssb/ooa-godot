using System;
using System.Collections.Generic;

namespace oracleofages;

internal static class PreBlackTowerCommandExtensions
{
    public static bool AnyText(this IReadOnlyList<CutsceneCommand> commands, int textId)
    {
        foreach (CutsceneCommand command in commands)
        {
            if (command is CutsceneShowTextCommand text && text.TextId == textId)
                return true;
        }
        return false;
    }
}

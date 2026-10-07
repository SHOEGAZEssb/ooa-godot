using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LibraryKeyholeDatabase
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/library_keyhole_commands.tsv");

    internal LibraryKeyholeDatabase()
    {
        if (Commands.Count != 12)
            throw new InvalidOperationException("miscPuzzles_eyeglassLibraryOpeningScript: incomplete $90:$13 command stream.");
    }
}

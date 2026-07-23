using Godot;
using System;

namespace oracleofages;

internal readonly record struct CutsceneCommandSource(
    string Script,
    string Label,
    int CommandIndex,
    int SourceLine,
    string Opcode)
{
    public override string ToString() =>
        $"{Script}:{Label}[{CommandIndex}] line {SourceLine} ({Opcode})";
}

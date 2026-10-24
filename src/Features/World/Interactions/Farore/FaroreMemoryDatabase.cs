using System.Collections.Generic;

namespace oracleofages;

internal sealed class FaroreMemoryDatabase
{
    internal IReadOnlyList<CutsceneCommand> Script { get; } = CutsceneCommandCatalog.LoadPrograms(
        "res://assets/oracle/cutscenes/farore_memory.tsv")["faroresMemoryScript"];
    internal NpcCharacter CreateActor(DungeonObjectRecord placement)
    {
        var row = GeneratedTable.Load("res://assets/oracle/objects/farore_memory.tsv", new(
            "Farore's Memory graphics $1c:$00", GeneratedTableKeySemantics.Unique,
            ["sprite", "tile-base", "palette", "animation", "source"], ["sprite"], headerRequired: true)).SingleRow();
        string animation = row.RequiredString(3);
        var actor = NpcCharacter.CreateFromRecord(new(placement.Group, placement.Room, 0x1c, 0,
            placement.Y, placement.X, 0x551b, 0, row.RequiredString(0), row.UnsignedDecimal(1), row.Decimal(2, 0, 7),
            0, false, animation, animation, animation, animation, "", NpcImplementationClassification.SpecializedNative));
        actor.Visible = false; actor.SetAnimationRate(0); actor.SetCollisionRadii(0, 0);
        actor.TimeWarpSolidMarked = false;
        return actor;
    }
}

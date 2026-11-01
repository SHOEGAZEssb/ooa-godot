using Godot;
using System;
using System.Linq;

namespace Oracles;

internal static class SeasonsNpcPresentation
{
    private const string Root = SeasonsGameContent.AssetRoot;
    internal static NpcCharacter CreateActor(GeneratedTableRow row, int id, int subid, Vector2 position,
        Func<int,string> animationFor, Color[,] palettes, int group = 0, int room = 0, bool canFace = false,
        NpcCharacter? presenter = null, NpcImplementationClassification implementation = NpcImplementationClassification.EventOwned, int var03 = 0)
    {
        string animation = animationFor(row.UnsignedDecimal(5));
        var record = new NpcRecord(group, room, id, subid, (int)position.Y, (int)position.X, var03, 0,
            row.RequiredString(2), row.UnsignedDecimal(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5), canFace || id == 0x9d,
            canFace || id == 0x9d ? animationFor(0) : animation, canFace || id == 0x9d ? animationFor(1) : animation,
            canFace || id == 0x9d ? animationFor(2) : animation, canFace || id == 0x9d ? animationFor(3) : animation,
            "", implementation);
        var actor = presenter ?? new NpcCharacter();
        actor.Name=$"Seasons_{id:x2}_{subid:x2}";actor.ZIndex=5;
        actor.Initialize(record, Root);
        actor.SetSourceGrayscaleInverted(row.Decimal(9, 0, 1) != 0);
        foreach (string sprite in row.String(6).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = sprite.Split(':');
            actor.AppendScriptGraphics(fields[0], (fields[1] == "1") != (row.Decimal(9, 0, 1) != 0));
        }
        actor.SetGraphicsSourceOffset(row.HexWord(7));
        actor.SetScriptAnimation(animation);
        actor.SetScriptPaletteOverrides(Enumerable.Range(0, 8).ToDictionary(index => index,
            index => Enumerable.Range(0, 4).Select(shade => shade == 0 ? Colors.Transparent : palettes[index, shade]).ToArray()));
        actor.ShowInitializedPresentation();
        return actor;
    }
}

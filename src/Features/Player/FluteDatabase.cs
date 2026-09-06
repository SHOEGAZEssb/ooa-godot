using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FluteDatabase
{
    internal IntroSpriteFrame[] Frames { get; } = new NewGameIntroDatabase().SpriteFrames("link-flute-item");
    private readonly int[] _parameters = new int[7];
    private readonly int[] _sounds = new int[4];
    private readonly HashSet<int> _rooms = new();
    private readonly Dictionary<int, string> _texts = new();
    internal bool Callable(int room) => _rooms.Contains(room);
    internal string Text(int id) => _texts[id];
    internal int Sound(int icon) => (uint)icon < 4 ? _sounds[icon] :
        throw new InvalidOperationException($"harpFluteParent.s has no flute icon ${icon:x2}.");

    internal FluteDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/flute.tsv",
            new GeneratedTableSchema("Flute source data", GeneratedTableKeySemantics.Ordered,
                ["kind", "index", "value", "source"], headerRequired: true)).Rows)
        {
            int index = row.UnsignedDecimal(1);
            switch (row.RequiredString(0))
            {
                case "parameter": _parameters[index] = row.HexByte(2); break;
                case "sound": _sounds[index] = row.HexByte(2); break;
                case "room": _rooms.Add(index); break;
                case "text": _texts.Add(index, row.Base64Utf8(2)); break;
                default: throw row.Invalid(0, "flute source row kind");
            }
        }
        if (Frames.Length != 7 || _rooms.Count == 0 || _texts.Count != 2)
            throw new InvalidOperationException("Flute animation/callable-room/text source data is incomplete.");
    }

    internal int Duration(int icon)
    {
        int elapsed = 0, mask = icon == 0 ? 0x40 : 0x80;
        for (int index = 0; index < Frames.Length; index++)
        {
            if ((_parameters[index] & mask) != 0) return elapsed;
            elapsed += Frames[index].Duration;
        }
        throw new InvalidOperationException("Flute animation lacks its terminal parameter.");
    }

    internal int Parameter(int update)
    {
        int elapsed = update - 1;
        for (int index = 0; index < Frames.Length; index++)
        {
            if (elapsed < Frames[index].Duration) return _parameters[index];
            elapsed -= Frames[index].Duration;
        }
        return _parameters[^1];
    }
}

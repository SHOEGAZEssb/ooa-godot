using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class FluteDatabase
{
    public string AssetRoot { get; }

    internal ObjectSpriteFrame[] Frames { get; }
    internal int SpawnerId { get; } = -1;
    private readonly int[] _parameters = new int[7];
    private readonly int[] _sounds = new int[4];
    private readonly HashSet<int> _rooms = new();
    private readonly Dictionary<int, string> _texts = new();
    internal bool Callable(int room) => _rooms.Contains(room);
    internal string Text(int id) => _texts[id];
    internal int Sound(int icon) => (uint)icon < 4 ? _sounds[icon] :
        throw new InvalidOperationException($"harpFluteParent.s has no flute icon ${icon:x2}.");

    internal FluteDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var frames = GeneratedTable.Load(AssetRoot + "metadata/link_flute_animation.tsv",
            new("native Flute animation",GeneratedTableKeySemantics.Ordered,
                ["index","duration","graphics-index","parameter","source-offset","oam","source"],headerRequired:true));
        if (frames.Rows.Count != 7) throw new InvalidOperationException("LINK_ANIM_MODE_FLUTE requires seven native frames.");
        Frames = frames.Rows.Select((row,index) =>
        {
            if (row.UnsignedDecimal(0)!=index) throw row.Invalid(0,"ordered Flute frame");
            var parts=row.RequiredString(5).Split(';').Select(value =>
            {
                var cells=value.Split(',');
                if (cells.Length!=4 || !int.TryParse(cells[0],out int y) || !int.TryParse(cells[1],out int x) ||
                    !int.TryParse(cells[2],out int tile) || !int.TryParse(cells[3],out int flags)) throw row.Invalid(5,"native y,x,source-tile,flags");
                return new ObjectSpritePart(y,x,tile,flags);
            }).ToArray();
            _parameters[index]=row.HexByte(3);
            return new ObjectSpriteFrame(row.UnsignedDecimal(1),row.HexWord(4),0,parts);
        }).ToArray();

        int parameters=0,sounds=0;
        foreach (var row in GeneratedTable.Load(AssetRoot + "objects/flute.tsv",
            new GeneratedTableSchema("Flute source data", GeneratedTableKeySemantics.Unique,
                ["kind", "index", "value", "source"],["kind","index"],headerRequired: true)).Rows)
        {
            int index = row.UnsignedDecimal(1);
            switch (row.RequiredString(0))
            {
                case "spawner":
                    if (index!=0) throw row.Invalid(1,"single native Flute caller identity");
                    SpawnerId=row.HexByte(2); break;
                case "parameter":
                    if (index>=7 || _parameters[index]!=row.HexByte(2)) throw row.Invalid(2,"matching native animation parameter");
                    parameters++;
                    break;
                case "sound":
                    if (index>=4) throw row.Invalid(1,"Flute icon $00-$03");
                    _sounds[index] = row.HexByte(2); sounds++; break;
                case "room":
                    if (index>255 || row.HexByte(2)!=1) throw row.Invalid(1,"native callable room $00-$ff");
                    _rooms.Add(index); break;
                case "text":
                    if (index is not (0x510c or 0x510f)) throw row.Invalid(1,"TX_510c or TX_510f");
                    _texts.Add(index, row.Base64Utf8(2)); break;
                default: throw row.Invalid(0, "flute source row kind");
            }
        }
        if (SpawnerId<0 || parameters != 7 || sounds != 4 || _rooms.Count == 0 || _texts.Count != 2)
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

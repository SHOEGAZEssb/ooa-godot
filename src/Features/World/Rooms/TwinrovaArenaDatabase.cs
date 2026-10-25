using System;
using System.Collections.Generic;

namespace Oracles;

// ages/roomSpecificTileChanges.s:tileReplacement_group5Mapf5. The request is
// consumed by loadTilesetAndRoomLayout, including requests from Ganon.
internal sealed class TwinrovaArenaDatabase
{
    internal const int ModeAddress = 0xcca9;
    private readonly byte[] _lava = OracleGraphicsData.ReadBytes(AgesGameContent.AssetRoot + "objects/twinrova_lava_layout.bin",176);
    private readonly byte[] _normal = OracleGraphicsData.ReadBytes(AgesGameContent.AssetRoot + "objects/twinrova_normal_layout.bin",176);
    private readonly Dictionary<int,int[]> _rectangles = [];
    internal TwinrovaArenaDatabase()
    {
        foreach(var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/twinrova_arena_rectangles.tsv",new("Twinrova arena rectangles",
            GeneratedTableKeySemantics.Unique,["mode","start","height","width","tile","source"],["mode"],headerRequired:true)).Rows)
            _rectangles.Add(row.HexByte(0),[row.HexByte(1),row.Decimal(2,1,11),row.Decimal(3,1,15),row.HexByte(4)]);
        if(_rectangles.Count!=2 || !_rectangles.ContainsKey(2) || !_rectangles.ContainsKey(4))
            throw new InvalidOperationException("tileReplacement_group5Mapf5 requires ice and seizure rectangles.");
    }
    internal void Apply(int group,OracleRoomData room,OracleRuntimeState? state,Dictionary<int,byte> writes)
    {
        if(group!=5 || room.Id!=0xf5 || state is null) return;
        int mode=state.ReadWramByte(ModeAddress); if(mode==0) return;
        state.SetWramByte(ModeAddress,0);
        if(mode is 1 or 3)
        {
            var layout=mode==1 ? _lava : _normal;
            for(int position=0;position<layout.Length;position++)
                if((position&15)<room.WidthInTiles) writes[position]=layout[position];
                else room.SetStorageLayoutWithoutGraphics(position,layout[position]);
        }
        else
        {
            var rectangle=_rectangles[mode==2 ? 2 : 4];
            for(int y=0;y<rectangle[1];y++)
            for(int x=0;x<rectangle[2];x++)
                writes[rectangle[0]+y*16+x]=(byte)rectangle[3];
        }
    }
}

using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GoronBigBangController(GoronCaveScriptHost host)
{
    private readonly OracleRoomData _room=host.Context.Rooms.CurrentRoom;
    private bool _changedLayout;
    internal RoomEventContext Context=>host.Context;
    internal GoronCaveDatabase Database=>host.Database;
    internal List<GoronBombRoomEntity> Parts {get;}=[];
    internal bool Playing {get;private set;}
    internal void Begin()
    {
        Playing=true;
        if(Context.Entities.PartSlotAvailable) Context.Entities.Spawn<NpcCharacter>(new GoronBombSpawn(this,0xff));
    }
    internal void Clear()
    {
        foreach(var part in Parts) part.Finish();
        Parts.Clear(); Playing=false;
    }
    internal void Layout(string name,bool bottom)
    {
        _changedLayout=true;
        int[] tiles=Database.Bytes("bigbang-"+name);
        for(int i=0;i<24;i++) SetTile((bottom?0x41:0x11)+(i/8)*16+i%8,tiles[i]);
    }
    internal void Exit(int offset)
    {
        _changedLayout=true;
        int[] tiles=[0xb5,0xef,0xef,0xb4,0xb2,0xb2,0xb2,0xb2];
        for(int i=0;i<4;i++) SetTile(0x73+i,tiles[offset+i]);
    }
    private void SetTile(int packed,int tile)=>_room.SetPositionTileAndCollision(
        new((packed&15)*16+8,(packed>>4)*16+8),(byte)tile,null,Context.AnimationTick());
    internal void Cancel()
    {
        Clear();
        if(_changedLayout) { Layout("normalRoomLayout",false); Layout("normalRoomLayout",true); Exit(0); }
        _changedLayout=false;
        Context.Player.SetScriptedLinkAnimationMode(null);
    }
}

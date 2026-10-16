using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_VERAN_ACID_POOL $57 owns logical tile writes, not a private overlay.
internal sealed class VeranAcidPoolRoomEntity(RoomEntityManager owner,VeranFinalDatabase data,Vector2 position,Func<byte,byte,bool> setTile)
    : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,INativePartHealthRoomEntity,
      IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node {get;}=new();
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Packed {get;private set;}
    internal Vector2 Position {get;private set;}=position.Floor();
    public void ClearHealthAndCollision() {} // Native partCode57 never tests health/status.
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch(State)
        {
            case 0:
                Position=new(((int)Position.X&0xf0)+8,((int)Position.Y&0xf0)+8);
                Packed=((int)Position.Y&0xf0)|((int)Position.X>>4);Counter=4;
                owner.OnSoundRequested(SoundId.SndUnknown3);Ring("acid-cardinals",0x60);return;
            case 1:if(Dec()!=0)return;Counter=4;Ring("acid-diagonals",0x60);return;
            case 2:if(Dec()!=0)return;Counter=45;State=3;Center(0x60);return;
            case 3:if(Dec()!=0)return;Counter=4;State=4;goto case 4;
            case 4:Ring("acid-diagonals",0xa0);return;
            case 5:if(Dec()!=0)return;Counter=4;Ring("acid-cardinals",0xa0);return;
            case 6:if(Dec()!=0)return;Center(0xa0);Finished=true;return;
        }
    }
    private int Dec()=>Counter=Counter==0?0:Counter-1;
    private static Vector2 Point(int packed)=>new((packed&15)*16+8,(packed&0xf0)+8);
    private static bool Protected(int tile)=>((tile-2)&255)<3;
    private void Center(int tile)
    {if(!Protected(owner.ActiveRoom.GetMetatile(Point(Packed))))Write(Packed,tile);}
    private void Ring(string offsets,int tile)
    {
        foreach(int offset in data.Bytes(offsets))
        {
            int packed=(Packed+offset)&255;Vector2 point=Point(packed);int original=owner.ActiveRoom.GetMetatile(point);
            if(original==0xda||!Protected(original)&&owner.ActiveRoom.GetTerrainInfo(point).Collision==0)Write(packed,tile);
        }
        State++;
    }
    private void Write(int packed,int tile)
    {if(setTile((byte)packed,(byte)tile))owner.OnRoomTileChanged();}
}

namespace Oracles;

// d1RisingStones.s: the entrance produces subids $00 (dust) and $01 (stone).
internal sealed class SeasonsGnarledStoneRoomEntity : NativeNpcRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    internal SeasonsGnarledStoneRoomEntity(SeasonsOpeningSession session,SeasonsTalkNpcDatabase data,
        NativeObjectInstruction source,OracleRoomData room)
        :base(data.Create(source,room,session.Data.World.SpritePalettes),session.Memory,session.Link,
            index=>data.Animation(0x4b,index),data.Text)
    {
        _session=session; _data=data;
        Npc.SetScriptVisible(false); Npc.SetBlocksLink(false); Npc.SetScriptButtonSensitive(false);
    }
    protected override bool InitializeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    { WriteByte(0x44,ReadByte(0x42)*2+1); return false; }
    protected override bool UpdateNativeNpc(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        switch(ReadByte(0x44))
        {
            case 1:
                WriteByte(0x44,2); _session.Sound.PlaySound(0x6f); Show(0x81); return true;
            case 2:
                if(Npc.CurrentAnimationParameter==0xff)Delete();
                else Npc.AdvanceAnimationUpdates(1);
                return true;
            case 3:
                WriteByte(0x44,4);
                int random=_session.Random.Next().Value;
                var speedsZ=_data.NativeTable("gnarled-stone-speed-z");
                int index=(random&0x60)>>4;
                WriteByte(0x54,speedsZ[index]); WriteByte(0x55,speedsZ[index+1]);
                WriteByte(0x50,_data.NativeTable("gnarled-stone-speeds")[random&3]);
                random=_session.Random.Next().Value;
                WriteByte(0x70,_data.NativeTable("gnarled-stone-gravity")[(random&0x30)>>4]);
                int angle=_data.NativeTable("gnarled-stone-angles")[random&15];
                WriteByte(0x49,angle);
                Show(((angle+1)&7)<3?0x82:0x80); return true;
            case 4:
                Move(ReadByte(0x50),ReadByte(0x49));
                int z=unchecked((short)Npc.NativeState!.Word(0x4e));
                int speed=unchecked((short)Npc.NativeState.Word(0x54));
                bool landed=OracleObjectMath.UpdateSpeedZ(ref z,ref speed,ReadByte(0x70));
                Npc.NativeState.SetWord(0x4e,z); Npc.NativeState.SetWord(0x54,speed);
                Npc.SetScriptDrawOffset(new(0,z>>8));
                if(landed)Delete();
                return true;
            default: throw new NotSupportedException($"d1RisingStones.s: INTERAC$4b state ${ReadByte(0x44):x2}.");
        }
    }
    private void Show(int visible)
    { WriteByte(0x5a,visible); Npc.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(visible)); Npc.SetScriptVisible(true); }
    protected override void AdvanceNpc(RoomEntityFrame frame) { }
}

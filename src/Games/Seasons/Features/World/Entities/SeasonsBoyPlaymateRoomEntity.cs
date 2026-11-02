
namespace Oracles;

// miscBoyNpcs.s -> ballThrownToDog.s -> horonDog.s; each child retains its own slot visit.
internal sealed class SeasonsBoyPlaymateRoomEntity : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, INativeInteractionRoomEntity
{
    private readonly SeasonsOpeningSession _session;
    private readonly SeasonsTalkNpcDatabase _data;
    private readonly OracleRoomData _room;
    private readonly NativeObjectInstruction _source;
    private readonly SeasonsBoyPlaymateRoomEntity? _ball;
    private readonly Vector2 _anchor;
    private bool _initialized;
    private int _state, _counter, _z, _speedZ, _speed, _angle;
    public bool NativeInitializationPending => !_initialized;
    internal SeasonsBoyPlaymateRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        OracleRoomData room, NativeObjectInstruction source, SeasonsBoyPlaymateRoomEntity? ball = null)
        : this(session, data, room, source, ball, data.Create(source, room, session.Data.World.SpritePalettes)) { }
    private SeasonsBoyPlaymateRoomEntity(SeasonsOpeningSession session, SeasonsTalkNpcDatabase data,
        OracleRoomData room, NativeObjectInstruction source, SeasonsBoyPlaymateRoomEntity? ball, NpcCharacter npc)
        : base(npc, npc.SetTransitionDrawOffset)
    { _session=session; _data=data; _room=room; _source=source; _ball=ball; _anchor=npc.Position; }
    public void BindNativeInteractionSlot(int slot)
    {
        int address = ((0xd0 + slot) << 8) | 0x40;
        _session.Memory.SetWramByte(address + 1, (byte)_source.Id);
        _session.Memory.SetWramByte(address + 2, 0);
    }
    private void Animate() { Entity.AdvanceAnimationUpdates(1); Entity.UpdateDrawPriority(_session.Link.Position, withTerrainEffects:true); }
    private bool Decrement() { _counter=(_counter-1)&255; return _counter==0; }
    private void Animation(int index) => Entity.SetScriptAnimation(_data.Animation(_source.Id,index));
    private void Move() => Entity.Position=NativeObjectMovement.ApplySpeed(_session.Memory,
        OracleObjectPosition.FromPixels(Entity.Position),_speed,_angle).PrecisePosition;
    private bool Gravity() => OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20);
    private void Hop() { if(Gravity())_speedZ=-0xc0; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized)
        {
            _initialized=true;Entity.SetScriptVisible(true);
            if(_ball is null)
            {
                _z=unchecked((short)0xfcff);_speed=0x3c;_angle=0x18;
                _session.Entities.TryAllocateInteraction(()=>new SeasonsBoyPlaymateRoomEntity(_session,_data,_room,
                    _source with {Id=0x82,X=(_source.X-12)&255},this));
            }
            else {_z=0;_speedZ=-0xc0;_counter=0x5a;}
        }
        else if(_ball is null) UpdateBall();
        else UpdateDog();
        Entity.SetScriptDrawOffset(new(0,unchecked((sbyte)(_z>>8))));
    }
    private void UpdateBall()
    {
        Entity.UpdateDrawPriority(_session.Link.Position,withTerrainEffects:true);
        switch(_state)
        {
            case 0:
                if(_session.Memory.ReadWramByte(0xcceb)!=1)return;
                int index=(_session.Random.Next().Value&3)*2;var table=_data.NativeTable("ball-speeds");
                _speedZ=unchecked((short)(table[index]|table[index+1]<<8));_state=1;return;
            case 1:
                if(!Gravity()){Move();return;}
                // The source shifts the positive impact word, then negates it.
                _speedZ=unchecked((short)-((ushort)_speedZ>>1));
                if(_speedZ>=-0x60)_state=2;
                return;
            case 2:
                if(_session.Memory.ReadWramByte(0xcceb)!=2)return;
                _state=0;Entity.Position=_anchor;_z=unchecked((short)0xfcff);return;
        }
    }
    private void UpdateDog()
    {
        var ball=_ball!;
        switch(_state)
        {
            case 0:
                Animate();
                if((byte)Entity.Position.X<unchecked((byte)((int)ball.Entity.Position.X+8)))
                {if(_counter==0)Hop();else Decrement();return;}
                _state=1;_counter=0x14;Animation(6);Hop();Animate();return;
            case 1:
                Hop();Animate();if(Decrement()){_angle=0x18;_speed=0x28;_state=2;}return;
            case 2:
                Hop();Animate();
                if((byte)Entity.Position.X!=unchecked((byte)((int)ball.Entity.Position.X+4))){Move();return;}
                _state=3;_counter=0x0c;_z=0;HoldBall();return;
            case 3:
                Animate();
                if(Decrement()){_state=4;_counter=0x1e;_angle=8;Animation(5);}else Move();
                HoldBall();return;
            case 4:
                Animate();HoldBall();if(Decrement())_state=5;return;
            case 5:
                Hop();Animate();HoldBall();Move();
                if((byte)Entity.Position.X!=(byte)_anchor.X)return;
                _session.Memory.SetWramByte(0xcceb,2);_state=0;_z=0;_counter=0x3c;return;
        }
    }
    private void HoldBall() => _ball!.Entity.Position=Entity.Position+new Vector2(_angle==0x18?-5:7,-2);
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized)UpdateFrame(new(_session.Link,_session.FrameCounter,false,null),spawns);
        return ScreenTransitionPresentation.Visible;
    }
}

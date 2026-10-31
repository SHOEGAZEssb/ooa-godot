using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// INTERAC_FLOATING_IMAGE $a0:$01. The interaction slot owns counters and XY.
internal sealed class FloatingMusicNoteRoomEntity : INativeInteractionRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor;
    private readonly OracleRuntimeState _memory;
    private readonly Vector2 _spawnPosition;
    private readonly bool _left;
    private readonly int _duration, _speed, _leftAngle, _rightAngle;
    private readonly int[] _sway;
    private int _address;
    private bool _retired;
    public Node2D Node => _actor;
    public bool Finished => _retired || _address != 0 && Read(0) == 0;
    public bool NativeInitializationPending => !Finished && (_address == 0 || Read(4) == 0);
    public bool UpdatesDuringDialogue => _address != 0 && (Read(0) & 0x80) != 0;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    internal Vector2 PrecisePosition => _address == 0 ? _spawnPosition : new(Word(0x0c)/256f,Word(0x0a)/256f);

    private FloatingMusicNoteRoomEntity(RoomEntityManager owner, Vector2 position, bool left, string name)
    {
        _memory=owner.RuntimeState; _spawnPosition=position; _left=left;
        var row=GeneratedTable.Load(owner.AssetRoot+"effects/music_note.tsv",
            new("INTERAC_FLOATING_IMAGE $a0:$01",GeneratedTableKeySemantics.Ordered,
                ["tile-base","palette","duration","speed-raw","left-angle","right-angle","sway","animation","source"],headerRequired:true)).SingleRow();
        _duration=row.UnsignedDecimal(2); _speed=row.HexByte(3); _leftAngle=row.HexByte(4); _rightAngle=row.HexByte(5);
        _sway=row.RequiredString(6).Split(',').Select(int.Parse).ToArray();
        if (_duration!=70 || _speed!=0x0f || _leftAngle!=0x1d || _rightAngle!=3 ||
            !_sway.SequenceEqual([-1,-2,-1,0,1,2,1,0])) throw row.Invalid(2,"native floating-image movement constants");
        string animation=row.RequiredString(7);
        var record=new NpcRecord(_memory.ReadField("wActiveGroup"),_memory.ReadField("wActiveRoom"),0xa0,1,(int)position.Y,(int)position.X,left?0:1,0,
            "spr_common_sprites",row.UnsignedDecimal(0),row.UnsignedDecimal(1),0,false,
            animation,animation,animation,animation,string.Empty,NpcImplementationClassification.EventOwned);
        _actor=NpcCharacter.CreateFromRecord(record,owner.AssetRoot);
        _actor.Name=name; _actor.SetAnimationRate(0); _actor.SetBlocksLink(false); _actor.SetScriptAnimation(animation);
        _actor.Hide();
    }

    internal static FloatingMusicNoteRoomEntity? Spawn(RoomEntityManager owner,Vector2 position,bool left,string name="FloatingMusicNote")
    {
        if (!owner.InteractionSlotAvailable) return null;
        var note=new FloatingMusicNoteRoomEntity(owner,position,left,name);
        owner.AddEntity(note);
        return note;
    }

    public void BindNativeInteractionSlot(int slot)
    {
        if (_address!=0) throw new InvalidOperationException("INTERAC $a0:$01 already owns its native slot.");
        _address=((0xd0+slot)<<8)|0x40;
        Write(0,1); Write(1,0xa0); Write(2,1); Write(3,_left?0:1);
        // objectCopyPositionWithOffset writes high bytes, retaining slot fractions.
        Write(0x0b,OracleObjectPosition.HighByte(_spawnPosition.Y));
        Write(0x0d,OracleObjectPosition.HighByte(_spawnPosition.X)); Write(0x0f,0);
    }
    public void ReleaseNativeInteractionSlot()
    {
        if (_address!=0 && !_retired && Read(1)==0xa0 && Read(2)==1)
            for (int offset=0;offset<0x40;offset++) Write(offset,0);
        _retired=true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (Read(1)!=0xa0 || Read(2)!=1 || Read(4)>1)
            throw new NotSupportedException($"floatingImage.s: slot${_address:x4} ID${Read(1):x2}:${Read(2):x2} state${Read(4):x2} has no bound music-note presentation.");
        if (Read(4)==0)
        {
            Write(4,1); Write(0,Read(0)|0x80); Write(9,Read(3)==0?_leftAngle:_rightAngle);
            Write(0x10,_speed); Write(6,_duration); Write(0x1a,0x80);
            _actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
            _actor.Show(); _actor.Position=OracleObjectMath.ToPixelPosition(PrecisePosition); return;
        }
        Write(6,unchecked((byte)(Read(6)-1)));
        if (Read(6)==0) { ReleaseNativeInteractionSlot(); _actor.SetActive(false); return; }
        Vector2 position=PrecisePosition;
        NativeObjectMovement.ApplySpeed(_memory,ref position,Read(0x10),Read(9));
        Word(0x0a,(int)(position.Y*256)); Word(0x0c,(int)(position.X*256));
        Write(0x30,Read(0x0d));
        if ((frame.Counter&7)==0) Write(0x0d,Read(0x30)+_sway[(frame.Counter>>3)&7]);
        _actor.Position=OracleObjectMath.ToPixelPosition(PrecisePosition);
    }
    private int Read(int offset)=>_memory.ReadWramByte(_address+offset);
    private void Write(int offset,int value)=>_memory.SetWramByte(_address+offset,unchecked((byte)value));
    private int Word(int offset)=>Read(offset)|(Read(offset+1)<<8);
    private void Word(int offset,int value) {Write(offset,value);Write(offset+1,value>>8);}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
}

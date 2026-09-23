using Godot;
using System;
using System.Linq;

namespace oracleofages;

// ITEM04's normal update and unconditional post-update remain separate.
// The owning controller supplies the live parent slot and dynamic allocator.
internal sealed partial class SomariaWeapon : TransitionOffsetNode2D
{
    private readonly SomariaSwingDatabase _swing;
    private readonly SomariaPlacementDatabase _placement;
    private readonly EnemyAnimationPlayer _visual;
    internal int State { get; private set; }
    internal int ZHigh { get; private set; }
    internal Vector2I Radius { get; private set; }
    internal int Collision { get; }
    internal int BaseDamage { get; }
    internal int Pose => _visual.AnimationIndex;
    internal bool Finished { get; private set; }

    internal SomariaWeapon(SomariaSwingDatabase swing,SomariaPlacementDatabase placement,
        SomariaGraphicsDatabase graphics,Vector2 position,int z)
    {
        _swing=swing; _placement=placement;
        Position=position.Floor(); ZHigh=(sbyte)(byte)z;
        var graphic=graphics.Weapon(0);
        Radius=graphic.InitialRadius; Collision=graphic.InitialCollision; BaseDamage=graphic.Damage;
        _visual=new(this,8);
        // Weapon graphics are loaded at tile$52 in VRAM, the first tile of
        // this imported sheet. OAM offsets therefore use local base zero.
        _visual.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{graphic.Sprite}.png"),
            Enumerable.Range(0,8).Select(i=>graphics.Weapon(i).Animation).ToArray(),0,graphic.OamFlags&7,
            positionedOam:true);
        _visual.SetAnimation(0); Visible=false;
    }

    internal void UpdateItem(bool frozen,int parentParameter,Vector2 link,int direction,int linkZ,
        Action transferKnockback,Action<int> sound,Action markPreviousBlock,Func<Vector2,int,bool> allocateBlock)
    {
        if(Finished || frozen && State!=0) return;
        transferKnockback();
        if(State==0)
        {
            State=1; _visual.SetAnimation(0); sound(OracleSoundEngine.SndSwordSlash); Visible=true;
        }
        else if(State==1 && parentParameter==_placement.CreateParameter)
        {
            State=2; // Allocation failure never retries during this swing.
            markPreviousBlock();
            _=allocateBlock(_placement.CreationPosition(link,direction),(sbyte)(byte)linkZ);
        }
    }

    internal void UpdatePost(int parentId,int parentParameter,Vector2 link,int direction,int linkZ,int raisedFloorOffset)
    {
        if(Finished) return;
        if(parentId!=0x04) { Finished=true; Visible=false; return; }
        var selection=_swing.Select(parentParameter,direction);
        _visual.SetAnimation(selection.Animation);
        SwordArc arc=LinkItemDatabase.Shared.SwordArc(selection.Arc);
        Radius=new(arc.RadiusX,arc.RadiusY);
        Position=new((byte)((int)Mathf.Floor(link.X)+arc.OffsetX),
            (byte)((int)Mathf.Floor(link.Y)+raisedFloorOffset+arc.OffsetY));
        ZHigh=(sbyte)(byte)(linkZ-2);
        QueueRedraw();
    }

    public override void _Draw() => DrawTexture(_visual.CurrentTexture,SourceOamDrawOffset+_visual.CurrentOffset+Vector2.Down*ZHigh);
}

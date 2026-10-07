using Godot;
using System.Linq;

namespace oracleofages;

// ITEM$0c's initialized normal handler is inert. Its unconditional post pass
// resets the pose, publishes the arc and recalculates damage from live rings.
internal sealed partial class BiggoronSwordWeapon : TransitionOffsetNode2D
{
    private readonly BiggoronSwordDatabase _data;
    private readonly EnemyAnimationPlayer _visual;
    internal int State { get; private set; }
    internal int Sector { get; private set; }
    internal int ZHigh { get; private set; }
    internal Vector2I Radius { get; private set; }
    internal int Collision { get; }
    internal int Damage { get; private set; }
    internal bool Finished { get; private set; }
    internal Rect2 Bounds => new(Position-(Vector2)Radius,(Vector2)Radius*2);

    internal BiggoronSwordWeapon(BiggoronSwordDatabase data,Vector2 position,int z)
    {
        _data=data; Position=position.Floor(); ZHigh=(sbyte)(byte)z;
        var graphic=data.Weapon(0); Radius=graphic.Radius; Collision=graphic.Collision;
        Damage=-(sbyte)(byte)graphic.Damage;
        _visual=new(this,8);
        _visual.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{graphic.Sprite}.png"),
            Enumerable.Range(0,8).Select(i=>data.Weapon(i).Animation).ToArray(),0,graphic.OamFlags&7,
            positionedOam:true,animationSourceOffsets:Enumerable.Range(0,8).Select(_=>new[]{graphic.SourceOffset}).ToArray());
        _visual.SetAnimation(0); Visible=false;
    }
    internal void Initialize(System.Action<int> sound)
    {
        if(State!=0 || Finished) return;
        State=1; Visible=true; sound(_data.Weapon(0).Sound);
    }
    internal void UpdatePost(BiggoronSwordParentAnimation? parent,Player player,int raisedFloorOffset,
        System.Action<int> tileProbe)
    {
        if(Finished) return;
        if(parent?.Active!=true) { Finished=true; Visible=false; return; }
        Sector=_data.SelectArc(parent.Parameter,CarriedObjectMotion.DirectionIndex(player.FacingVector));
        if(parent.ConsumeTileProbe()) tileProbe(Sector);
        _visual.SetAnimation(Sector);
        SwordArc arc=_data.Arc(Sector);
        Radius=new(arc.RadiusX,arc.RadiusY);
        Position=new((byte)((int)Mathf.Floor(player.Position.X)+arc.OffsetX),
            (byte)((int)Mathf.Floor(player.Position.Y)+raisedFloorOffset+arc.OffsetY));
        ZHigh=(sbyte)(byte)(player.EnemyContactZ-2);
        Damage=RingEffects.SwordDamageFromBase(player.Inventory,-(sbyte)(byte)_data.Weapon(Sector).Damage);
        QueueRedraw();
    }
    public override void _Draw() => DrawTexture(_visual.CurrentTexture,SourceOamDrawOffset+_visual.CurrentOffset+Vector2.Down*ZHigh);
}

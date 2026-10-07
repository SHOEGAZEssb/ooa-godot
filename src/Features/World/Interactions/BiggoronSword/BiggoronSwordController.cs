using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BiggoronSwordController(Node world,RoomEntityManager entities,CombatController combat,Action<int> sound)
{
    private readonly BiggoronSwordDatabase _data=new();
    private readonly Dictionary<(int Mode,int Frame,int Direction,int Palette),(Texture2D Texture,Vector2 Offset)> _linkGraphics=new();
    internal BiggoronSwordParentAnimation? Parent { get; private set; }
    internal BiggoronSwordWeapon? Weapon { get; private set; }
    internal bool Active => Parent?.Active==true;
    internal void Begin(Player player)
    {
        Cancel();
        Parent=new(_data,player.MinecartRideActive||player.CompanionRideActive,player.RaftRideActive);
        Weapon=new(_data,player.Position,player.EnemyContactZ); world.AddChild(Weapon);
        player.NotifyParentItemAnimationStarted(TreasureId.BiggoronSword);
    }
    internal void UpdateParent(bool prohibited)
    {
        if(prohibited) Parent?.Cancel(); else Parent?.Update();
    }
    internal void UpdateItem() => Weapon?.Initialize(sound);
    internal void UpdatePost(Player player)
    {
        Weapon?.UpdatePost(Parent,player,unchecked((sbyte)entities.RuntimeState.ReadWramByte(WramAddress.wLinkRaisedFloorOffset)),
            sector=>combat.ApplyBiggoronTileHit(player,sector));
        if(Weapon is {Finished:false} weapon)
            entities.ApplySwordHit(weapon.Bounds,player.Position,weapon.Damage,EnemyKnockbackStrength.High,
                collectItemDrops:true,itemZ:weapon.ZHigh,meleeActive:()=>Active && Weapon==weapon,
                itemCollisionType:ItemCollisionType.BiggoronSword,deferAll:true);
        if(Weapon?.Finished==true) { Weapon.Free(); Weapon=null; }
    }
    internal void DrawLink(CanvasItem canvas,Player player,bool damagePalette)
    {
        if(!Active) return;
        int direction=CarriedObjectMotion.DirectionIndex(player.FacingVector),palette=damagePalette?5:0;
        var key=(Parent!.Mode,Parent.Frame,direction,palette);
        if(!_linkGraphics.TryGetValue(key,out var graphic))
        {
            var source=OracleGraphicsCache.LoadTwoBitSpriteSheet("res://assets/oracle/gfx/spr_link.2bpp",0x22e0);
            var record=_data.LinkGraphic(key.Mode,key.Frame,direction);
            graphic=NpcCharacter.BuildPositionedOamTexture(source,record.Oam,0,palette,null,
                sourceGrayscaleInverted:true,sourceOffset:record.ByteOffset);
            _linkGraphics.Add(key,graphic);
        }
        canvas.DrawTexture(graphic.Texture,graphic.Offset+Vector2.Down*player.EnemyContactZ);
    }
    internal void ClearParent() => Parent?.Cancel();
    internal void Cancel()
    {
        Parent?.Cancel(); Parent=null;
        if(Weapon is not null) { Weapon.Free(); Weapon=null; }
    }
}

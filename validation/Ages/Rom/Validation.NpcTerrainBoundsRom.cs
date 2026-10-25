using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNpcTerrainBoundsRom()
    {
        foreach(var room in new[]{(Group:4,Id:0xfc),(Group:3,Id:0xef)})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(room.Group,room.Id);_entities.Clear();
            var data=new BlackTowerPossessionDatabase();var npc=data.Actor(0x4d,7,new(120,200));
            _entities.AddEntity(new EnemyAiSlotReservation(npc));
            npc.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,withTerrainEffects:true);
            var native=new FrontendRom();native[0xcc2d]=(byte)room.Group;native[0xcc30]=native[0xcc2f]=(byte)room.Id;
            native[0xcc2e]=(byte)(_currentRoom.WidthInTiles==15?1:0);native[0xcd00]=1;
            native[0xcfb7]=native[0xcfc7]=0xf8;
            native.LoadRoomTileset();native.Call(0x38dc,0); // Native loadRoomLayout and its exact clear range.
            FailIf(native[0xcfb7]!=0||native[0xcfc7]!=0xf8,"Room load must clear through $cfbf and preserve script byte $cfc7.");
            _runtimeState.SetWramByte(0xcfc7,0xf8);
            // Grass at the final row ensures an off-room read cannot be
            // approximated by clamping onto the nearest playable tile.
            Vector2 last=new(120,_currentRoom.Height-8);
            _currentRoom.SetPositionTileAndCollision(last,0xf8,0,0);
            int packed=_currentRoom.GetPackedPosition(last);native[0xcf00+packed]=0xf8;
            foreach(var spec in new[]{(Position:last,Grass:true),
                (Position:new Vector2(120,_currentRoom.Height+8),Grass:false),
                (Position:new Vector2(120,200),Grass:true),
                (Position:new Vector2(120,248),Grass:false)})
            {
                npc.Position=spec.Position;
                int x=(int)spec.Position.X,y=(int)spec.Position.Y;
                native[0xd24b]=(byte)y;native[0xd24d]=(byte)x;native[0xff8c]=(byte)(y+16);native[0xff8d]=(byte)(x+8);
                native[0xff9f]=0;native[0xcc34]=0;native[0xcc52]=0;
                // _drawObjectTerrainEffects: HL=Object.visible, B=camera,
                // E=ZH. Execute the original lookup and actual OAM emission.
                byte[] caller=[0x21,0x5a,0xd2,0x06,0,0x1e,0,0xcd,0x08,0x0f,0xc9];
                for(int i=0;i<caller.Length;i++)native[0xc100+i]=caller[i];native.Call(0xc100,0);
                var effect=npc.CurrentTerrainEffect;
                FailIf((effect?.Kind==LinkTerrainEffectKind.Grass)!=spec.Grass||native[0xff9f]!=(spec.Grass?8:0),
                    $"NPC terrain ${room.Group:x1}:${room.Id:x2}, Y=${y:x2}: native off-room lookup/OAM differs.");
                byte position=(byte)(((y+5)&0xf0)|(x>>4));
                FailIf(_currentRoom.ReadNativeLayoutByte(position,_runtimeState)!=native[0xcf00+position],
                    $"NPC terrain packed${position:x2} must read native padding or live aliased script memory.");
            }
            npc.Position=new(120,200);_runtimeState.SetWramByte(0xcfc7,0);
            FailIf(npc.CurrentTerrainEffect is not null,"Clearing $cfc7 must immediately remove its aliased terrain effect.");
            npc.Position=new(120,100);npc.SetScriptDrawOffset(new(0,-8));
            FailIf(npc.CurrentTerrainEffect is not null,"Airborne NPCs must skip ground terrain sampling.");
            _entities.Clear();
        }
    }
}

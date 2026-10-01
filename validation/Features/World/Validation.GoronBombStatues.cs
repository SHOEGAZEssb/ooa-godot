using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronBombStatues()
    {
        // mainData.s: group3Map4eObjectData; interactionData.s: $6b:$13/$14;
        // interactionAnimation5a3e4/5a3e9 and interactionOamData5020e/502a7.
        NpcRecord[] records=new NpcDatabase().GetRoomNpcs(3,0x4e).ToArray();
        FailIf(records.Length!=2,"Room 3:4e must import both Goron bomb statues.");
        for(int i=0;i<2;i++)
        {
            NpcRecord record=records[i];
            string oam=i==0?"127@8,0,0,0;8,8,2,0":"127@8,0,2,32;8,8,0,32";
            FailIf(record.Id!=0x6b || record.SubId!=0x13+i || record.Y!=0x10 ||
                record.X!=(i==0?0x38:0x68) || record.TileBase!=0x1c ||
                record.Palette!=4 || record.DefaultAnimation!=8+i || record.CanFace ||
                record.SpriteName!="spr_chickens_dog_forestfairy_other" ||
                record.DownAnimation!=oam ||
                record.Implementation!=NpcImplementationClassification.SpecializedNative,
                $"Goron statue 3:4e $6b:${0x13+i:x2} lost its source placement, graphics or fixed OAM.");
        }

        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(3,0x4e,0xe0);
            OracleRoomData original=_rooms.World.LoadRoom(3,0x4e);
            byte[] originalPixels=original.Texture.GetImage().GetData();
            LoadValidationRoom(3,0x4e);
            NpcCharacter[] actors=_entities.Entities<NpcCharacter>().ToArray();
            FailIf(actors.Length!=2 || _entities.EntityAdapters<GoronBombStatueRoomEntity>().Count()!=2 ||
                !originalPixels.SequenceEqual(_currentRoom.Texture.GetImage().GetData()),
                "Room 3:4e did not create both statues while preserving the rendered background.");
            for(int i=0;i<2;i++)
            {
                NpcCharacter actor=actors[i];
                FailIf(!actor.Active || !actor.Visible || actor.Record.SubId!=0x13+i ||
                    actor.Position!=new Vector2(i==0?0x38:0x68,0x10) ||
                    actor.CurrentAnimationOpaquePixels==0 || actor.ObjectCollisionBounds.Size!=Vector2.Zero ||
                    _currentRoom.GetMetatile(actor.Position)!=0x00 ||
                    _currentRoom.GetTerrainInfo(actor.Position).Collision!=0x0f,
                    $"Goron statue $6b:${0x13+i:x2}: active={actor.Active}, visible={actor.Visible}, position={actor.Position}, texture={actor.CurrentAnimationTextureSize}, opaque={actor.CurrentAnimationOpaquePixels}, radii={actor.ObjectCollisionBounds.Size}, tile=${_currentRoom.GetMetatile(actor.Position):x2}, collision=${_currentRoom.GetTerrainInfo(actor.Position).Collision:x2}.");
            }
            FailIf(_entities.InteractionSlot(actors[1])!=_entities.InteractionSlot(actors[0])+1,
                "Room 3:4e reversed the two statues' source interaction slots.");
            ulong[] hashes=actors.Select(actor=>actor.CurrentAnimationPixelHash).ToArray();
            FailIf(hashes[0]==hashes[1],"Left/right Goron statue poses must use opposite source OAM flips.");
            var application=new ApplicationValidationFixture(this);
            _player.ApplicationUpdateOwned=true;
            application.Step(200,Vector2.Zero,batched:batch);
            for(int i=0;i<2;i++)
            {
                // Start on real floor, then walk into the statue tile. Its
                // actor is at Y $10; solidity covers the entire packed $13/$16.
                Vector2 start=new(i==0?0x38:0x68,0x38);
                FailIf(_currentRoom.IsSolid(start),"Goron statue approach starts inside solid room geometry.");
                for(int repeat=0;repeat<2;repeat++)
                {
                    _player.WarpTo(start);
                    application.Step(60,Vector2.Up,["move_up"],batched:batch);
                    FailIf(_player.Position.Y<0x20 || _player.Position.Y>=start.Y,
                        $"Link crossed Goron statue 3:4e tile ${0x13+i*3:x2} or never approached it through gameplay movement: {_player.Position}.");
                    application.Step(1,Vector2.Zero,["attack"],["attack"]);
                    FailIf(_dialogue.IsOpen,"A-button routing made a silent Goron bomb statue talk.");
                }
            }
            FailIf(actors.Where((actor,i)=>actor.CurrentAnimationFrame!=0 ||
                    actor.CurrentAnimationPixelHash!=hashes[i] || !actor.Visible).Any(),
                "Goron bomb statues advanced animation or disappeared during ordinary gameplay.");

            _entities.BeginScreenTransition(3,_rooms.GetRoom(3,0x4e),new(160,0),_player);
            actors=_entities.Entities<NpcCharacter>().ToArray();
            application.Step(24,Vector2.Zero,batched:batch);
            FailIf(actors.Any(actor=>!actor.Visible || actor.CurrentAnimationFrame!=0 ||
                actor.TransitionDrawOffset!=new Vector2(160,0)),
                "Scrolling preload lost the Goron statues' fixed visible poses.");
            _entities.FinishScreenTransition();
            LoadValidationRoom(3,0x4f);
            FailIf(_entities.EntityAdapters<GoronBombStatueRoomEntity>().Any(),
                "Goron statues survived leaving room 3:4e.");
            LoadValidationRoom(3,0x4e);
            application.Step(2,Vector2.Zero,batched:batch);
            FailIf(_entities.EntityAdapters<GoronBombStatueRoomEntity>().Count()!=2 ||
                _currentRoom.GetTerrainInfo(new Vector2(0x38,0x18)).Collision!=0x0f ||
                _currentRoom.GetTerrainInfo(new Vector2(0x68,0x18)).Collision!=0x0f,
                "Re-entering room 3:4e failed to restore both solid statue tiles.");
        }
    }
}

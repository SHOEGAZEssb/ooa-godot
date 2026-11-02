
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSeasonsRickyInitialization(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;var save=rooms.SaveData;
        if(save.Game!=OracleGame.Seasons)return;
        var memory=rooms.RuntimeState;var entities=root._seasonsOpening!.Entities;
        var player=root._scene.Player;var inventory=player.Inventory;var app=new ApplicationValidationFixture(root);
        int oldGroup=rooms.ActiveGroup,oldRoom=rooms.CurrentRoom.Id,oldAnimal=inventory.AnimalCompanion;
        int oldA=inventory.EquippedA,oldB=inventory.EquippedB;
        byte oldFlags=save.ReadField("wRickyState");
        void Clear()
        {
            entities.Clear();player.ClearNativeItemParents();CompanionRuntimeState.ForgetRemembered(memory);
            for(int at=0xd100;at<0xd140;at++)memory.SetWramByte(at,0);
            memory.SetField("wLinkObjectIndex",0xd0);memory.SetField("wDisabledObjects",0);memory.SetField("wMenuDisabled",0);
            memory.SetField("wDisableScreenTransitions",0);memory.SetField("wWarpsDisabled",0);memory.SetField("wDisableWarpTiles",0);
        }
        try
        {
            Clear();root._seasonsOpening.DebugTravel(0,0x97);Clear();
            var room=rooms.CurrentRoom;
            Vector2 point=(from y in Enumerable.Range(1,room.HeightInTiles-2)
                from x in Enumerable.Range(1,room.WidthInTiles-2)
                let p=new Vector2(x*16+8,y*16+8)
                where Enumerable.Range(-6,31).All(dy=>Enumerable.Range(-6,13).All(dx=>
                    !room.IsSolid(p+new Vector2(dx,dy))&&room.GetTerrainInfo(p+new Vector2(dx,dy)).Hazard==HazardType.None))
                select p).First();
            foreach(var (flags,assigned) in new (int,bool)[]{(0x80,true),(0x80,false),(0,true),(0,false),(0x20,true),(0x40,true),(0x60,false)})
            {
                Clear();save.WriteField("wRickyState",(byte)flags);inventory.AssignAnimalCompanion(assigned?0x0b:0x0d);
                inventory.EquipA(0);inventory.EquipB(0);
                player.WarpTo(point+new Vector2(0,24));player.Face(Vector2I.Up);player.ResetEnemyInvincibility();
                entities.Spawn(new RickyCompanionSpawn(point,2,0,room.Id));
                var native=new BraceletRom(room,player.PrecisePosition,0,0xff,game:OracleGame.Seasons);
                native.LoadSave(save);native.InitializeWalkingAnimation();
                for(int at=0xd100;at<0xd140;at++)native[at]=memory.ReadWramByte(at);
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;
                void Write(int address,byte value){memory.SetWramByte(address,value);native[address]=value;}
                void Step(int count=1)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,0);
                    string context=$"Seasons Ricky/flags${flags:x2}/assigned{assigned}/batch{batched}/update{++updates}";
                    for(int at=0xd100;at<0xd140;at++)
                        FailIf(memory.ReadWramByte(at)!=native[at],context+$": byte${at:x4} runtime${memory.ReadWramByte(at):x2}/native${native[at]:x2}.");
                    FailIf(memory.ReadField("wDisabledObjects")!=native[0xcc8a] || memory.ReadField("wMenuDisabled")!=native[0xcc02],
                        context+": native input masks differ.");
                    FailIf(player.CompanionRideActive!=(native[0xcc2c]==0xd1) ||
                        player.Position!=new Vector2(native[0xd00d],native[0xd00b]),context+": Link handoff differs.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context+": ordered cues differ.");
                });
                Step();
                bool ordinary=(flags&0x80)!=0;
                FailIf(memory.ReadWramByte(0xd104)!=(ordinary?1:0x0a),
                    "Seasons rickyState0 must ignore Ages quest bits $20/$40 and animal assignment.");
                Step(48);
                if(ordinary)continue;
                // Stage the native script's force-mount signal; the glove quest itself is not bound.
                Write(0xd103,1);Step(44);
                FailIf(!player.CompanionRideActive || player.CompanionJumpActive ||
                    entities.Entities<RickyCompanionRoomEntity>().Count!=1,
                    "Seasons Ricky must finish native force mounting in the same D1 slot and shared body.");
            }
        }
        finally
        {
            Clear();save.WriteField("wRickyState",oldFlags);inventory.AssignAnimalCompanion(oldAnimal);
            inventory.EquipA(oldA);inventory.EquipB(oldB);
            root._seasonsOpening.DebugTravel(oldGroup,oldRoom);entities.Clear();
        }
    }
}

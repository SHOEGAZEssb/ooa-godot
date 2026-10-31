using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRickyGloveSpawnerRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool full in new[]{false,true})
        foreach(int state in new[]{0,1,0x21})
        foreach(bool obtained in new[]{false,true})
        for(int repeat=0;repeat<2;repeat++)
        {
            var save=OracleSaveData.CreateStandardGame(OracleGame.Ages);
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            _saveData.WriteField("wRickyState",(byte)state);
            if(obtained)_inventory.GiveTreasure(0x48,0);else _inventory.LoseTreasure(0x48);
            LoadValidationRoom(0,0x98);
            Vector2 link=(from y in Enumerable.Range(1,6) from x in Enumerable.Range(1,8)
                let p=new Vector2(x*16+8,y*16+8)
                where _currentRoom.GetTerrainInfo(p).Type==TerrainType.Normal &&
                    _currentRoom.GetTerrainInfo(p).Hazard==HazardType.None && !_collision.Collides(p) && p.DistanceTo(new(72,40))>24
                select p).First();
            _player.WarpTo(link);
            var parent=_entities.EntityAdapters<RickyGloveSpawnerRoomEntity>().Single();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,(int)link.X,(int)link.Y);
            native[0xd240]=1;native[0xd241]=0x74;native[0xd24b]=0x28;native[0xd24d]=0x48;
            native[0xd340]=1;native[0xd341]=0x71;native[0xd342]=6;
            FailIf(_entities.InteractionSlot(parent.Node)!=2,"$74 must occupy D2 before $71:$06.");
            // Source object placement writes only enabled/id/subid/YH/XH.
            for(int offset=0;offset<64;offset++)FailIf(_runtimeState.ReadWramByte(0xd240+offset)!=native[0xd240+offset],
                $"$74 placement byte${offset:x2} differs.");
            // objectCreateInteraction copies XYZ highs, retaining the child's low bytes.
            _runtimeState.SetWramByte(0xd24f,0xf8);native[0xd24f]=0xf8;
            if(full)
                while(_entities.InteractionSlotAvailable)
                {
                    var puff=new StationaryDebrisEffect();puff.Initialize(AgesGameContent.AssetRoot,new(24,24),SoundId.MusNone);
                    var filler=new InteractionSlotValidationEntity(puff,static _=>{});_entities.AddEntity(filler);
                    int at=0xd040+_entities.InteractionSlot(puff)*256;
                    native[at]=1;native[at+1]=0x48;native[at+2]=0x1b;native[at+4]=1;
                }
            _runtimeState.SetField("wDisabledObjects",2);native[0xcc8a]=2;
            bool eligible=state==1&&!obtained;
            for(int update=0;update<3;update++)
            {
                StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:()=>native.UpdateGameplay(0,0,0xff,_saveData.ReadField("wPlaytimeCounter")));
                string context=$"$74 full{full}/state${state:x2}/got{obtained}/repeat{repeat}/batch{batched}/update{update+1}";
                FailIf(parent.Finished!=(native[0xd240]==0),context+": native parent retirement differs.");
                var child=_entities.Entities<GroundTreasurePickup>().SingleOrDefault();
                int childSlot=Enumerable.Range(2,14).FirstOrDefault(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x60);
                FailIf((child is not null)!=(childSlot!=0),context+": native checked child allocation differs.");
                if(child is not null)
                {
                    int at=0xd040+childSlot*256;
                    FailIf(_entities.InteractionSlot(child)!=childSlot || _runtimeState.ReadWramByte(at+1)!=0x60 ||
                        _runtimeState.ReadWramByte(at+2)!=0x48 || child.Position!=new Vector2(native[at+13],native[at+11]) ||
                        child.ZFixed!=unchecked((short)native.Word(at+14)) ||
                        child.State!=PickupState.Spawning || child.SpawnSubstate!=native[at+5],context+": child identity/XYZ/state or same-pass birth differs.");
                    FailIf(childSlot!=(full?3:4),context+$": parent must retain D2 during child admission; child=D{childSlot:x1}.");
                }
                if(full&&eligible&&update==0)FailIf(parent.Finished||child is not null,context+": failed allocation must retain $74 for retry.");
            }
            FailIf(!parent.Finished || _entities.Entities<GroundTreasurePickup>().Any()!=eligible,
                "$74 must finish its flag/treasure gates or retry successfully after $71:$06 frees D3.");
        }
        GD.Print("Validated native $74 gates, full-pool retry, parent-held first-free child admission, XYZ copy, same-pass $60:$48 dispatch and repeated split/batched gameplay.");
    }
}

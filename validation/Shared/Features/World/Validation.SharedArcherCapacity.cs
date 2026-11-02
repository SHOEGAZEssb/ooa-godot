using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedArcherCapacity(FrontendValidationRoot root, bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player; var game=rooms.SaveData.Game;
        var app=new ApplicationValidationFixture(root); entities.Clear();
        var room=rooms.CurrentRoom;
        for (int y=0; y<room.HeightInTiles; y++)
        for (int x=0; x<room.WidthInTiles; x++)
            room.SetPositionTileAndCollision(new(x*16+8,y*16+8),game==OracleGame.Ages?(byte)0xa0:(byte)0x04,0,0);
        var seed=entities.Random.CaptureState();
        FailIf(!entities.TrySpawnDebugEnemy(0x0c,0,new(80,72),out string error),error);
        var actor=entities.Entities<ArrowMoblinCharacter>().Single();
        var native=SharedNativeEnemyReference(rooms,player,0x0c);
        native.SetResidentObjectGraphics(3,game==OracleGame.Ages?0x8e:0x73);
        native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
        native[0xd080]=1; native[0xd081]=0x0c; native[0xd08b]=72; native[0xd08d]=80;
        rooms.RuntimeState.SetField("wDisabledObjects",8); native[0xcc8a]=8;
        var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_partSlots");
        for (int index=0; index<16; index++)
        {
            var arrow=entities.Spawn<EnemyArrowProjectile>(new EnemyArrowSpawn(new(32,96),8));
            int at=0xd0c0+slots.Single(pair=>ReferenceEquals(pair.Key.PresentationNode,arrow)).Value*256;
            native[at]=1; native[at+1]=0x1a; native[at+9]=8; native[at+11]=96; native[at+13]=32;
        }
        void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
        {
            int target=game==OracleGame.Ages?0xffb0:0xffae;
            native[target]=(byte)player.Position.Y; native[target+1]=(byte)player.Position.X;
            native[target+2]=(byte)player.Position.Y; native[target+3]=(byte)player.Position.X;
            native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts();
            var rng=entities.Random.CaptureState();
            FailIf((int)actor.State!=native[0xd084] || actor.Counter!=native[0xd086] || actor.MoveCycles!=native[0xd0b0] ||
                rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] ||
                entities.Entities<EnemyArrowProjectile>().Count!=Enumerable.Range(0,16).Count(slot=>native[0xd0c0+slot*256]!=0),
                $"{game}: checked archer PART admission changed native route, RNG, or capacity.");
            for (int slot=0; slot<16; slot++)
            {
                int at=0xd0c0+slot*256;
                var entry=slots.FirstOrDefault(pair=>pair.Value==slot).Key;
                FailIf((entry is null)!=(native[at]==0),$"{game}: archer PART slot${0xd0+slot:x2} occupancy differs.");
                if (entry?.PresentationNode is not EnemyArrowProjectile arrow) continue;
                FailIf(arrow.NativeState!=native[at+4] || arrow.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f),
                    $"{game}: admitted arrow must initialize in the native same-update PART slot walk.");
            }
        });
        void Turn(int cycles)
        {
            const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(ArrowMoblinCharacter).GetField("_state",fields)!.SetValue(actor,ArrowMoblinState.Turning);
            typeof(ArrowMoblinCharacter).GetField("_counter",fields)!.SetValue(actor,1);
            typeof(ArrowMoblinCharacter).GetField("_moveCycles",fields)!.SetValue(actor,cycles);
            actor.Position=new(80,72); native.Word(0xd08a,72*256); native.Word(0xd08c,80*256);
            native[0xd084]=9; native[0xd086]=1; native[0xd0b0]=(byte)cycles;
            var prediction=new OracleRandom(); prediction.RestoreState(entities.Random.CaptureState());
            player.WarpTo(actor.Position+OracleObjectMath.StrictCardinalVector(prediction.NextCardinalAngle())*32);
        }
        Step(1); Turn(0); Step(1);
        FailIf(actor.MoveCycles!=1 || entities.Entities<EnemyArrowProjectile>().Count!=16,
            "A full PART pool must drop the eligible arrow without retrying its route.");
        entities.RetireEntity(slots.Single(pair=>pair.Value==5).Key);
        for (int offset=0; offset<64; offset++) native[0xd5c0+offset]=0;
        Step(1);
        FailIf(entities.Entities<EnemyArrowProjectile>().Count!=15,"Capacity release must not replay an old archer shot.");
        Turn(2); Step(1);
        FailIf(native[0xd5c1]!=0x1a || entities.Entities<EnemyArrowProjectile>().Count!=16,
            "A later eligible route must reuse the original first-free PART slot.");
        rooms.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a]=0; Step(4);
        entities.Clear();
    }
}


namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckAgesNativeItemStreamBinding(FrontendValidationRoot root, bool batched)
    {
        var rooms=root._gameplaySession!.Rooms; var entities=root._entities;
        var memory=rooms.RuntimeState; var player=root._scene.Player; var room=rooms.CurrentRoom;
        var app=new ApplicationValidationFixture(root);
        byte load=memory.ReadField("wcc05"), gate=memory.ReadField("wcc85");
        try
        {
            entities.Clear(); EnemyPlacementReservations.BeginRoomParse(memory);
            entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl(); player.WarpTo(new(136,112));
            memory.SetField("wcc05",2); memory.SetField("wcc85",0); memory.SetField("wDisabledObjects",4);
            var seed=entities.Random.CaptureState();
            var native=SharedNativeEnemyReference(rooms,player,0x59);
            native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2; native[0xcc05]=2; native[0xcc85]=0; native[0xcc8a]=4;
            for (int slot=0; slot<12; slot++)
            {
                entities.RegisterEnemySlot(null,slot);
                native[0xd080+slot*256]=1; native[0xd084+slot*256]=8;
            }
            var data=NativeObjectStreamDatabase.For(rooms.AssetRoot);
            int entry=data.Root(4,0xd3);
            foreach (var entity in entities.CreateNativeObjectStream(rooms.ActiveGroup,room,entry,EnemyPlacementContext.Unrestricted))
                entities.AddEntity(entity);
            native.ParseObjectAddress(entry);
            var producers=entities.Entities<ItemDropProducer>().ToArray();
            // mainData/enemyData: four uncounted $fa rows precede a full-pool
            // $f7 Lynel run. Only admitted producers reserve these four tiles.
            int[] positions=[0x16,0x18,0x66,0x68]; int[] subids=[0,9,9,0];
            FailIf(producers.Length!=4 || entities.RoomEnemyCount!=0 || native[0xcdd1]!=0 || memory.ReadWramByte(0xcec1)!=4,
                "Ages4:d3 supplied program must admit four uncounted item producers and skip its full fixed-enemy run.");
            for (int index=0; index<4; index++)
            {
                int address=0xdc80+index*256;
                FailIf(memory.ReadWramByte(0xced0+index)!=positions[index] || native[address+1]!=0x59 || native[address+2]!=subids[index] ||
                    producers[index].Position!=new Vector2((positions[index]&15)*16+8,(positions[index]>>4)*16+8),
                    $"Ages4:d3 item run index${index:x2} lost native ID/subid, reservation or packed position.");
            }
            app.Step(3,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts();
                var rng=entities.Random.CaptureState();
                FailIf(producers.Any(producer=>!producer.Initialized || producer.Finished) ||
                    rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls!=seed.Calls+native.RandomCalls,
                    "Ages4:d3 shared native item projection must preserve common initialization order under its category mask.");
            });
        }
        finally
        {
            memory.SetField("wcc05",load); memory.SetField("wcc85",gate); memory.SetField("wDisabledObjects",0);
            player.EndCutsceneControl(); entities.EnableLinkCollisionsAndMenu();
        }
    }

    private static void CheckSharedDropProducerBinding(FrontendValidationRoot root, bool batched)
    {
        var rooms=root._gameplaySession!.Rooms; var entities=root._seasonsOpening?.Entities ?? root._entities;
        var memory=rooms.RuntimeState; var player=root._scene.Player; var app=new ApplicationValidationFixture(root);
        var game=rooms.SaveData.Game; var room=rooms.CurrentRoom;
        Vector2 point=new(40,40); const int packed=0x22;
        byte floor=game==OracleGame.Ages?(byte)0xa0:(byte)0x04;
        var tiles=TilePropertyDatabase.For(rooms.AssetRoot);
        byte replacement=(byte)Enumerable.Range(0,256).First(tile=>tile!=floor &&
            room.GetCollision((byte)tile)==0 && tiles.NativeHazardFlags(room.ActiveCollisions,(byte)tile)==0);
        byte obtained=rooms.SaveData.ReadWramByte(0xc608);
        try
        {
            rooms.SaveData.WriteWramByte(0xc608,0xff);
            foreach (int subid in new[] {1,0x0a})
            foreach (bool counted in new[] {false,true})
            {
                entities.ClearRecentEnemyDefeats(); entities.LoadRoom(rooms.ActiveGroup,room); entities.Clear();
                EnemyPlacementReservations.BeginRoomParse(memory);
                entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl(); player.WarpTo(new(136,112));
                player.Inventory.EquipA(0); player.Inventory.EquipB(0);
                room.SetPositionTileAndCollision(point,floor,0,0); memory.SetField("wDisabledObjects",0);
                var seed=entities.Random.CaptureState();
                var native=SharedNativeEnemyReference(rooms,player,0x59);
                native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                native[0xcdc0]=(byte)room.Id;
                var source=new RoomObjectRecord(rooms.ActiveGroup,room.Id,0,
                    counted?RoomObjectKind.FixedEnemy:RoomObjectKind.ItemDrop,0x59,subid,0,1,40,40,packed,0xff)
                    {SourceOverride=$"objectLoading.s item producer / {game} / subid${subid:x2} / counted{counted}"};
                foreach (var entity in entities.PlaceRoomObject(source,room,EnemyPlacementContext.Unrestricted)) entities.AddEntity(entity);
                native.ParseObjectStream(game==OracleGame.Ages&&!counted ? [0xfa,0,(byte)subid,packed,0xff] :
                    [0xf7,(byte)(counted?0:2),0x59,(byte)subid,40,40,0xff]);
                var producer=entities.Entities<ItemDropProducer>().Single();
                int updates=0;
                void Compare()
                {
                    var rng=entities.Random.CaptureState();
                    FailIf(producer.Finished!=(native[0xd080]==0) || native[0xd080]!=0 && producer.Initialized!=(native[0xd084]!=0) ||
                        entities.RoomEnemyCount!=native[0xcdd1] || rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] ||
                        rng.Calls!=seed.Calls+native.RandomCalls,
                        $"{source.Source}: update{updates}: source admission, initialization, retirement or RNG differs: " +
                        $"finished={producer.Finished}/{native[0xd080]:x2}, init={producer.Initialized}/{native[0xd084]:x2}, count={entities.RoomEnemyCount}/{native[0xcdd1]}, " +
                        $"RNG={rng.Rng1:x2},{rng.Rng2:x2}/{native[0xff94]:x2},{native[0xff95]:x2}, draws={rng.Calls-seed.Calls}/{native.RandomCalls}.");
                }
                void Step(int count)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++; Compare();
                });
                Compare();
                FailIf(memory.ReadWramByte(0xcec1)!=1 || memory.ReadWramByte(0xced0)!=packed || native[0xd080]!=0x11,
                    $"{source.Source}: the producer must reserve its tile and retain killable index$01.");
                FailIf(entities.RoomEnemyCount!=(counted?1:0),
                    $"{source.Source}: native fixed-enemy flags must select the initial room count.");
                Step(1);
                root._scene.Dialogue.ShowMessage("Item producer pause.",player.Position.Y); native[0xcba0]=1;
                Step(2); root._scene.Dialogue.Close(); native[0xcba0]=0;
                memory.SetField("wDisabledObjects",4); native[0xcc8a]=4;
                room.SetPositionTileAndCollision(point,replacement,0,0); native.SetTile(packed,replacement);
                Step(2);
                FailIf(producer.Finished,"An initialized item producer must hold the changed tile while its enemy category is frozen.");
                memory.SetField("wDisabledObjects",0); native[0xcc8a]=0; Step(3);
                bool emitted=subid==1;
                FailIf(!producer.Finished || producer.SpawnedDrop!=emitted || entities.Entities<ItemDropEffect>().Count!=(emitted?1:0),
                    $"{source.Source}: availability-mask zero must delete without a drop after native initialization.");
                FailIf(entities.RoomEnemyCount!=(counted?1:0),
                    $"{source.Source}: enemyCode59's plain enemyDelete must retain its counted allocation after either deletion branch.");
                FailIf(entities.ActiveRoomDefeatBitset!=(emitted?2:0) || native[0xcdc1]!=entities.ActiveRoomDefeatBitset,
                    $"{source.Source}: only successful child admission may mark placement index$01 consumed.");
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); rooms.SaveData.WriteWramByte(0xc608,obtained);
            memory.SetField("wDisabledObjects",0); player.EndCutsceneControl(); entities.EnableLinkCollisionsAndMenu();
        }
    }
}

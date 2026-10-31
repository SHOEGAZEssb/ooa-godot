using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedPlacement(FrontendValidationRoot root, bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var memory=rooms.RuntimeState; var player=root._scene.Player;
        var app=new ApplicationValidationFixture(root); var game=rooms.SaveData.Game;
        var initialRandom=entities.Random.CaptureState();
        int originalGroup=rooms.ActiveGroup, originalRoom=rooms.CurrentRoom.Id;
        byte originalModifier=memory.ReadField("wRoomStateModifier");
        void Load(int group,int room)
        {
            root._scene.Dialogue.Close(); memory.SetField("wDisabledObjects",0);
            if (game==OracleGame.Ages) root.LoadDebugRoom(group,room);
            else root._seasonsOpening!.DebugTravel(group,room);
            entities.Clear(); entities.DisableLinkCollisionsAndMenu(); player.BeginCutsceneControl();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0); player.WarpTo(new(120,24));
        }
        try
        {
            Load(0,game==OracleGame.Ages?0x98:0xc9);
            var room=rooms.CurrentRoom;
            var reference=SharedNativeEnemyReference(rooms,player,0x0c);
            var tiles=new EnemySpawnTileDatabase(rooms.AssetRoot);
            FailIf(tiles.RecordCount!=(game==OracleGame.Ages?63:52),$"{game}: native placement tile-list extent differs.");
            for (int mode=0; mode<6; mode++)
            for (int tile=0; tile<256; tile++)
            foreach (byte collision in new byte[] {0,1})
                FailIf(tiles.IsValid(mode,new((byte)tile,collision,TerrainType.Normal,HazardType.None))!=
                    reference.TileAllowsEnemyPlacement(mode,(byte)tile,collision),
                    $"{game}: native placement mode${mode:x2}/tile${tile:x2}/collision${collision:x2} differs.");
            CheckSharedObjectConditions(entities,reference);
            CheckNativeObjectStreams(root);

            EnemyPlacementContext[] contexts=[
                EnemyPlacementContext.Scrolling(Vector2I.Up),EnemyPlacementContext.Scrolling(Vector2I.Right),
                EnemyPlacementContext.Scrolling(Vector2I.Down),EnemyPlacementContext.Scrolling(Vector2I.Left),
                EnemyPlacementContext.Warp(0x23),EnemyPlacementContext.Warp(0x45),EnemyPlacementContext.FromWarpDestination(0xf0)];
            (int Terrain,int Flags,int Killed,int Occupied,int Modifier,int Mask)[] scenarios=[
                (0,0,0,0,0,0xff),(0,0,2,0,0,0xff),(1,0,0,0,0,0xff),(1,4,0,0,0,0xff),
                (0,2,0,0,0,0xff),(0,1,2,0,0,0xff),(0,0,0,16,0,0xff),(0,0,0,15,0,0xff),
                (0,0,0,0,1,1),(0,0,0,0,2,4),(0,0,0,0,7,0x80),(0,0,0,0,0,0)];
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(entities,"_enemySlots");
            var reserved=SomariaPrivate<HashSet<int>>(entities,"_reservedEnemySlots");
            int cases=0;
            for (int context=0; context<contexts.Length; context++)
            foreach (var scenario in scenarios)
            {
                entities.Clear(); memory.SetField("wDisabledObjects",4);
                memory.SetField("wRoomStateModifier",(byte)scenario.Modifier);
                byte floor=game==OracleGame.Ages?(byte)0xa0:(byte)0x04;
                for (int y=0; y<room.HeightInTiles; y++)
                for (int x=0; x<room.WidthInTiles; x++)
                    room.SetPositionTileAndCollision(new(x*16+8,y*16+8),floor,scenario.Terrain==0?(byte)0:(byte)0x0f,0);
                var seed=initialRandom with {Rng1=0x37,Rng2=0x0d,Calls=0};
                entities.Random.RestoreState(seed);
                EnemyPlacementReservations.BeginRoomParse(memory);
                memory.SetWramByte(EnemyPlacementMemory.KilledEnemies,(byte)scenario.Killed);
                entities.Random.BeginRoomParse();
                var native=SharedNativeEnemyReference(rooms,player,0x0c);
                native.SetResidentObjectGraphics(3,game==OracleGame.Ages?0x9d:0x84);
                native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                native.GeneratePlacementBuffer(); native[0xcdd1]=0; native[0xcc8a]=4;
                native[0xcc32]=(byte)scenario.Modifier;
                native[0xcd00]=(byte)(context<4?8:0); native[0xcd02]=(byte)(context<4?context:3);
                native[0xcc4a]=(byte)(context==4?0x23:context==5?0x45:0xf0);
                for (int address=0xcec0; address<0xcee0; address++) native[address]=0;
                native[0xcec9]=(byte)scenario.Killed;
                for (int slot=0; slot<scenario.Occupied; slot++)
                {
                    entities.RegisterEnemySlot(null,slot);
                    // Initialized dummy slots remain frozen; no native handler is replaced.
                    native[0xd080+slot*256]=1; native[0xd084+slot*256]=8;
                }
                string source=$"declared native object stream/{game}/context{context}/terrain{scenario.Terrain}/flags${scenario.Flags:x2}/killed${scenario.Killed:x2}/occupied{scenario.Occupied}/modifier${scenario.Modifier:x2}/mask${scenario.Mask:x2}/batch{batched}";
                RoomObjectRecord Row(RoomObjectKind kind,int id,int count,int y,int x,int flags,int mask=0xff) =>
                    new(rooms.ActiveGroup,room.Id,0,kind,id,0,flags,count,y,x,0x22,mask) {SourceOverride=source,Var03=kind==RoomObjectKind.ParameterEnemy?9:0};
                var rows=new[] {
                    Row(RoomObjectKind.FixedEnemy,0x0c,1,40,40,scenario.Flags,scenario.Mask),
                    Row(RoomObjectKind.RandomEnemy,0x32,3,-1,-1,scenario.Flags,scenario.Mask),
                    Row(RoomObjectKind.ParameterEnemy,0x32,1,88,104,0)};
                byte[] bytes=[0xf0,(byte)scenario.Mask,0xf7,(byte)scenario.Flags,0x0c,0,40,40,
                    0xf6,(byte)(0x60|scenario.Flags),0x32,0,
                    0xf0,0xff,0xf9,1,0x32,0,9,88,104,0xff];
                void Place(RoomObjectRecord row)
                {
                    foreach (var entity in entities.PlaceEnemies(row,room,contexts[context])) entities.AddEntity(entity);
                }
                void Compare(bool initialized)
                {
                    FailIf(entities.RoomEnemyCount!=native[0xcdd1],source+$": room count {entities.RoomEnemyCount}/${native[0xcdd1]:x2} differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls!=native.RandomCalls,
                        source+$": placement/dispatch changed native RNG order: initialized={initialized}, " +
                        $"rng={rng.Rng1:x2},{rng.Rng2:x2}/{native[0xff94]:x2},{native[0xff95]:x2}, calls={rng.Calls}/{native.RandomCalls}, mask=${memory.ReadField("wDisabledObjects"):x2}, " +
                        $"states={string.Join(',',slots.Select(pair=>$"{pair.Value}:{((EnemyCharacter)pair.Key.PresentationNode!).InitializationPending}/{native[0xd084+pair.Value*256]:x2}"))}.");
                    for (int slot=0; slot<16; slot++)
                    {
                        int at=0xd080+slot*256;
                        FailIf(reserved.Contains(slot)!=(native[at]!=0),source+$": physical enemy slot${slot:x2} differs.");
                        var pair=slots.FirstOrDefault(pair=>pair.Value==slot);
                        if (pair.Key is null) continue;
                        var actor=(EnemyCharacter)pair.Key.PresentationNode!;
                        int index=(int)pair.Key.GetType().GetProperty("KillableEnemyIndex",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pair.Key)!;
                        FailIf(native[at]!=(1|(index<<4)) || actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f),
                            source+$": enemy slot${slot:x2} kill index or declared/random XY differs.");
                        if (!initialized) continue;
                        int state=actor is ArrowMoblinCharacter moblin?(int)moblin.State:(int)((KeeseCharacter)actor).State;
                        FailIf(state!=native[at+4] || actor.Health!=native[at+0x29] || actor.Visible!=((native[at+0x1a]&128)!=0),
                            source+$": enemy slot${slot:x2} state-zero admission/hold differs.");
                    }
                }
                void Scratch()
                {
                    for (int address=0xcec0; address<0xcee0; address++)
                        FailIf(memory.ReadWramByte(address)!=native[address],source+$": placement scratch ${address:x4} differs (${memory.ReadWramByte(address):x2}/${native[address]:x2}).");
                }
                foreach (var row in rows) Place(row);
                native.ParseObjectStream(bytes); Compare(false); Scratch();
                void Step()=>app.Step(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); Compare(true);
                });
                Step();
                // parseGivenObjectData does not clear the movement/placement
                // alias. Declare an intervening velocity scratch write in both.
                byte[] scratch=[0xff,1,0x12,0];
                for (int index=0; index<4; index++)
                {
                    memory.SetWramByte(0xcec0+index,scratch[index]); native[0xcec0+index]=scratch[index];
                }
                Place(Row(RoomObjectKind.RandomEnemy,0x32,1,-1,-1,6));
                native.ParseObjectStream([0xf6,0x26,0x32,0,0xff]); Compare(false); Scratch();
                Step(); cases++;
            }
            GD.Print($"Compared {game} shared enemy placement in {cases} declared native streams, all1536 tile/mode cells with solid rejection, both dispatch schedules and parseGiven scratch reuse.");
            if (game==OracleGame.Ages)
            {
                CheckAgesRoomObjectConditions(root,batched);
                CheckAgesNativeItemStreamBinding(root,batched);
            }
            else CheckSeasonsAfterEventObjects(root,batched);
            CheckSharedDropProducerBinding(root,batched);
        }
        finally
        {
            root._scene.Dialogue.Close(); memory.SetField("wDisabledObjects",0);
            memory.SetField("wRoomStateModifier",originalModifier);
            player.EndCutsceneControl(); Load(originalGroup,originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu(); entities.Random.RestoreState(initialRandom);
        }
    }
}

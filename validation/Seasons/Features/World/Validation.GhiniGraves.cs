using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSeasonsGhiniGraves(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        if(rooms.SaveData.Game!=OracleGame.Seasons)return;
        var entities=root._seasonsOpening!.Entities;
        var player=root._scene.Player;
        var memory=rooms.RuntimeState;
        var app=new ApplicationValidationFixture(root);
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        byte originalFlags=rooms.SaveData.GetRoomFlags(0,0xd1),maple=memory.ReadField("wIsMaplePresent");
        int a=player.Inventory.EquippedA,b=player.Inventory.EquippedB;
        try
        {
            player.Inventory.EquipA(0);player.Inventory.EquipB(0);
            foreach(var fixture in new[]{(Maple:false,Full:false),(Maple:true,Full:false),(Maple:false,Full:true)})
            for(int repeat=0;repeat<2;repeat++)
            {
                memory.SetField("wIsMaplePresent",0);
                root._seasonsOpening.DebugTravel(0,0xd1);entities.Clear();
                var room=rooms.CurrentRoom;
                Vector2 center=new(56,56),start=new(56.25f,72.5f);
                player.WarpTo(start);player.Face(Vector2I.Up);
                FailIf(room.IsSolid(start) || room.GetMetatile(center)!=0xd5,
                    "Seasons grave0:d1/$33 must use its original southern$c3 floor and$d5 grave.");
                app.Step(1,Vector2.Zero,batched:batched);
                var native=SharedNativeEnemyReference(rooms,player,0x17);
                native[0xd008]=0;native[0xd009]=unchecked((byte)player.NativePlanarAngle);native[0xd010]=unchecked((byte)player.NativePlanarSpeed);
                native[0xcbca]=0;native[0xcdd1]=0;
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                if(fixture.Full)
                {
                    for(int slot=0;slot<16;slot++)
                    {
                        FailIf(!entities.TryCreateEnemy(new GhiniSpawn(new(8,8),1,"OccupiedGraveGhini","ghini.s:full counted pool fixture")),
                            "Grave fixture must fill exactly16 checked enemy slots.");
                        int at=0xd080+slot*256;native[at]=1;native[at+1]=0x17;native[at+2]=1;native[at+0xb]=8;native[at+0xd]=8;
                    }
                    native[0xcdd1]=16;
                }
                memory.SetField("wIsMaplePresent",fixture.Maple?(byte)1:(byte)0);
                native.SetNativeByte(memory.FieldAddress("wIsMaplePresent"),memory.ReadField("wIsMaplePresent"));
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                byte contactFlags=rooms.SaveData.GetRoomFlags(0,0xd1);
                byte[] background=room.CaptureBackgroundMappings();byte collision=room.GetTerrainInfo(center).Collision;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;
                void Step(int count)=>app.Step(count,Vector2.Up,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,0x40,beforeInteractions:()=>native.AdvanceEnemies(entities.FrameCounter));
                    string context=$"Seasons/grave0:d1/Maple{fixture.Maple}/full{fixture.Full}/repeat{repeat}/batch{batched}/update{++updates}";
                    FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                        room.GetMetatile(center)!=native[0xcf33] || room.GetTerrainInfo(center).Collision!=native[0xce33] ||
                        room.GetTerrainInfo(center).Collision!=collision || !room.CaptureBackgroundMappings().SequenceEqual(background) ||
                        rooms.SaveData.GetRoomFlags(0,0xd1)!=contactFlags || entities.RoomEnemyCount!=native[0xcdd1] ||
                        rooms.TilePushCounter!=native.ReadNativeByte(memory.FieldAddress("wPushingAgainstTileCounter")),
                        context+": native approach, logical-only tile write, retained collision/background, flags, counted allocation or clock differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.SequenceEqual(native.Sounds),context+": native cues or RNG differ.");
                    for(int slot=0;slot<16;slot++)
                    {
                        int at=0xd080+slot*256;var ghini=entities.ResolveEnemySlot(slot)?.Node as GhiniCharacter;
                        FailIf((ghini is not null)!=(native[at]!=0),context+": physical enemy admission differs.");
                        if(ghini is null)continue;
                        FailIf((int)ghini.State!=native[at+4] || ghini.Counter!=native[at+6] || ghini.Record.SubId!=native[at+2] ||
                            ghini.Position!=new Vector2(native.Word(at+0xc)/256f,native.Word(at+0xa)/256f) ||
                            (ushort)ghini.ZFixed!=native.Word(at+0xe) || ghini.Visible!=((native[at+0x1a]&128)!=0),
                            context+": counted slow-spawn Ghini phase differs.");
                    }
                });
                if(fixture.Maple)
                {
                    Step(8);FailIf(rooms.TilePushCounter!=20 || room.GetMetatile(center)!=0xd5 || entities.RoomEnemyCount!=0,
                        "Maple must return before touching the grave contact clock or allocating an enemy.");
                    memory.SetField("wIsMaplePresent",0);native.SetNativeByte(memory.FieldAddress("wIsMaplePresent"),0);
                }
                while(room.GetMetatile(center)==0xd5 && updates<64)Step(1);
                FailIf(room.GetMetatile(center)!=0 || entities.RoomEnemyCount!=(fixture.Full?16:1),
                    "Grave expiry must commit the raw layout write before checked, counted enemy admission.");
                Step(16);
                FailIf(entities.RoomEnemyCount!=(fixture.Full?16:1),"Held contact must not respawn a logically cleared grave.");
                root._seasonsOpening.DebugTravel(0,0xd1);
                FailIf(rooms.CurrentRoom.GetMetatile(center)!=0xd5 || rooms.CurrentRoom.GetTerrainInfo(center).Collision!=collision,
                    "Room re-entry must reconstruct the original grave: its raw one-room write has no persistent room flag.");
            }
        }
        finally
        {
            rooms.SaveData.SetRoomFlag(0,0xd1,0xff,false);rooms.SaveData.SetRoomFlag(0,0xd1,originalFlags);
            memory.SetField("wIsMaplePresent",maple);player.Inventory.EquipA(a);player.Inventory.EquipB(b);
            root._seasonsOpening.DebugTravel(originalGroup,originalRoom);
        }
    }
}

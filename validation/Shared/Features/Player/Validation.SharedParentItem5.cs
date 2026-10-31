using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedParentItem5(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var game = memory.Game;
        byte companion = rooms.SaveData.ReadField("wAnimalCompanion");
        var room = rooms.CurrentRoom;
        var app = new ApplicationValidationFixture(root);
        var tiles = (from y in Enumerable.Range(1,6) from x in Enumerable.Range(1,8)
            let point = new Vector2(x*16+8,y*16+8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        (int A,int B)[] pairs = game == OracleGame.Ages ?
            [(1,1),(1,0x0e),(1,5),(5,1),(1,0x11),(0x11,1),(0x0e,1)] :
            [(1,1),(1,0x0e),(1,5),(5,1)];
        try
        {
            foreach (var pair in pairs)
            for (int repeat=0;repeat<2;repeat++)
            {
                player.ClearNativeItemParents(); entities.Clear();
                foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,(byte)(game==OracleGame.Ages ? 0x3a : 4),0,0);
                player.WarpTo(new(88,72)); player.Face(Vector2I.Down);
                foreach (int id in new[] {pair.A,pair.B}.Distinct()) inventory.GiveTreasure(id,id is 0x0e or 0x11 ? 0 : 1);
                inventory.EquipA(pair.A); inventory.EquipB(pair.B);
                app.Step(1,Vector2.Zero,batched:batched);
                var native = new BraceletRom(room,player.PrecisePosition,2,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation(); native[0xcc2d]=(byte)rooms.ActiveGroup;
                var seed = entities.Random.CaptureState(); native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit();
                int previous=0,updates=0;
                void Step(int count=1,int keys=0)
                {
                    int edge=keys & ~previous; previous=keys;
                    app.Step(count,Vector2.Zero,held:MenuRomActions(keys),pressed:MenuRomActions(edge),batched:batched,afterUpdate:()=>
                    {
                        native.UpdateLinkItems(entities.FrameCounter,keys);
                        string context=$"{game} ParentItem5 A${pair.A:x2}/B${pair.B:x2} repeat{repeat} batch={batched} update{++updates}";
                        foreach (int offset in new[] {0,1,3,4,0x3f})
                            FailIf(memory.ReadWramByte(0xd500+offset)!=native[0xd500+offset],
                                context+$": native parent byte${0xd500+offset:x4} differs: runtime${memory.ReadWramByte(0xd500+offset):x2}, native${native[0xd500+offset]:x2}.");
                        bool instrument=native[0xd500]!=0 && native[0xd501] is 0x0e or 0x11 && native[0xd504]!=0;
                        FailIf(player.IsUsingHarp!=instrument || player.IsUsingShield!=(native[game==OracleGame.Ages ? 0xcc6f : 0xcc89]!=0),
                            context+": retained instrument or raised Shield differs.");
                        FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),
                            context+": parent admission changed Link motion.");
                        FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context+": initialization cue order differs.");
                        var now=entities.Random.CaptureState();
                        FailIf(now.Rng1!=native[0xff94] || now.Rng2!=native[0xff95] || now.Calls-seed.Calls!=native.RandomCalls,
                            context+": parent admission consumed different RNG.");
                    });
                }
                Step(1,3); Step(3,3);
                if (!player.IsUsingHarp)
                {
                    Step(35,3); Step(1,2); Step(2,2); Step(); Step(30);
                    FailIf(memory.ReadWramByte(0xd500)!=0,"Released Shield must free ParentItem5 before repeated use.");
                }
                // Global parent cancellation must retire an instrument as well as a Shield.
                player.ClearNativeItemParents(); native.ClearItemParents();
                FailIf(Enumerable.Range(0,64).Any(offset=>memory.ReadWramByte(0xd500+offset)!=native[0xd500+offset]),
                    $"{game}: native ParentItem5 cancellation must clear every byte before repeat.");
            }
        }
        finally
        {
            player.ClearNativeItemParents(); entities.ClearPhysicalPlayerItems();
            rooms.SaveData.WriteField("wAnimalCompanion",companion);
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}

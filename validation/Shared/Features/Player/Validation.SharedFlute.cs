using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedFlute(FrontendValidationRoot root,bool batched)
    {
        var rooms=root._gameplaySession!.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player;
        var inventory=player.Inventory;
        var memory=rooms.RuntimeState;
        var game=memory.Game;
        var instruments=((PlayerWorld)player.World).Instruments;
        int group=rooms.ActiveGroup,roomId=rooms.CurrentRoom.Id;
        byte companion=rooms.SaveData.ReadField("wAnimalCompanion");
        var app=new ApplicationValidationFixture(root);
        void Load(int g,int r)
        {
            if (game==OracleGame.Ages) root.LoadDebugRoom(g,r);
            else root._seasonsOpening!.DebugTravel(g,r);
            entities.Clear();
        }
        try
        {
            // Native indoor/Subrosia rejection lets every Flute complete independently of animal content.
            Load(game==OracleGame.Ages?4:1,game==OracleGame.Ages?0x91:0);
            FailIf((rooms.CurrentRoom.TilesetFlags&0x81)==1,"Flute completion fixture requires an actual native indoor/Subrosia room.");
            var types=TilePropertyDatabase.For(rooms.AssetRoot);
            byte floor=(byte)Enumerable.Range(0,256).First(tile=>rooms.CurrentRoom.GetCollision((byte)tile)==0 &&
                types.Type(rooms.CurrentRoom.ActiveCollisions,(byte)tile)==TerrainType.Normal &&
                types.Hazard(rooms.CurrentRoom.ActiveCollisions,(byte)tile)==HazardType.None);
            for(int y=1;y<rooms.CurrentRoom.HeightInTiles-1;y++)
            for(int x=1;x<rooms.CurrentRoom.WidthInTiles-1;x++)
                rooms.CurrentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8),floor,0,0);
            foreach(bool primary in new[]{true,false})
            foreach(int icon in Enumerable.Range(0,4))
            for(int repeat=0;repeat<2;repeat++)
            {
                player.ClearNativeItemParents(); entities.Clear();
                inventory.GiveTreasure(TreasureId.Flute,icon==0?0:icon+0x0a);
                rooms.SaveData.WriteField("wFluteIcon",(byte)icon);
                inventory.EquipA(primary?TreasureId.Flute:0); inventory.EquipB(primary?0:TreasureId.Flute);
                player.WarpTo(new(88.25f,72.5f)); player.Face(Vector2I.Down);
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation(); native[0xcc2d]=(byte)rooms.ActiveGroup;
                var seed=entities.Random.CaptureState(); native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                var sound=root._sound.AttachPlayRequestAudit();
                int previous=0,updates=0;
                void Step(int count=1,int keys=0)
                {
                    int edge=keys & ~previous; previous=keys;
                    app.Step(count,Vector2.Zero,held:MenuRomActions(keys),pressed:MenuRomActions(edge),batched:batched,afterUpdate:()=>
                    {
                        native.UpdateLinkItems(entities.FrameCounter,keys,interactions:true);
                        string context=$"{game} Flute icon{icon} A={primary} repeat{repeat} batch={batched} update{++updates}";
                        FailIf(player.IsUsingHarp!=(native[0xd500]!=0) || instruments.IsPlaying!=player.IsUsingHarp ||
                            instruments.PlayingInstrument!=native[game==OracleGame.Ages?0xcc8d:0xcca7] ||
                            player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),
                            context+": native playback/publication/Link movement differs.");
                        foreach(int offset in new[]{0,1,3,4,0x3f})
                            FailIf(memory.ReadWramByte(0xd500+offset)!=native[0xd500+offset],context+$": parent byte${0xd500+offset:x4} differs.");
                        var notes=entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Where(note=>!note.Finished).ToArray();
                        int[] addresses=Enumerable.Range(2,14).Select(slot=>((0xd0+slot)<<8)|0x40)
                            .Where(at=>native[at]!=0 && native[at+1]==0xa0).ToArray();
                        FailIf(notes.Length!=addresses.Length,context+": native note allocation count differs.");
                        foreach(var note in notes)
                        {
                            int at=SomariaPrivate<int>(note,"_address");
                            FailIf(!addresses.Contains(at) || note.PrecisePosition!=new Vector2(native.Word(at+0x0c)/256f,native.Word(at+0x0a)/256f),
                                context+": ordered note slot/position differs.");
                            foreach(int offset in new[]{0,1,2,3,4,6,9,0x10,0x1a,0x30})
                                FailIf(memory.ReadWramByte(at+offset)!=native[at+offset],
                                    context+$": note byte${at+offset:x4} runtime${memory.ReadWramByte(at+offset):x2}/native${native[at+offset]:x2} differs.");
                        }
                        FailIf(!sound.Requests.Where(id=>id!=SoundId.SndText).SequenceEqual(native.Sounds),context+": cue order differs.");
                        var now=entities.Random.CaptureState();
                        FailIf(now.Rng1!=native[0xff94] || now.Rng2!=native[0xff95] || now.Calls-seed.Calls!=native.RandomCalls,
                            context+": shared RNG differs.");
                    });
                }
                int button=primary?1:2;
                Step(32,button);
                root._scene.Dialogue.ShowMessage("Flute pause.",player.Position.Y); native[0xcba0]=1;
                Step(5,button);
                root._scene.Dialogue.Close(); native[0xcba0]=0;
                Step((icon==0?95:255)-33,button);
                FailIf(!player.IsUsingHarp || native[0xd500]==0,
                    $"{game}: Flute icon${icon:x2} must remain active until its own source completion update.");
                Step(1,button);
                FailIf(player.IsUsingHarp || root.DialogueOpen || root.InventoryMenuOpen || root.MapMenuOpen,
                    "Indoor/Subrosia Flute must complete at its source boundary without an animal call or text.");
                Step(1,button); Step(75);
                FailIf(entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Any(),"Native floating notes must expire before repeated Flute use.");
            }
            Load(0,game==OracleGame.Ages?0x06:0xb6);
            FailIf((rooms.CurrentRoom.TilesetFlags&0x81)!=1,"Flute echo text requires actual present/Holodrum outdoors.");
            floor=(byte)Enumerable.Range(0,256).First(tile=>rooms.CurrentRoom.GetCollision((byte)tile)==0 &&
                types.Type(rooms.CurrentRoom.ActiveCollisions,(byte)tile)==TerrainType.Normal &&
                types.Hazard(rooms.CurrentRoom.ActiveCollisions,(byte)tile)==HazardType.None);
            for(int y=1;y<7;y++)
            for(int x=1;x<9;x++) rooms.CurrentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8),floor,0,0);
            for(int repeat=0;repeat<2;repeat++)
            {
                inventory.GiveTreasure(TreasureId.Flute,0); inventory.EquipA(TreasureId.Flute); inventory.EquipB(0);
                rooms.SaveData.WriteField("wFluteIcon",0);
                player.WarpTo(new(88,72)); player.Face(Vector2I.Down);
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                var seed=entities.Random.CaptureState(); native[0xff94]=seed.Rng1; native[0xff95]=seed.Rng2;
                app.Step(95,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched,
                    afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,1,interactions:true));
                FailIf(player.IsUsingHarp || !root.DialogueOpen || native[0xcba0]==0 || native.Word(0xcba2)!=0x550f ||
                    root._scene.Dialogue.CurrentMessage!="Your song just\nechoes...",
                    $"{game}: completed mysterious Flute must request native TX_510f through its real dialogue owner.");
                root._scene.Dialogue.Close(); native[0xcba0]=0;
                app.Step(75,Vector2.Zero,batched:batched,
                    afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,0,interactions:true));
                FailIf(instruments.PlayingInstrument!=0 || entities.EntityAdapters<FloatingMusicNoteRoomEntity>().Any(),
                    $"{game}: Flute echo completion must release publication and expire notes before repeat.");
            }
            CheckSharedFluteCallAllocation(root,batched);
        }
        finally
        {
            player.ClearNativeItemParents(); entities.Clear();
            rooms.SaveData.WriteField("wAnimalCompanion",companion);
            Load(group,roomId);
        }
    }
}

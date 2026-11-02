
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSeasonsOverworldKeyholes(FrontendValidationRoot root,bool batched)
    {
        var engine=root._gameplaySession!;
        var rooms=engine.Rooms;
        if(rooms.SaveData.Game!=OracleGame.Seasons)return;
        var entities=root._seasonsOpening!.Entities;
        var player=root._scene.Player;
        var inventory=player.Inventory;
        var save=rooms.SaveData;
        var memory=rooms.RuntimeState;
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int a=inventory.EquippedA,b=inventory.EquippedB;
        bool[] originalKeys=Enumerable.Range(0x42,3).Select(inventory.HasTreasure).ToArray();
        var fixtures=new[]{(Room:0x96,Position:0x26,Treasure:0x42),(Room:0x81,Position:0x12,Treasure:0x43),(Room:0x0d,Position:0x13,Treasure:0x44)};
        byte[] flags=fixtures.Select(f=>save.GetRoomFlags(0,f.Room)).ToArray();
        var app=new ApplicationValidationFixture(root);
        try
        {
            inventory.EquipA(0);inventory.EquipB(0);
            foreach(var fixture in fixtures)
            foreach(bool owned in new[]{false,true})
            {
                for(int key=0x42;key<=0x44;key++)inventory.LoseTreasure(key);
                inventory.GiveTreasure(owned?fixture.Treasure:0x42+(fixture.Treasure-0x42+1)%3,1);
                save.SetRoomFlag(0,fixture.Room,0x80,false);
                root._seasonsOpening.DebugTravel(0,fixture.Room);entities.Clear();
                var room=rooms.CurrentRoom;
                Vector2 center=new((fixture.Position&15)*16+8,(fixture.Position>>4)*16+8);
                Vector2 start=center+new Vector2(0.25f,fixture.Room==0x0d?16.5f:32.5f);
                player.WarpTo(start);player.Face(Vector2I.Up);
                FailIf(room.IsSolid(start) || room.GetMetatile(center)!=0xec,
                    $"Seasons: named keyhole0:{fixture.Room:x2}/${fixture.Position:x2} must retain its native southern approach.");
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(room,player.PrecisePosition,0,inventory.ActiveRing,game:OracleGame.Seasons);
                native.LoadSave(save);native.InitializeWalkingAnimation();
                native[0xcc2d]=0;native[0xcc2e]=0;
                native[0xd009]=unchecked((byte)player.NativePlanarAngle);native[0xd010]=unchecked((byte)player.NativePlanarSpeed);
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wInformativeTextsShown","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;
                void Step(int count)=>app.Step(count,Vector2.Up,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,0x40,interactions:true);
                    string context=$"Seasons/keyhole0:{fixture.Room:x2}/owned{owned}/batch{batched}/update{++updates}";
                    FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                        room.GetMetatile(center)!=native[0xcf00+fixture.Position] || save.GetRoomFlags(0,fixture.Room)!=native[0xc700+fixture.Room],
                        context+": native approach, layout or room flags differ.");
                    foreach(string field in new[]{"wPushingAgainstTileCounter","wInformativeTextsShown","wDisabledObjects","wDisableLinkCollisionsAndMenu"})
                        FailIf(memory.ReadField(field)!=native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.Where(cue=>cue!=SoundId.SndText).SequenceEqual(native.Sounds),context+": native cues/RNG differ.");
                });
                while(!root._scene.Dialogue.IsOpen && (!owned || rooms.TilePushCounter!=2) && updates<64)Step(1);
                if(!owned)
                {
                    FailIf(!root._scene.Dialogue.IsOpen || root._scene.Dialogue.CurrentMessage!="Huh? This has a\nkeyhole." || rooms.InformativeTextsShown!=0x20,
                        "Seasons: an unrelated named key must retain native TX_5109 and shared hint suppression.");
                    root._scene.Dialogue.Close();native[0xcba0]=0;Step(24);
                    FailIf(root._scene.Dialogue.IsOpen,"Seasons: repeated wrong-key contact must retain the suppressed hint.");
                }
                else
                {
                    FailIf(rooms.TilePushCounter!=2,"Seasons: owned-key contact must reach its native doubled-counter event boundary.");
                    byte roomFlags=save.GetRoomFlags(0,fixture.Room),signal=memory.ReadField("wTmpcfc0");
                    int cues=sounds.Requests.Count,random=entities.Random.Calls;
                    bool rejected=false;
                    try { engine.Keyholes.UpdatePushAttempt(player.PrecisePosition,Vector2I.Up,Vector2.Up); }
                    catch(NotSupportedException error) when(error.Message.Contains("interactableTiles.s:@group0/ROOM_SEASONS_") && error.Message.Contains("entrance event")) {rejected=true;}
                    FailIf(!rejected || save.GetRoomFlags(0,fixture.Room)!=roomFlags || memory.ReadField("wTmpcfc0")!=signal ||
                        memory.ReadField("wDisabledObjects")!=0 || memory.ReadField("wDisableLinkCollisionsAndMenu")!=0 ||
                        !inventory.HasTreasure(fixture.Treasure) || sounds.Requests.Count!=cues || entities.Random.Calls!=random ||
                        entities.Entities<OverworldKeyUseEffect>().Count!=0,
                        "Seasons: missing entrance content must reject before flag/signal/lock/sprite/cue/RNG changes and retain the key.");
                }
            }
        }
        finally
        {
            root._scene.Dialogue.Close();
            for(int index=0;index<fixtures.Length;index++)
            {
                save.SetRoomFlag(0,fixtures[index].Room,0xff,false);save.SetRoomFlag(0,fixtures[index].Room,flags[index]);
                inventory.LoseTreasure(0x42+index);if(originalKeys[index])inventory.GiveTreasure(0x42+index,1);
            }
            inventory.EquipA(a);inventory.EquipB(b);
            root._seasonsOpening.DebugTravel(originalGroup,originalRoom);
        }
    }
}

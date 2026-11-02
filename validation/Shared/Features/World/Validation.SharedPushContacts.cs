
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedPushContacts(FrontendValidationRoot root,bool batched)
    {
        var engine=root._gameplaySession!;
        var rooms=engine.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player;
        var memory=rooms.RuntimeState;
        var inventory=player.Inventory;
        var game=memory.Game;
        bool ages=game==OracleGame.Ages;
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int a=inventory.EquippedA,b=inventory.EquippedB;
        var app=new ApplicationValidationFixture(root);
        void Load(int group,int room)
        {
            if(ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);
        }
        try
        {
            inventory.GiveTreasure(TreasureId.Bracelet,2);
            inventory.EquipA(0);inventory.EquipB(0);
            foreach(bool ice in ages?new[]{false}:new[]{false,true})
            {
                int group=ages?4:ice?5:0,roomId=ages?0x09:ice?0x86:0x52;
                Load(group,roomId);entities.Clear();
                var room=rooms.CurrentRoom;
                var blocks=entities.RequiredReservedPushBlock;
                Vector2 start=ages?new(184.25f,56.5f):ice?new(72.25f,56.5f):new(56.25f,104.5f);
                Vector2 movement=ages||ice?Vector2.Right:Vector2.Up;
                int direction=ages||ice?1:0;
                byte source=ages?(byte)0x3c:ice?(byte)0x36:(byte)0x53;
                byte tile=ages?(byte)0x19:ice?(byte)0x2f:(byte)0xd6;
                FailIf(room.IsSolid(start) || room.ReadNativeLayoutByte(source,memory)!=tile,
                    $"{game}: push fixture must retain native floor and source tile${tile:x2} in room${group:x1}:${roomId:x2}.");
                player.WarpTo(start);player.Face(direction==0?Vector2I.Up:Vector2I.Right);
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(room,player.PrecisePosition,direction,inventory.ActiveRing,2,game);
                native.LoadSave(rooms.SaveData);native.InitializeWalkingAnimation();
                // Ice convergence retains angle independently of facing.
                native[0xd009]=unchecked((byte)player.NativePlanarAngle);
                native[0xd010]=unchecked((byte)player.NativePlanarSpeed);
                native[0xcc2d]=(byte)group;native[0xcc2e]=(byte)(group>=4?1:0);
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wDisabledObjects","wBlockPushAngle","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                var seed=entities.Random.CaptureState();
                native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();
                int updates=0,slidingVisits=0;
                void Step(int count,Vector2 input=default)=>app.Step(count,input,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,input==Vector2.Up?0x40:input==Vector2.Right?0x10:0,interactions:true);
                    string context=$"{game}/push${tile:x2}/batch{batched}/update{++updates}";
                    bool active=native[0xd140]!=0 && native[0xd141]==0x14;
                    FailIf(blocks.Active!=active || player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) || player.NativePlanarSpeed!=native[0xd010],
                        context+$": native allocation/Link XY differ: {blocks.Active}/{active}, {player.PrecisePosition}/{native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}, angle${player.NativePlanarAngle:x2}/${native[0xd009]:x2}, speed${player.NativePlanarSpeed:x2}/${native[0xd010]:x2}.");
                    if(blocks.NativeInitialized)
                    {
                        Vector2 center=blocks.BlockTopLeft+new Vector2(8,6);
                        FailIf(center!=new Vector2(native.Word(0xd14c)/256f,native.Word(0xd14a)/256f),
                            context+$": native block XY differ: {center}/{native.Word(0xd14c)/256f},{native.Word(0xd14a)/256f}.");
                        if(native[0xd144]==2 && native[0xd150]==0x46)slidingVisits++;
                    }
                    foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wDisabledObjects","wBlockPushAngle"})
                        FailIf(memory.ReadField(field)!=native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs.");
                    for(int y=0;y<room.HeightInTiles;y++)
                    for(int x=0;x<room.WidthInTiles;x++)
                        FailIf(room.GetMetatile(new(x*16+8,y*16+8))!=native[0xcf00+y*16+x],context+": native layout write differs.");
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.Where(cue=>cue!=SoundId.SndText).SequenceEqual(native.Sounds),context+": source cues/RNG differ.");
                });
                while(!blocks.NativeInitialized && updates<80)Step(1,movement);
                FailIf(!blocks.NativeInitialized,$"{game}: native geometry never admitted the push.");
                FailIf(engine.CanCaptureDebugState,$"{game}: an active reserved push must not advertise unsupported transient reconstruction.");
                bool rejected=false;
                try { engine.CaptureDebugState(0); }
                catch(NotSupportedException) { rejected=true; }
                FailIf(!rejected,$"{game}: direct debug capture must reject the same active tile phase.");
                Step(4,movement);
                memory.SetField("wDisabledObjects",2);native.SetNativeByte(memory.FieldAddress("wDisabledObjects"),2);
                Vector2 paused=blocks.BlockTopLeft;Step(3);
                FailIf(blocks.BlockTopLeft!=paused,$"{game}: initialized push must freeze under the native interaction mask.");
                memory.SetField("wDisabledObjects",0);native.SetNativeByte(memory.FieldAddress("wDisabledObjects"),0);
                Step(96,movement);Step(3);Step(24,movement);
                FailIf(blocks.Active || sounds.Requests.Count(cue=>cue==SoundId.SndMoveBlock)!=1 ||
                    sounds.Requests.Count(cue=>cue==SoundId.SndClink)!=(ice?1:0) || ice && slidingVisits==0,
                    $"{game}: push completion/repeat must preserve native movement state, clink and one allocation.");
            }
            foreach(var reserved in new[]{(Slot:0xd140,Id:0x14),(Slot:0xd040,Id:0x1e),(Slot:0xd240,Id:0x17)})
            {
                var unsupported=new OracleRuntimeState(game);
                unsupported.RestoreState(memory.CaptureState());
                unsupported.SetWramByte(reserved.Slot,1);unsupported.SetWramByte(reserved.Slot+1,(byte)reserved.Id);
                bool rejected=false;
                try { entities.ValidateDebugReconstruction(unsupported.CaptureState(),rooms.ActiveGroup,rooms.CurrentRoom.Id); }
                catch(NotSupportedException) { rejected=true; }
                FailIf(!rejected,$"{game}: raw transient INTERAC${reserved.Id:x2} must reject restore before replacing the live scene.");
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects",0);
            inventory.EquipA(a);inventory.EquipB(b);
            Load(originalGroup,originalRoom);
        }
    }
}

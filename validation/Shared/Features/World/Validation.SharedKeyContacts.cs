using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedKeyContacts(FrontendValidationRoot root,bool batched)
    {
        var engine=root._gameplaySession!;
        var rooms=engine.Rooms;
        var entities=root._seasonsOpening?.Entities ?? root._entities;
        var player=root._scene.Player;
        var memory=rooms.RuntimeState;
        var save=rooms.SaveData;
        var inventory=player.Inventory;
        bool ages=save.Game==OracleGame.Ages;
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int group=ages?4:5,roomId=ages?0xb3:0x86;
        byte[] flags=Enumerable.Range(0,256).Select(id=>save.GetRoomFlags(group,id)).ToArray();
        var app=new ApplicationValidationFixture(root);
        void Load(int g,int id) { if(ages)root.LoadDebugRoom(g,id);else root._seasonsOpening!.DebugTravel(g,id); }
        Load(group,roomId);
        int dungeon=rooms.CurrentDungeonIndex,originalKeys=inventory.GetDungeonSmallKeys(dungeon);
        void GiveKey()=>inventory.GiveTreasure(entities.Treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
        try
        {
            foreach(bool hasKey in new[]{false,true})
            {
                foreach(int id in Enumerable.Range(0,256))save.SetRoomFlag(group,id,0xff,false);
                Load(group,roomId);entities.Clear();
                while(inventory.TryUseDungeonSmallKey(dungeon)) { }
                if(hasKey)GiveKey();
                FailIf(inventory.GetDungeonSmallKeys(dungeon)!=(hasKey?1:0),
                    $"{save.Game}: native small-key collection must target the live dungeon${dungeon:x2}.");
                var keys=entities.ReservedKeyDoor!;
                var room=rooms.CurrentRoom;
                Vector2 start=ages?new(204.25f,136.5f):new(40.25f,72.5f);
                Vector2 center=ages?new(232,136):new(8,72),movement=ages?Vector2.Right:Vector2.Left;
                int direction=ages?1:3,packed=ages?0x8e:0x40;
                player.WarpTo(start);player.Face(ages?Vector2I.Right:Vector2I.Left);
                FailIf(room.IsSolid(start) || room.GetMetatile(center)!=(ages?0x71:0x73),$"{save.Game}: key contact must approach the native door through floor.");
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(room,player.PrecisePosition,direction,inventory.ActiveRing,game:save.Game);
                native.LoadSave(save);native.InitializeWalkingAnimation();
                native[0xd009]=unchecked((byte)player.NativePlanarAngle);
                native[0xd010]=unchecked((byte)player.NativePlanarSpeed);
                native[0xcc2d]=(byte)group;native[0xcc2e]=1;native.LoadDungeon(dungeon);
                Vector2 camera=-entities.WorldToScreen(Vector2.Zero);
                native.SetNativeByte(ages?0xffaa:0xffa8,unchecked((byte)(int)camera.Y));
                native.SetNativeByte(ages?0xffac:0xffaa,unchecked((byte)(int)camera.X));
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wDisabledObjects","wInformativeTextsShown","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;
                void Step(int count,Vector2 input=default)=>app.Step(count,input,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,input==Vector2.Right?0x10:input==Vector2.Left?0x20:0,interactions:true);
                    string context=$"{save.Game}/key-door/key{hasKey}/batch{batched}/update{++updates}";
                    FailIf(keys.Opening!=(native[0xd040]!=0 && native[0xd041]==0x1e) || keys.OpeningCounter!=native[0xd046] ||
                        room.GetMetatile(center)!=native[0xcf00+packed] || room.GetTerrainInfo(center).Collision!=native[0xce00+packed] ||
                        inventory.GetDungeonSmallKeys(dungeon)!=native.ReadNativeByte(memory.FieldAddress("wDungeonSmallKeys",dungeon)) ||
                        player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),
                        context+$": opener{keys.Opening}/{native[0xd040]!=0}, counter{keys.OpeningCounter}/{native[0xd046]}, tile${room.GetMetatile(center):x2}/${native[0xcf00+packed]:x2}, collision${room.GetTerrainInfo(center).Collision:x2}/${native[0xce00+packed]:x2}, keys{inventory.GetDungeonSmallKeys(dungeon)}/{native.ReadNativeByte(memory.FieldAddress("wDungeonSmallKeys",dungeon))}.");
                    foreach(string field in new[]{"wPushingAgainstTileCounter","wInformativeTextsShown"})
                        FailIf(memory.ReadField(field)!=native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs: ${memory.ReadField(field):x2}/${native.ReadNativeByte(memory.FieldAddress(field)):x2}.");
                    for(int id=0;id<256;id++)
                        FailIf(save.GetRoomFlags(group,id)!=native[(group==4?0xc900:0xca00)+id],context+": both dungeon-layout door flags must follow native adjacency.");
                    CompareSharedKeySprites(entities,native,context);
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.Where(cue=>cue!=SoundId.SndText).SequenceEqual(native.Sounds),
                        context+$": native cue/RNG order differs: [{string.Join(',',sounds.Requests)}]/[{string.Join(',',native.Sounds)}], RNG{rng.Calls-seed.Calls}/{native.RandomCalls}.");
                });
                while(!keys.Opening && !root._scene.Dialogue.IsOpen && updates<96)Step(1,movement);
                if(!hasKey)
                {
                    FailIf(!root._scene.Dialogue.IsOpen || !root._scene.Dialogue.CurrentMessage.Contains("key") || keys.Opening || rooms.InformativeTextsShown!=4,
                        $"{save.Game}: no-key contact must show the selected native hint without opening.");
                    root._scene.Dialogue.Close();native[0xcba0]=0;Step(24,movement);
                    FailIf(root._scene.Dialogue.IsOpen,$"{save.Game}: held no-key contact must retain native hint suppression.");
                }
                else
                {
                    FailIf(!keys.Opening || engine.CanCaptureDebugState,$"{save.Game}: active reserved door must open and reject unsupported transient capture.");
                    Step(40);
                    FailIf(keys.Opening || room.GetTerrainInfo(center).Collision!=0 || inventory.GetDungeonSmallKeys(dungeon)!=0,
                        $"{save.Game}: native door/key sprite must finish after one key debit.");
                    Step(3,movement);
                    Load(group,roomId);
                    FailIf(rooms.CurrentRoom.GetMetatile(center)!=0xa0 || rooms.CurrentRoom.IsSolid(center) ||
                        inventory.GetDungeonSmallKeys(dungeon)!=0,$"{save.Game}: re-entry must apply the native room-flag profile without another key debit.");
                }
            }
        }
        finally
        {
            root._scene.Dialogue.Close();
            for(int id=0;id<256;id++){save.SetRoomFlag(group,id,0xff,false);save.SetRoomFlag(group,id,flags[id]);}
            while(inventory.GetDungeonSmallKeys(dungeon)>originalKeys)inventory.TryUseDungeonSmallKey(dungeon);
            while(inventory.GetDungeonSmallKeys(dungeon)<originalKeys)GiveKey();
            Load(originalGroup,originalRoom);
        }
    }

    private static void CompareSharedKeySprites(RoomEntityManager entities,BraceletRom native,string context)
    {
        var effects=entities.Entities<DungeonKeyUseEffect>().OrderBy(entities.InteractionSlot).ToArray();
        int[] slots=Enumerable.Range(0xd2,14).Select(page=>(page<<8)|0x40).Where(slot=>native[slot]!=0 && native[slot+1]==0x17).ToArray();
        FailIf(effects.Length!=slots.Length,context+": checked key allocation differs.");
        for(int index=0;index<effects.Length;index++)
        {
            var key=effects[index];int slot=slots[index];
            FailIf(entities.InteractionSlot(key)!=((slot>>8)&15) ||
                (key.Initialized?key.Phase+1:0)!=native[slot+4] ||
                key.Initialized && (key.Counter!=native[slot+6] || key.Z!=unchecked((sbyte)native[slot+0xf]) ||
                    key.Graphic!=0x42+native[slot+2] || key.AnimationParameter!=native[slot+0x61]) ||
                key.Position!=new Vector2(native[slot+0xd],native[slot+0xb]) || key.Visible!=((native[slot+0x1a]&0x80)!=0),
                context+": native key sprite phase, position, graphic or order differs.");
        }
    }
}

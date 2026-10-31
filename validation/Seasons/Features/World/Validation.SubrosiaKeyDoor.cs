using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSubrosiaKeyDoor(FrontendValidationRoot root,bool batched)
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
        int datingRosa=new GlobalFlagDatabase(rooms.AssetRoot).GetId("GLOBALFLAG_DATING_ROSA");
        bool originalDate=save.HasGlobalFlag(datingRosa);
        byte originalFlags=save.GetRoomFlags(1,0x08);
        int a=inventory.EquippedA,b=inventory.EquippedB;
        int[] originalKeys=Enumerable.Range(0,16).Select(inventory.GetDungeonSmallKeys).ToArray();
        var app=new ApplicationValidationFixture(root);
        try
        {
            inventory.EquipA(0);inventory.EquipB(0);
            foreach(var fixture in new[]{(Dating:false,Full:false,Cancel:false),(Dating:true,Full:false,Cancel:false),
                (Dating:true,Full:true,Cancel:false),(Dating:true,Full:false,Cancel:true)})
            {
                bool dating=fixture.Dating;
                save.SetGlobalFlag(datingRosa,dating);save.SetRoomFlag(1,0x08,0x40,false);
                root._seasonsOpening.DebugTravel(1,0x08);entities.Clear();
                var room=rooms.CurrentRoom;
                Vector2 center=new(56,88),start=new(56.25f,104.5f);
                player.WarpTo(start);player.Face(Vector2I.Up);
                FailIf(room.IsSolid(start) || room.GetMetatile(center)!=0xec,
                    "Seasons Subrosia1:08/$53 must retain a closed door and actual southern approach floor.");
                app.Step(1,Vector2.Zero,batched:batched);
                var native=new BraceletRom(room,player.PrecisePosition,0,inventory.ActiveRing,game:OracleGame.Seasons);
                native.LoadSave(save);native.InitializeWalkingAnimation();
                native[0xcc2d]=1;native[0xcc2e]=0;
                native[0xd009]=unchecked((byte)player.NativePlanarAngle);native[0xd010]=unchecked((byte)player.NativePlanarSpeed);
                foreach(string field in new[]{"wPushingAgainstTileCounter","wLinkPushingDirection","wInformativeTextsShown","wScrollMode"})
                    native.SetNativeByte(memory.FieldAddress(field),memory.ReadField(field));
                var seed=entities.Random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
                var sounds=root._sound.AttachPlayRequestAudit();int updates=0;bool filled=false;
                void Step(int count,Vector2 input=default)=>app.Step(count,input,batched:batched,afterUpdate:()=>
                {
                    native.UpdateLinkItems(entities.FrameCounter,input==Vector2.Up?0x40:0,interactions:true);
                    string context=$"Seasons/Subrosia1:08/date{dating}/full{fixture.Full}/batch{batched}/update{++updates}";
                    FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                        room.GetMetatile(center)!=native[0xcf53] || room.GetTerrainInfo(center).Collision!=native[0xce53] ||
                        save.GetRoomFlags(1,0x08)!=native[0xc808],context+": native approach, tile or room flag differs.");
                    foreach(string field in new[]{"wPushingAgainstTileCounter","wInformativeTextsShown","wDisabledObjects","wDisableLinkCollisionsAndMenu"})
                        FailIf(memory.ReadField(field)!=native.ReadNativeByte(memory.FieldAddress(field)),context+$": {field} differs.");
                    for(int dungeon=0;dungeon<16;dungeon++)
                        FailIf(inventory.GetDungeonSmallKeys(dungeon)!=originalKeys[dungeon] ||
                            native.ReadNativeByte(memory.FieldAddress("wDungeonSmallKeys",dungeon))!=originalKeys[dungeon],context+": Rosa's door must not debit an inventory key.");
                    CompareSharedKeySprites(entities,native,context);
                    var rng=entities.Random.CaptureState();
                    FailIf(rng.Rng1!=native[0xff94] || rng.Rng2!=native[0xff95] || rng.Calls-seed.Calls!=native.RandomCalls ||
                        !sounds.Requests.Where(cue=>cue!=SoundId.SndText).SequenceEqual(native.Sounds),context+": native cues/RNG differ.");
                });
                while(!root._scene.Dialogue.IsOpen && room.GetMetatile(center)==0xec && updates<64)
                {
                    if(fixture.Full && !filled && rooms.TilePushCounter==2)
                    {
                        // Real muted puffs occupy the pool at the player's checked allocation boundary.
                        while(entities.InteractionSlotAvailable)
                        {
                            var puff=entities.Spawn<StationaryDebrisEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
                            int slot=0xd040+entities.InteractionSlot(puff)*0x100;
                            native[slot]=1;native[slot+1]=5;native[slot+2]=0x80;
                            native[slot+0xb]=native[slot+0xd]=24;
                        }
                        filled=true;
                    }
                    Step(1,Vector2.Up);
                }
                if(!dating)
                {
                    FailIf(!root._scene.Dialogue.IsOpen || !root._scene.Dialogue.CurrentMessage.Contains("keyhole") ||
                        rooms.InformativeTextsShown!=0x20 || save.HasRoomFlag(1,0x08,0x40),
                        "Seasons: no Rosa date must show native TX_5109 and retain the locked door.");
                    Step(3,Vector2.Up);root._scene.Dialogue.Close();native[0xcba0]=0;Step(24,Vector2.Up);
                    FailIf(root._scene.Dialogue.IsOpen || room.GetMetatile(center)!=0xec,
                        "Seasons: repeated missing-Rosa contact must preserve the shared hint suppression bit.");
                }
                else
                {
                    FailIf(room.GetMetatile(center)!=0xe8 || !save.HasRoomFlag(1,0x08,0x40) ||
                        entities.Entities<DungeonKeyUseEffect>().Count!=(fixture.Full?0:1) ||
                        fixture.Full && !filled || !fixture.Full && engine.CanCaptureDebugState,
                        "Seasons: Rosa's door must preserve its flag/tile and checked key-sprite admission.");
                    if(!fixture.Full)
                    {
                        bool rejected=false;
                        try {engine.CaptureDebugState(0);}catch(NotSupportedException){rejected=true;}
                        FailIf(!rejected,"Seasons: direct capture must reject the same live key sprite.");
                    }
                    if(fixture.Cancel)
                    {
                        root._seasonsOpening.DebugTravel(1,0x08);
                        FailIf(entities.Entities<DungeonKeyUseEffect>().Count!=0 || !engine.CanCaptureDebugState ||
                            rooms.CurrentRoom.GetMetatile(center)!=0xe8 || !save.HasRoomFlag(1,0x08,0x40) ||
                            sounds.Requests.Count(cue=>cue==SoundId.SndOpenChest)!=1,
                            "Seasons: room reconstruction must cancel the live key sprite while retaining the opened door without replaying its cue.");
                        engine.CaptureDebugState(0).ValidateReconstruction(entities);
                        continue;
                    }
                    Step(32);Step(2,Vector2.Up);
                    FailIf(entities.Entities<DungeonKeyUseEffect>().Count!=0 || !engine.CanCaptureDebugState ||
                        sounds.Requests.Count(cue=>cue==SoundId.SndOpenChest)!=1 || !save.HasGlobalFlag(datingRosa),
                        "Seasons: the key sprite must retire, release capture admission and leave Rosa's date flag intact without repeating the door.");
                    engine.CaptureDebugState(0).ValidateReconstruction(entities);
                    root._seasonsOpening.DebugTravel(1,0x08);
                    FailIf(rooms.CurrentRoom.GetMetatile(center)!=0xe8 || rooms.CurrentRoom.IsSolid(center),
                        "Seasons: re-entry must use the native ordered single-tile room-flag write.");
                }
            }
        }
        finally
        {
            root._scene.Dialogue.Close();save.SetGlobalFlag(datingRosa,originalDate);
            save.SetRoomFlag(1,0x08,0xff,false);save.SetRoomFlag(1,0x08,originalFlags);
            inventory.EquipA(a);inventory.EquipB(b);
            root._seasonsOpening.DebugTravel(originalGroup,originalRoom);
        }
    }
}

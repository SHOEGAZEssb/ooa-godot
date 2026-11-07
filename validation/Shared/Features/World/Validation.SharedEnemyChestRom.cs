namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedEnemyChestRom()
    {
        int fixture=0;
        foreach(var game in new[]{OracleGame.Ages,OracleGame.Seasons})
        foreach(bool collected in new[]{false,true})
        foreach(bool batched in RomHostSchedules(fixture++))
        {
            FrontendValidationRoot? frontend=null;
            try
            {
                bool seasons=game==OracleGame.Seasons;
                if(seasons)
                {
                    frontend=StartSeasonsAfterImpa(batched);
                    frontend._seasonsOpening!.Save.SetRoomFlag(4,0x19,0x20,collected);
                    frontend._seasonsOpening.DebugTravel(4,0x19);
                }
                else
                {
                    ReinitializeGameplayForValidation();
                    _saveData.SetRoomFlag(4,0x65,0x20,collected);LoadValidationRoom(4,0x65);
                }
                GameRoot host=frontend is null?this:frontend;
                var session=frontend?._seasonsOpening;
                var rooms=seasons?session!.Rooms:_rooms;
                var entities=seasons?session!.Entities:_entities;
                var player=seasons?session!.Link:_player;
                var save=seasons?session!.Save:_saveData;
                var memory=seasons?session!.Memory:_runtimeState;
                var sound=seasons?session!.Sound:_sound;
                var dialogue=seasons?session!.Dialogue:_dialogue;
                var room=rooms.CurrentRoom;
                int packed=seasons?0x57:0x58;
                var chest=entities.Entities<EnemyClearChestRoomEntity>().Single();
                if(seasons)FailIf(entities.Entities<StalfosCharacter>().Count!=4 || entities.RoomEnemyCount!=4 ||
                    entities.EntityAdapters<DungeonDoorRoomEntity>().Count()!=2 || chest.Position!=new Vector2(120,88),
                    "The $4:19 native root must construct four Stalfos, two shutters and its $57 map-chest watcher.");
                // Isolate the chest's source gate inputs after proving root construction.
                // Enemy AI/death timing and door scripts have separate ROM fixtures.
                foreach(var other in entities.EntityAdapters<IRoomEntity>().Where(actor=>!ReferenceEquals(actor,chest)).ToArray())
                    entities.RetireEntity(other);
                player.WarpTo(chest.Position+new Vector2(0,24));player.BeginCutsceneControl();
                FailIf((seasons?session!.Collision:_collision).Collides(player.Position),"Enemy-chest fixture must start on actual floor.");
                entities.SetNativeRoomEnemyCount(1);
                int address=((0xd0+entities.NativeInteractionSlot(chest))<<8)|0x40;
                var native=new BraceletRom(room,player.PrecisePosition,0,seasons?session!.Inventory.ActiveRing:_inventory.ActiveRing,game:game);
                native.LoadSave(save);
                // Room identity belongs to RoomSession; these raw WRAM bytes
                // are inputs for the native reference, not a second port owner.
                native.SetNativeByte(memory.FieldAddress("wActiveGroup"),(byte)rooms.ActiveGroup);
                native.SetNativeByte(memory.FieldAddress("wActiveRoom"),(byte)room.Id);
                native.SetNativeByte(memory.FieldAddress("wDungeonIndex"),(byte)rooms.CurrentDungeonIndex);
                native.SetNativeByte(memory.FieldAddress("wScrollMode"),memory.ReadField("wScrollMode"));
                native.SetNativeByte(memory.FieldAddress("wNumEnemies"),1);
                native.SetNativeByte(address,1);native.SetNativeByte(address+1,0x12);native.SetNativeByte(address+2,2);
                native.SetNativeByte(address+0x0b,(byte)chest.Position.Y);native.SetNativeByte(address+0x0d,(byte)chest.Position.X);
                native.ClearSounds();sound.AttachPlayRequestAudit();sound.ClearPlayRequestAudit();
                var app=new ApplicationValidationFixture(host);
                int updates=0;
                void Step(int count=1)=>app.Step(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceInteractions(entities.FrameCounter);updates++;
                    bool deleted=native.ReadNativeByte(address)==0;
                    FailIf(chest.Finished!=deleted || !deleted && chest.Counter!=native.ReadNativeByte(address+6) ||
                        room.Layout[packed]!=native.ReadNativeByte(0xcf00+packed),
                        $"{game}/collected{collected}/batch{batched}/update{updates}: enemy chest counter/deletion/tile differs: counter{chest.Counter}/{native.ReadNativeByte(address+6)}, deleted{chest.Finished}/{deleted}, tile${room.Layout[packed]:x2}/${native.ReadNativeByte(0xcf00+packed):x2}.");
                    foreach(int cue in new[]{SoundId.SndSolvePuzzle,SoundId.SndPoof})
                        FailIf(sound.PlayRequestsFor(cue)!=native.Sounds.Count(value=>value==cue),
                            $"{game}/update{updates}: enemy chest cue${cue:x2} differs.");
                });
                Step(3);
                if(collected)
                {
                    FailIf(!chest.Finished,"An opened chest's $12:$02 watcher must delete in its first state-zero update.");
                    continue;
                }
                FailIf(chest.Counter!=0 || sound.PlayRequestsFor(SoundId.SndSolvePuzzle)!=0,
                    "A live enemy count must keep the chest script at checknoenemies.");
                entities.SetNativeRoomEnemyCount(0);native.SetNativeByte(memory.FieldAddress("wNumEnemies"),0);
                Step(4); // checknoenemies, playsound, createpuff, wait30 each yield.
                FailIf(chest.Counter!=30 || sound.PlayRequestsFor(SoundId.SndSolvePuzzle)!=1,
                    "createChestWhenNoEnemiesScript must install wait30 on its fourth enemy-clear update.");
                dialogue.ShowMessage("Chest pause.",player.Position.Y);
                native.SetNativeByte(memory.FieldAddress("wTextIsActive"),1);Step(3);
                dialogue.Close();native.SetNativeByte(memory.FieldAddress("wTextIsActive"),0);
                memory.SetField("wDisabledObjects",2);native.SetNativeByte(memory.FieldAddress("wDisabledObjects"),2);Step(3);
                memory.SetField("wDisabledObjects",0);native.SetNativeByte(memory.FieldAddress("wDisabledObjects"),0);
                Step(29);FailIf(chest.Counter!=1 || room.Layout[packed]==0xf1,"The chest must remain hidden through wait update29.");
                // Stage contact while the chest tile is still floor. The
                // final state2 update must separate Link, preserving fractions.
                var contact=chest.Position+new Vector2(0.25f,8.5f);
                player.WarpTo(contact);native.Word(0xd00c,(int)(contact.X*256));native.Word(0xd00a,(int)(contact.Y*256));
                FailIf((seasons?session!.Collision:_collision).Collides(player.Position),"Final chest contact must begin on actual floor before tile creation.");
                Step();FailIf(chest.Finished || room.Layout[packed]!=0xf1,"Wait update30 must write $f1 and yield after incstate.");
                Step();FailIf(!chest.Finished,"State2 must finish its final pushout/scriptend update.");
                FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                    player.PrecisePosition==contact,$"{game}: the final chest pushout must match native byte collision and preserve fractions: Link{player.PrecisePosition}, native{native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}.");
                Step(4);
            }
            finally {frontend?.Free();}
        }
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownButtons()
    {
        CompareGroundButtonGameplayRom();
        CompareGroundButtonPreloadRom();
        LoadValidationRoom(4,0xbc);
        var data = new DungeonMechanicDatabase();
        var record = data.GetRoomRecords(4,0xbc).Single(r=>r.Id==0x09 && r.SubId==0x80);
        // Arithmetic-only contact fixtures, not movement/reachability claims.
        // Expectations come from byte SUB/ADD/SLA/CP in checkObjectsCollidedFromVariables.
        foreach(var test in new (Vector2 Offset,bool Hit)[] {
            (new(-8,0),true),(new(-9,0),false),(new(7,0),true),(new(8,0),false),
            (new(0,-8),true),(new(0,-9),false),(new(0,7),true),(new(0,8),false),
            (new(-8,-8),true),(new(-8,8),false),
            (new(-8.25f,0),false),(new(-7.75f,0),true),(new(7.99f,0),true),(new(8.01f,0),false),
            (new(248,0),true),(new(0,248),true),(new(-264,0),true),(new(256,256),true)
        })
        {
            bool trigger=false; int writes=0; int sounds=0;
            var button = new GroundButtonRoomEntity(record,_currentRoom,data,(_,value)=>trigger=value,
                (_,_)=>writes++,_=>sounds++);
            _player.WarpTo(button.Position+test.Offset);
            button.UpdateFrame(new RoomEntityFrame(_player,0,false),new List<RoomEntitySpawn>());
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,
                Mathf.FloorToInt(_player.Position.X),Mathf.FloorToInt(_player.Position.Y));
            rom.Word(0xd00c,(int)(_player.PrecisePosition.X*256));
            rom.Word(0xd00a,(int)(_player.PrecisePosition.Y*256));
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = 0x80;
            rom[0xd0cb] = 56; rom[0xd0cd] = 72;
            rom.AdvanceParts(0);
            FailIf(button.Pressed!=test.Hit || trigger!=test.Hit || writes!=(test.Hit?1:0) || sounds!=(test.Hit?1:0),
                $"PART$09 byte overlap at relative {test.Offset} must be {test.Hit}, with one press write/sound only on contact.");
            FailIf(button.Pressed != (rom[0xd0f0] != 0) || trigger != ((rom[0xcca0]&1) != 0) ||
                writes != ((rom[0xcce0]-rom[0xccdf])&31) || sounds != rom.Sounds.Count ||
                rom.Sounds.Any(cue => cue != SoundId.SndSplash),
                $"PART$09 relative {test.Offset}: native byte overlap/trigger/write/cue differs.");
            button.Free();
        }
        LoadValidationRoom(0,0x60);
    }
}

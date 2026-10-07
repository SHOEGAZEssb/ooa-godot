using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateDungeonDoorScriptsRom()
    {
        var data = new DungeonMechanicDatabase();
        int fixture = 0;
        foreach (var c in new[] { (Group:4,Room:0x54,Sub:0x11,Packed:0x20,Tile:0xb3),
            (Group:5,Room:0x4f,Sub:0x10,Packed:0x06,Tile:0x78),
            (Group:5,Room:0x51,Sub:0x10,Packed:0x03,Tile:0x78),
            (Group:5,Room:0x5c,Sub:0x10,Packed:0x0b,Tile:0x78) })
        foreach (bool entered in c.Group == 5 ? new[] { false,true } : new[] { false })
        foreach (bool batch in entered && c.Room == 0x4f ? new[] { false,true } : RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(c.Group,c.Room);
            var record = data.GetRoomRecords(c.Group,c.Room).Single(row => row.Id == 0x1e && row.SubId >= 0x10);
            FailIf(record.Order != 0 || record.SubId != c.Sub || record.PackedPosition != c.Packed || record.Parameter != 0 ||
                _entities.Entities<DungeonDoorRoomEntity>().Count(door => door.SubId == c.Sub) != 1 ||
                _currentRoom.Layout[c.Packed] != c.Tile,
                $"Original mainData.s room${c.Group:x1}:${c.Room:x2} entrance controller placement/source tile must be retained by the factory.");
            _entities.Clear(); _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 center = new((c.Packed&15)*16+8,(c.Packed>>4)*16+8);
            Vector2 inward = c.Sub == 0x11 ? Vector2.Right : Vector2.Down;
            if (entered) _currentRoom.SetPositionTileAndCollision(center,0xa0,0,0);
            // Declared completed entry substitution. Whole scroll/preload
            // has separate ROM coverage; walk out through unchanged floor.
            Vector2 start = center+inward*(entered ? 0 : 16)+new Vector2(0.25f,0.5f);
            _player.WarpTo(start); _player.Face((Vector2I)inward);
            FailIf(_collision.Collides(start),"Entrance door approach must begin on original or substituted open floor.");
            _player.SetLocalRespawnPosition(center,(Vector2I)inward);
            _runtimeState.SetWramByte(WramAddress.wTmpcfc0,0xa4);
            int cameraX = c.Packed == 0x0b ? 80 : 0;
            var door = new DungeonDoorRoomEntity(record,_currentRoom,data,() => 0,_entities.TriggerIsActive,
                p => p-new Vector2(cameraX,0),() => _entities.FrameCounter,_sound.PlaySound,default,true,_rooms.TrySetTile,
                _entities.UpdateBossShutterSignal,_entities.IsScriptTextActive,_entities.IsOutgoingEntity,
                () => _entities.DoorPaletteFadeActive,flipEntryScratch:() =>
                    _runtimeState.SetWramByte(WramAddress.wTmpcfc0,(byte)(_runtimeState.ReadWramByte(WramAddress.wTmpcfc0)^data.DoorEntryScratchMask)));
            _entities.AddEntity(door);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,c.Sub == 0x11 ? 1 : 2,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xffac] = (byte)cameraX;
            if (c.Group == 5) rom[0xd009] = 0; // Full room load resets the underwater convergence angle.
            rom[0xd240] = 1; rom[0xd241] = 0x1e; rom[0xd242] = (byte)c.Sub; rom[0xd24b] = (byte)c.Packed;
            rom[0xcc21] = (byte)center.Y; rom[0xcc22] = (byte)center.X;
            rom[0xcc23] = c.Sub == 0x11 ? (byte)1 : (byte)2; rom[0xcfc0] = 0xa4;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1,int angle = 0xff)
            {
                int tick = 0;
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : inward,
                    angle == 0xff ? [] : c.Sub == 0x11 ? ["move_right"] : ["move_down"],
                    angle == 0xff ? [] : c.Sub == 0x11 ? ["move_right"] : ["move_down"],batch,() => {
                int keys = angle == 0xff ? 0 : c.Sub == 0x11 ? 0x10 : 0x80;
                rom.UpdateGameplay(tick++ == 0 ? keys : 0,keys,angle,_entities.FrameCounter);
                CompareSomariaMotionRom(rom,"Entrance door walking/swimming handoff");
                rom.AdvanceTileGraphics();
                var random = _random.CaptureState();
                FailIf(door.Finished != (rom[0xd240] == 0) || SomariaPrivate<int>(door,"_counter") != rom[0xd246] ||
                    door.Counter2Alias != rom[0xd247] || _entities.BossEntrySignal != rom[0xcc93] ||
                    _runtimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0] ||
                    _player.LocalRespawnPosition != new Vector2(rom[0xcc22],rom[0xcc21]) ||
                    CarriedObjectMotion.DirectionIndex(_player.LocalRespawnFacingVector) != rom[0xcc23] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls,
                    $"Entrance door${c.Sub:x2} room${c.Group:x1}:${c.Room:x2}, entered={entered}, batch={batch}: runtime={SomariaPrivate<DoorState>(door,"_state")}/finished={door.Finished}/counter={SomariaPrivate<int>(door,"_counter")}/scratch=${_runtimeState.ReadWramByte(WramAddress.wTmpcfc0):x2}; native=${rom[0xd244]:x2}:${rom[0xd245]:x2}/alive={rom[0xd240]}/counter={rom[0xd246]}/scratch=${rom[0xcfc0]:x2}.");
                });
            }
            Step(3);
            _dialogue.ShowGameplayMessage("Entrance script pause",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            if (c.Group == 5)
                for (int stroke = 0; !door.Finished && stroke < 16; stroke++)
                {
                    Step(4,16); Step(); // Release/repress renews the native Mermaid impulse.
                }
            else Step(32,8);
            Step(8);
            FailIf(!door.Finished || _runtimeState.ReadWramByte(WramAddress.wTmpcfc0) != 0xa5 ||
                _currentRoom.Layout[c.Packed] != (entered ? 0x78 : c.Tile) ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != (entered ? 2 : 0),
                $"Entrance room${c.Group:x1}:${c.Room:x2}, entered={entered} must complete once: state={SomariaPrivate<DoorState>(door,"_state")}, finished={door.Finished}, scratch=${_runtimeState.ReadWramByte(WramAddress.wTmpcfc0):x2}, tile=${_currentRoom.Layout[c.Packed]:x2}, cues=[{string.Join(',',sounds.Requests)}], Link={_player.PrecisePosition}/{start}, nativeState=${rom[0xd004]:x2}:${rom[0xd005]:x2}, input=${rom[0xcc2b]:x2}, walls=${rom[0xd033]:x2}, speed=${rom[0xd050]:x2}.");
        }
        CompareTorchDoorScriptsRom();
        CompareTorchDoorContactRom();
    }
}

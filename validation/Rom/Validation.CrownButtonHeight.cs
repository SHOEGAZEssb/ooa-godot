using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownButtonHeight()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xbc); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,0xbc).Single(row => row.Id == 9 && row.PackedPosition == 0x34);
            FailIf(record.SubId != 0x80 || _currentRoom.Layout[0x34] != 0x0c,
                "Height comparison requires original reusable PART$09:$80/button$34:$0c.");
            var button = new GroundButtonRoomEntity(record,_currentRoom,data,_entities.SetTrigger,
                (packed,tile) => _rooms.TrySetTile(packed,tile),_sound.PlaySound);
            _entities.AddEntity(button); _entities.SetTrigger(7,true);
            Vector2 start = new(72.25f,88.5f); _player.WarpTo(start); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(start),"Height comparison must approach the actual button through original floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,72,88) { HostilePartsEnabled = true };
            rom.Word(0xd00c,72*256+64); rom.Word(0xd00a,88*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcca0] = 0x80;
            rom[0xd0c0] = 1; rom[0xd0c1] = 9; rom[0xd0c2] = 0x80;
            rom[0xd0cb] = 56; rom[0xd0cd] = 72;
            var sounds = _sound.AttachPlayRequestAudit();
            // Walk to the last floor position outside the asymmetric8-pixel
            // pressure interval. The following script owner supplies XY/Z.
            StepSomariaMotionRom(rom,24,batch,0);
            FailIf(_player.PrecisePosition != new Vector2(72.25f,64.5f) || button.Pressed || rom[0xd0f0] != 0,
                "Real floor approach must stop outside the button before the declared scripted height inputs.");
            object owner = new(); _player.BeginCutsceneControl(owner:owner);
            int nativeFrame = _entities.FrameCounter,update = 0;
            // The declared script owns Link's bytes; execute the original PART
            // dispatcher beneath the complete application loop. This comparison
            // excludes the script producer and full new-game fall animation.
            void Position(Vector2 point)
            {
                FailIf(_currentRoom.IsSolid(point),"Scripted button inputs must remain on original floor.");
                _player.SetScriptedPosition(point);
                rom.Word(0xd00c,(int)(point.X*256)); rom.Word(0xd00a,(int)(point.Y*256));
            }
            void Height(int fixedZ)
            {
                _player.SetCutsceneDrawZFixed(fixedZ); rom.Word(0xd00e,fixedZ);
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                nativeFrame = (nativeFrame+1)&0xff; rom.AdvanceParts(nativeFrame); rom.AdvanceTileGraphics();
                string context = $"Scripted button height batch={batch}, update{++update}";
                FailIf(_entities.FrameCounter != nativeFrame || button.Pressed != (rom[0xd0f0] != 0) ||
                    button.ReleaseCounter != rom[0xd0c6] || button.Finished != (rom[0xd0c0] == 0) ||
                    _entities.ActiveTriggers != rom[0xcca0] || _player.ObjectZHigh != rom[0xd00f] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    _currentRoom.Layout[0x34] != rom[0xcf34] ||
                    _currentRoom.GetTerrainInfo(new(72,56)).Collision != rom[0xce34] ||
                    _currentRoom.GetUnderlyingMetatile(new(72,56)) != rom.Underlying(0x34) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),context+": pressure/trigger/height/Link/tile/cue differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls,context+": shared RNG differs.");
            });
            try
            {
                Position(new(72.25f,56.5f)); Height(-256); Step(2);
                FailIf(button.Pressed,"Initial zh$ff must reject pressure while overlapping the real button.");
                Height(0); Step();
                FailIf(!button.Pressed,"Changing only zh to zero must permit pressure on the next PART pass.");
                Height(256); Step(2);
                FailIf(!button.Pressed,"Nonzero zh during overlap must retain an already pressed button.");
                Position(new(24.25f,24.5f)); Step();
                FailIf(button.Pressed,"Leaving XY overlap must release pressure even when zh is nonzero.");
                Position(new(72.25f,56.5f)); Height(-1); Step();
                FailIf(button.Pressed,"Fixed z$ffff must reject pressure via its high byte$ff.");
                Height(255); Step();
                FailIf(!button.Pressed,"Fractional z$00ff must permit pressure because only zh is tested.");
                Position(new(24.25f,24.5f)); Step(); Position(new(72.25f,56.5f));
                Height(0); _player.BeginNewGameSlowFall(0); rom[0xd004] = 9; Step();
                FailIf(!button.Pressed || !_player.IsNewGameSlowFalling,
                    "A declared fall state with zh0 must retain native PART pressure eligibility.");
                _player.EndNewGameSlowFall(); Position(new(24.25f,24.5f)); Step(3);
                FailIf(button.Pressed || _entities.ActiveTriggers != 0x80,
                    "Repeated pressure/release must preserve unrelated triggerbit7 without a delayed re-press.");
            }
            finally
            {
                _player.EndNewGameSlowFall(); _player.SetCutsceneDrawZFixed(0); _player.EndCutsceneControl(owner);
            }
        }
    }
}

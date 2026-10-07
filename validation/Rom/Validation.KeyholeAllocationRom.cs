using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareLibraryKeyholeAllocationRom()
    {
        int fixture = 0;
        foreach (var row in new[] { (Free:0,Slide:0xff), (Free:0,Slide:0x22), (Free:1,Slide:0xff) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); _saveData.SetRoomFlag(1,0xa5,0x80,false);
            _inventory.GiveTreasure(TreasureId.LibraryKey,1); _inventory.EquipA(0); _inventory.EquipB(0);
            LoadValidationRoom(1,0xa5); RetainKeyholeControllerRom();
            _player.WarpTo(new(72,72)); _player.Face(Vector2I.Up);
            var gate = _roomEvents.Get<LibraryKeyholeEvent>(); var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,72,72);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x13;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Audit()
            {
                string context = $"Library allocation free={row.Free}, slide=${row.Slide:x2}, batch={batch}, update={++update}";
                FailIf(_rooms.TilePushCounter != rom[0xcc6a] || gate.BlocksGameplay != ((rom[0xcc8a] & 0x81) != 0) ||
                    _roomEvents.MenusDisabled != (rom[0xcc02] != 0) || gate.BlocksGameplay && gate.Counter != rom[0xd246],
                    context + ": shared contact/script/input/menu clock differs.");
                for (int room = 0; room < 256; room++)
                    FailIf(_saveData.GetRoomFlags(1,room) != rom[0xc800+room],context + $": room flag${room:x2} differs.");
                var keys = _entities.Entities<OverworldKeyUseEffect>();
                var slots = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x18).ToArray();
                FailIf(keys.Count != slots.Length,context + ": key allocation/lifetime differs.");
                if (keys.Count != 0)
                {
                    var key = keys.Single(); int slot = slots.Single();
                    FailIf(_entities.InteractionSlot(key) != 15 || key.State != rom[slot+4] || key.Counter != rom[slot+6] ||
                        key.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                        (key.ZFixed & 0xffff) != rom.Word(slot+0xe) || (key.SpeedZ & 0xffff) != rom.Word(slot+0x14) ||
                        key.Visible != ((rom[slot+0x1a] & 0x80) != 0),context + ": last-slot key state/XYZ/speed/visibility differs.");
                }
                int nativePuffs = Enumerable.Range(0xd2,14).Count(page => rom[(page << 8) | 0x40] != 0 && rom[(page << 8) | 0x41] == 5);
                var random = _random.CaptureState();
                FailIf(_entities.Entities<PuzzlePuffEffect>().Count != nativePuffs ||
                    !sounds.Requests.SequenceEqual(rom.Sounds) || random.Rng1 != rom[0xff94] ||
                    random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                    context + ": occupied-slot retirement/cues/RNG differ.");
            }
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:Audit,contextPrefix:"Library key allocation");
            Step();
            for (int wait = 0; rom[0xcc6a] != 2 && wait < 40; wait++) Step(angle:0);
            FailIf(rom[0xcc6a] != 2 || _collision.Collides(_player.Position),
                "Key allocation boundary must follow the actual Library floor approach.");
            // Hold the physical slots only at the allocation boundary. These
            // ordinary muted puffs retire through their real later updates.
            for (int index = 0; index < 13 - row.Free; index++)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
                int slot = 0xd000 + _entities.InteractionSlot(puff) * 0x100 + 0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80;
                rom[slot+0xb] = rom[slot+0xd] = 24;
            }
            for (int channel = 2; channel <= 4; channel++)
            {
                byte value = channel == 2 ? (byte)row.Slide : (byte)(0x60 + channel);
                _sound.SetNativeChannelPitchSlide(channel,value); rom[0xc03f+channel] = value;
            }
            // Execute the actual front dispatcher before the audio queue is
            // drained. This boundary isolates its echo writes; continuation
            // below uses the complete gameplay loop, including normal audio.
            bool returned = _keyholes.UpdatePushAttempt(_player.PrecisePosition,Vector2I.Up,Vector2.Up);
            rom.InteractWithFrontTile(); Audit();
            FailIf(!returned || !gate.BlocksGameplay || !_inventory.HasTreasure(TreasureId.LibraryKey) ||
                _entities.Entities<OverworldKeyUseEffect>().Count != row.Free,
                "Full/last-free key allocation retains key, flag, signal and $81 handoff without retrying a missing sprite.");
            for (int channel = 2; channel <= 4; channel++)
                FailIf(unchecked((byte)_sound.Channel(channel).PitchSlide) != rom[0xc03f+channel],
                    $"Unchecked key allocation audio echo at ${0xc03f+channel:x4} differs.");
            FailIf(rom[0xc041] != (row.Free == 0 ? unchecked((byte)(row.Slide+1)) : row.Slide) ||
                rom[0xc042] != (row.Free == 0 ? 4 : 0x63) || rom[0xc043] != (row.Free == 0 ? 4 : 0x64),
                "Native full-pool failure increments echo$e041 and writes key subid4 to$e042/$e043.");
            Step(120);
            FailIf(gate.BlocksGameplay || _entities.Entities<OverworldKeyUseEffect>().Count != 0 ||
                _entities.Entities<PuzzlePuffEffect>().Count != 0 || _currentRoom.GetMetatile(new(40,40)) != 0xee ||
                _currentRoom.GetMetatile(new(56,40)) != 0xef || rom[0xcf22] != 0xee || rom[0xcf23] != 0xef,
                "Allocation contention must not delay the native Library door script or retry after slots retire.");
            Step(12,0);
            FailIf(_entities.Entities<OverworldKeyUseEffect>().Count != 0 || sounds.Requests.Count(cue => cue == SoundId.SndOpenChest) != 1,
                "Renewed opened-keyhole contact must not recreate the skipped/retired key sprite.");
        }
    }
}

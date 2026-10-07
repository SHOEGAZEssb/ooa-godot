using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateLibraryKeyholeRom()
    {
        CompareKeyholeScrollRom();
        CompareLibrarySideKeyholeRom();
        CompareLibraryKeyholeAllocationRom();
        int fixture = 0;
        foreach (int ownership in new[] { 1,0,2 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.LoseTreasure(0x46);
            if (ownership == 1) _inventory.GiveTreasure(0x46,1);
            if (ownership == 2) _inventory.GiveTreasure(0x43,1);
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword);
            _saveData.SetRoomFlag(1,0xa5,0x80,false);
            LoadValidationRoom(1,0xa5);
            var allocation = RetainKeyholeControllerRom();
            var gate = _roomEvents.Get<LibraryKeyholeEvent>();
            _player.WarpTo(new(72,72)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(72,56)) != 0xec ||
                _currentRoom.GetMetatile(new(40,40)) != 0xb0 || _currentRoom.GetMetatile(new(56,40)) != 0xb1,
                "Library$1:$a5 must use original layout02:$a5 floor, keyhole$34:$ec and doors$22:$b0/$23:$b1.");
            FailIf(!_keyholes.Database.TryGet(1,0xa5,out var data) ||
                data is not { Treasure:0x46,SubId:4,TileBase:0x14,Palette:0 },
                "Library Key must retain its independently sourced $18:$04 visual and treasure$46.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,72,72);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x13;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int held = 0,int pressed = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held,pressed,afterUpdate:() =>
            {
                string context = $"Library$1:$a5 ownership={ownership}, batch={batch}, update={++update}";
                FailIf(allocation.Initialized != (rom[0xd244] != 0) || allocation.Finished || rom[0xd240] == 0,
                    context + ": placed controller state/lifetime differs.");
                FailIf(_keyholes.RemainingPushFrames != rom[0xcc6a] ||
                    _rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                    context + ": contact clock/shared hint/modal differs.");
                FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0] ||
                    gate.BlocksGameplay != ((rom[0xcc8a] & 0x81) != 0) ||
                    _roomEvents.MenusDisabled != (rom[0xcc02] != 0) ||
                    gate.BlocksGameplay && gate.Counter != rom[0xd246],
                    context + $": shared signal/input/menu/event clock runtime={gate.BlocksGameplay}/{gate.Counter}, ROM=${rom[0xcc8a]:x2}/${rom[0xcc02]:x2}/{rom[0xd246]}.");
                FailIf(_player.IsAttacking != (rom[0xd200] != 0 && rom[0xd201] == 0x05),
                    context + ": Sword parent allocation differs.");
                for (int room = 0; room < 256; room++)
                    FailIf(_saveData.GetRoomFlags(1,room) != rom[0xc800+room],context + $": room flag${room:x2} differs.");
                var keys = _entities.Entities<OverworldKeyUseEffect>();
                var slots = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x18).ToArray();
                FailIf(keys.Count != slots.Length,context + ": key sprite lifetime differs.");
                for (int i = 0; i < slots.Length; i++)
                {
                    var key = keys[i]; int slot = slots[i];
                    FailIf(_entities.InteractionSlot(key) != (slot >> 8)-0xd0 ||
                        key.State != rom[slot+4] || key.Counter != rom[slot+6] ||
                        key.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) ||
                        (key.ZFixed & 0xffff) != rom.Word(slot+0xe) || (key.SpeedZ & 0xffff) != rom.Word(slot+0x14) ||
                        key.Visible != ((rom[slot+0x1a] & 0x80) != 0),context + ": key slot/state/clock/XYZ/speed/visibility differs.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context + $": gameplay cues/RNG differ: runtime={string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}, ROM={string.Join(',',rom.Sounds)}.");
            },contextPrefix:$"Library$1:$a5 ownership={ownership}, after={update}");
            Step();
            for (int wait = 0; !gate.BlocksGameplay && !_dialogue.IsOpen &&
                (ownership != 1 || rom[0xcc6a] != 2) && wait < 40; wait++) Step(angle:0);
            if (ownership != 1)
            {
                FailIf(!_dialogue.IsOpen || rom.TextGeneration != 1 || rom.Word(0xcba2) != 0x5509 ||
                    _dialogue.CurrentMessage != "Huh? This has a\nkeyhole.",
                    "Missing Library Key or another named key must show source TX_5109.");
                Step(3,0); _dialogue.Close(); rom[0xcba0] = 0; Step(24,0);
                FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || gate.BlocksGameplay,
                    "Repeated missing Library Key contact must not restart the hint or event.");
                continue;
            }
            FailIf(rom[0xcc6a] != 2,"Library keyhole must reach its final contact update on actual floor.");
            Step(angle:0,held:1,pressed:1);
            FailIf(!gate.BlocksGameplay || _player.CutsceneControlled || !_inventory.HasTreasure(0x46) ||
                sounds.Requests.Contains(SoundId.SndCtrlStopMusic),
                "Library signal read must yield before music, retain the key and keep normal Link/parents.");
            Step(13);
            FailIf(_entities.Entities<OverworldKeyUseEffect>() is not [{ State:2,Counter:60,ZFixed:-0xdd0,SpeedZ:8 }],
                "Library key sprite must reach the source -$0dd0 apex with a $3c hold.");
            _dialogue.ShowMessage("Library pause.",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; gate.BlocksGameplay && wait < 130; wait++) Step();
            FailIf(gate.BlocksGameplay || _entities.Entities<OverworldKeyUseEffect>().Count != 0 || !_inventory.HasTreasure(0x46) ||
                _currentRoom.GetMetatile(new(40,40)) != 0xee || _currentRoom.GetMetatile(new(56,40)) != 0xef ||
                _currentRoom.GetMetatile(new(72,56)) != 0xec || sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Library script must open $22:$ee/$23:$ef, retain keyhole$34/key$46 and complete its shared tail once.");
            Step(4); Step(4,0); Step(held:1,pressed:1);
            FailIf(!_player.IsAttacking,"Fresh Sword use must resume after Library enableinput.");
        }
        ValidateLibraryKeyholeReloads();
        GD.Print("Validated native Library keyhole: actual floor contact, missing/wrong/retained key, controller/key slots and clocks, text, two queued door writes, input/menu handoff, cues/RNG, repeat and era reloads.");
    }

    private void ValidateLibraryKeyholeReloads()
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(0x46,1);
        var gate = _roomEvents.Get<LibraryKeyholeEvent>();
        foreach (int elapsed in new[] { 10,65,120 })
        {
            _saveData.SetRoomFlag(1,0xa5,0x80,false);
            LoadValidationRoom(1,0xa5); _player.WarpTo(new(72,72));
            for (int i = 0; i < 40 && !gate.BlocksGameplay; i++) StepGameplayUpdates(1,Vector2.Up);
            FailIf(!gate.BlocksGameplay,"Library opening must start before cancellation/reload probes.");
            StepGameplayUpdates(elapsed,Vector2.Zero); LoadValidationRoom(0,0x11);
            FailIf(gate.HasState || _roomEvents.MenusDisabled || _player.CutsceneControlled,
                "Cancelling Library opening leaked input/menu ownership.");
            foreach (int group in new[] { 1,0 })
            {
                LoadValidationRoom(group,0xa5); StepGameplayUpdates(1,Vector2.Zero);
                FailIf(_currentRoom.GetMetatile(new(40,40)) != 0xee || _currentRoom.GetMetatile(new(56,40)) != 0xef ||
                    !_inventory.HasTreasure(0x46) || gate.HasState || _entities.EntityAdapters<KeyholeControllerRoomEntity>().Any(),
                    $"Library group${group:x} reload must reconstruct both doors from past flag$80 and retire its controller.");
            }
        }
        var sounds = _sound.AttachPlayRequestAudit();
        foreach (int group in new[] { 1,0 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            LoadValidationRoom(group,0xa5); _player.WarpTo(new(72,72));
            // Original $f8/$2b floor leads left from the keyhole to the
            // midpoint of the two partial-collision doorway tiles.
            // Past $f8 grass uses SPEED_c0; present $2a/$2b floor uses
            // SPEED_100. Both source routes travel24px to the same midpoint.
            StepGameplayUpdates(group == 1 ? 32 : 24,Vector2.Left);
            FailIf(_collision.Collides(_player.Position) || _player.Position != new Vector2(48,72),
                $"Opened Library era${group:x} repeat{repeat} forecourt approach differs: Link={_player.Position}, solid={_collision.Collides(_player.Position)}, room={_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}, blocked={_roomEvents.Active}, text={_dialogue.IsOpen}.");
            StepGameplayUpdates(60,Vector2.Up);
            for (int i = 0; i < 180 && _transitions.IsTransitioning; i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 5 || _currentRoom.Id != (group == 1 ? 0xec : 0xd0),
                $"Library era${group:x} repeat{repeat} did not finish its source doorway warp: {_rooms.ActiveGroup:x}:{_currentRoom.Id:x2}.");
        }
        FailIf(sounds.Requests.Contains(SoundId.SndOpenChest),"Re-entering either Library must not replay key use.");
    }
}

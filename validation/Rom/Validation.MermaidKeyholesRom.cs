using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMermaidKeyholesRom()
    {
        int fixture = 0;
        foreach (var entrance in new[] { (Group:1,Room:0x0e,Key:0x45,Other:0x44),
            (Group:3,Room:0x0f,Key:0x44,Other:0x45) })
        foreach (int ownership in new[] { 1,0,2 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.LoseTreasure(entrance.Key); _inventory.LoseTreasure(entrance.Other);
            if (ownership != 0) _inventory.GiveTreasure(ownership == 1 ? entrance.Key : entrance.Other,1);
            _inventory.GiveTreasure(TreasureId.Sword,1); _inventory.EquipA(TreasureId.Sword);
            _saveData.SetRoomFlag(entrance.Group,entrance.Room,0x80,false);
            LoadValidationRoom(entrance.Group,entrance.Room);
            var allocation = RetainKeyholeControllerRom();
            var controller = _roomEvents.Get<MermaidsCaveEntranceEvent>();
            _player.WarpTo(new(104,56)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(104,24)) != 0xae,
                "Mermaid keyhole must be approached from its original floor below tile$ae.");
            var randomSeed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,randomSeed,_currentRoom,0,104,56);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // Other room actors remain outside this focused fixture; retain
            // the actual placed controller and its descendant slot order.
            rom[0xd240] = 1; rom[0xd241] = 0x90; rom[0xd242] = 0x12;
            rom[0xd24b] = 24; rom[0xd24d] = 104;
            rom[0xcc35] = rom[0xcc46] = (byte)_sound.ActiveMusic;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int held = 0,int pressed = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held,pressed,afterUpdate:() =>
            {
                string context = $"Mermaid keyhole${entrance.Group:x1}:${entrance.Room:x2}, ownership={ownership}, batch={batch}, update={++update}";
                FailIf(allocation.Initialized != (rom[0xd244] != 0) || allocation.Finished || rom[0xd240] == 0,
                    context + ": placed controller initialization/lifetime differs.");
                FailIf(_rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                    context + ": shared hint/modal differs.");
                FailIf(_keyholes.RemainingPushFrames != rom[0xcc6a],
                    context + $": contact clock runtime={_keyholes.RemainingPushFrames}, ROM={rom[0xcc6a]}.");
                FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) != rom[0xcfc0],
                    context + ": retained keyhole signal differs.");
                FailIf(controller.BlocksGameplay != ((rom[0xcc8a] & 0x81) != 0),context + ": keyhole/event input lock differs.");
                FailIf(_roomEvents.MenusDisabled != (rom[0xcc02] != 0),context + ": source wMenuDisabled differs.");
                FailIf(controller.BlocksGameplay && controller.Counter != rom[0xd246],
                    context + $": event clock runtime={controller.Counter}, ROM={rom[0xd246]}, pointer=${rom.Word(0xd258):x4}, text=${rom[0xcba0]:x2}.");
                FailIf(_player.IsAttacking != (rom[0xd200] != 0 && rom[0xd201] == 0x05),
                    context + ": Sword parent allocation differs.");
                for (int room = 0; room < 256; room++)
                    FailIf(_saveData.GetRoomFlags(entrance.Group,room) != rom[0xc800 + room],
                        context + $": room flag${room:x2} differs.");
                var keys = _entities.Entities<OverworldKeyUseEffect>();
                var slots = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x18).ToArray();
                FailIf(keys.Count != slots.Length,context + ": key sprite lifetime differs.");
                for (int i = 0; i < slots.Length; i++)
                {
                    var key = keys[i]; int slot = slots[i];
                    FailIf(_entities.InteractionSlot(key) != (slot >> 8) - 0xd0 ||
                        key.State != rom[slot + 4] || key.Counter != rom[slot + 6] ||
                        (key.ZFixed & 0xffff) != rom.Word(slot + 0xe) || (key.SpeedZ & 0xffff) != rom.Word(slot + 0x14) ||
                        key.Position != new Vector2(rom[slot + 0xd],rom[slot + 0xb]) ||
                        key.Visible != ((rom[slot + 0x1a] & 0x80) != 0),context + ": key sprite state/clock/XYZ/speed/visibility differs.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls - randomSeed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),context +
                    $": gameplay cue/RNG order differs: runtime={string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}, ROM={string.Join(',',rom.Sounds)}; RNG calls={random.Calls-randomSeed.Calls}/{rom.RandomCalls}.");
            },contextPrefix:$"Mermaid${entrance.Group:x1}:${entrance.Room:x2}, ownership={ownership}, after={update}");
            Step();
            for (int wait = 0; !controller.BlocksGameplay && !_dialogue.IsOpen &&
                (ownership != 1 || rom[0xcc6a] != 2) && wait < 40; wait++) Step(angle:0);
            if (ownership != 1)
            {
                FailIf(!_dialogue.IsOpen || _rooms.InformativeTextsShown != 0x20 || rom.TextGeneration != 1 ||
                    rom.Word(0xcba2) != 0x5509 || _dialogue.CurrentMessage != "Huh? This has a\nkeyhole.",
                    "Missing or wrong-era key must show original TX_5109 with shared mask$20.");
                Step(3,0); _dialogue.Close(); rom[0xcba0] = 0;
                Step(24,0);
                FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || controller.BlocksGameplay,
                    "Repeated missing-key contact must not reopen TX_5109 or start the event.");
                continue;
            }
            // Fresh A on the final doubled decrement reaches the tile handler
            // first. Its carry return prevents allocating the Sword parent.
            FailIf(rom[0xcc6a] != 2 || _keyholes.RemainingPushFrames != 2,
                "Owned keyhole must reach its final contact update through the original wall.");
            Step(angle:0,held:1,pressed:1);
            FailIf(!controller.BlocksGameplay || !_inventory.HasTreasure(entrance.Key) ||
                !_saveData.HasRoomFlag(entrance.Group,entrance.Room,0x80) || rom[0xcc6a] != 0,
                "Owned key must remain held while zero contact clock sets flag$80 and starts the original event.");
            Step(13); // State0 ran on allocation; thirteen vertical updates reach the apex.
            FailIf(_entities.Entities<OverworldKeyUseEffect>() is not [{ State:2,Counter:60,ZFixed:-0xdd0,SpeedZ:8 }],
                "Overworld key must reach its independently sourced -$0dd0 apex with a $3c hold.");
            _dialogue.ShowMessage("Key sprite pause.",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int wait = 0; controller.BlocksGameplay && wait < 130; wait++) Step();
            FailIf(controller.BlocksGameplay || _entities.Entities<OverworldKeyUseEffect>().Count != 0 ||
                _currentRoom.GetMetatile(new(104,24)) != 0xaf || !_inventory.HasTreasure(entrance.Key) ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Mermaid keyhole event must finish its door/key lifetime and retain the named key.");
            Step(4); Step(4,0);
            Step(held:1,pressed:1);
            FailIf(!_player.IsAttacking,"Sword input must resume after the keyhole releases $81 control.");
        }
    }
}

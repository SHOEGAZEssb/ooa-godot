using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareReachableKeyDoorRom()
    {
        foreach (bool hasKey in new[] { false,true })
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xb3); _entities.Clear();
            if (hasKey) _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            Vector2 start = new(204.25f,136.5f),center = new(232,136);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(center) != 0x71,
                "Key door must be approached from actual room$4:$b3 floor.");
            var oldScreen = _entities.WorldToScreen;
            _entities.WorldToScreen = position => position - new Vector2(80,48);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,204,136);
            rom.Word(0xd00a,(int)(start.Y * 256)); rom.Word(0xd00c,(int)(start.X * 256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(5);
            rom[0xffaa] = 48; rom[0xffac] = 80;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                {
                    string context = $"Reachable key door$4:$b3 key={hasKey}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context,excludeText:!hasKey);
                    FailIf(_keyDoors.RemainingPushFrames != rom[0xcc6a],context + ": shared contact clock differs.");
                    FailIf(_rooms.InformativeTextsShown != rom[0xccd7] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                        context + ": shared hint mask/modal handoff differs.");
                    for (int room = 0; room < 256; room++)
                        FailIf(_saveData.GetRoomFlags(4,room) != rom[0xc900 + room],
                            context + $": room flags$4:${room:x2} differ.");
                });
            try
            {
                Step();
                for (int wait = 0; !_keyDoors.Opening && !_dialogue.IsOpen && wait < 40; wait++)
                {
                    Step(angle:8);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Key-door approach entered solid geometry.");
                }
                if (!hasKey)
                {
                    FailIf(!_dialogue.IsOpen || _rooms.InformativeTextsShown != 0x04 || rom.TextGeneration != 1 ||
                        rom.Word(0xcba2) != 0x5500 || _keyDoors.Opening || _currentRoom.GetMetatile(center) != 0x71,
                        "Missing small key must open native TX_5100 once and retain the closed door.");
                    Step(3,8); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(24,8);
                    FailIf(_dialogue.IsOpen || rom.TextGeneration != 1 || _keyDoors.Opening,
                        "Held no-key retry must not reopen the shared hint or allocate an opener.");
                    continue;
                }
                FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0 ||
                    !_rooms.TryGetNeighbor(Vector2I.Right,out int neighbor) ||
                    !_saveData.HasRoomFlag(4,0xb3,0x02) || !_saveData.HasRoomFlag(4,neighbor,0x08),
                    "Native key handoff must spend one key and mark both dungeon-layout door sides.");
                Step(); // First interleave update, six updates still remain.
                _dialogue.ShowMessage("Door pause.",120); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(5);
                FailIf(!_keyDoors.Opening || _keyDoors.OpeningCounter != 1 || !_currentRoom.IsSolid(center),
                    "Native door must retain collision through interleave update5.");
                Step(); Step(32);
                FailIf(_keyDoors.Opening || _currentRoom.IsSolid(center) ||
                    _entities.Entities<DungeonKeyUseEffect>().Count != 0,
                    "Door/key sprite must complete without another key debit.");
                Step(4); Step(4,8); Step(4,24);
                FailIf(_inventory.GetDungeonSmallKeys(5) != 0 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 2,
                    "Repeated movement through an opened door must not restart its opener.");
            }
            finally { _entities.WorldToScreen = oldScreen; _dialogue.Close(); }
        }
    }

    private void CompareKeyDoorGameplayRom(SomariaRom rom,OracleRandomState seed,
        IReadOnlyList<int> sounds,string context,bool excludeText = false,int dungeon = 5)
    {
        FailIf(_keyDoors.Opening != (rom[0xd040] != 0 && rom[0xd041] == 0x1e) ||
            _keyDoors.OpeningCounter != rom[0xd046],context + ": reserved opener allocation/clock differs.");
        var keys = _entities.Entities<DungeonKeyUseEffect>().OrderBy(_entities.InteractionSlot).ToArray();
        int[] native = Enumerable.Range(0xd2,14).Select(page => (page << 8) | 0x40)
            .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x17).ToArray();
        FailIf(keys.Length != native.Length,context + ": key sprite count differs.");
        for (int index = 0; index < keys.Length; index++)
        {
            var key = keys[index]; int slot = native[index];
            FailIf(_entities.InteractionSlot(key) != ((slot >> 8) & 15) ||
                (key.Initialized ? key.Phase + 1 : 0) != rom[slot + 4] ||
                key.Initialized && (key.Counter != rom[slot + 6] || key.Z != unchecked((sbyte)rom[slot + 0xf])) ||
                key.Position != new Vector2(rom[slot + 0xd],rom[slot + 0xb]) ||
                key.Visible != ((rom[slot + 0x1a] & 0x80) != 0),
                context + $": key sprite${slot >> 8:x2} phase/clock/position/Z/visibility differs.");
        }
        FailIf(_inventory.GetDungeonSmallKeys(dungeon) != rom[0xc672 + dungeon],context + ": small-key debit differs.");
        var random = _random.CaptureState();
        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
            random.Calls - seed.Calls != rom.RandomCalls ||
            !sounds.Where(cue => !excludeText || cue != SoundId.SndText).SequenceEqual(rom.Sounds),
            context + ": ordered key/door cues or shared RNG differs.");
    }
}

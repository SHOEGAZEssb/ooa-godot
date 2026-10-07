using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePushBlockSplashRom()
    {
        int fixture = 0;
        foreach (var spec in new (bool Water,bool Full,int Level)[] {
            (false,false,1),(true,false,1),(false,true,1),(true,true,1),
            (false,false,2),(true,false,2)})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            if (spec.Level != 0) _inventory.GiveTreasure(TreasureId.Bracelet,spec.Level);
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                LoadValidationRoom(spec.Water ? 5 : 4,spec.Water ? 0xcd : 0x6a); _entities.Clear();
                Vector2 center = spec.Water ? new(24,56) : new(184,24);
                Vector2 direction = spec.Water ? Vector2.Up : Vector2.Left;
                Vector2 start = center-direction*16+new Vector2(0.25f,0.5f);
                int angle = spec.Water ? 0 : 24, id = spec.Water ? 3 : 4;
                byte destination = spec.Water ? (byte)0xfa : (byte)0x61;
                _player.WarpTo(start); _player.Face((Vector2I)direction);
                FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(center) != 0x10 ||
                    _currentRoom.GetMetatile(center+direction*16) != destination,
                    "Block splash requires unchanged original room5:cd/4:6a geometry and a collision-free approach.");
                var seed = _random.CaptureState();
                var rom = new SomariaRom(_saveData,seed,_currentRoom,angle/8,(int)start.X,(int)start.Y);
                rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
                rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
                var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
                void Step(int count = 1,int input = 0xff) =>
                    StepSomariaMotionRom(rom,count,batch,input,afterUpdate:() => {
                        string context = $"Block splash water={spec.Water}, full={spec.Full}, L{spec.Level}, repeat={repeat}, batch={batch}, update={++update}";
                        FailIf(_pushBlocks.Active != (rom[0xd140] != 0) ||
                            _pushBlocks.RemainingPushFrames != rom[0xcc6a],context + ": reserved block/contact clock differs.");
                        var effects = _entities.Entities<SplashEffect>().ToArray();
                        int[] slots = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                            .Where(slot => rom[slot] != 0 && rom[slot+1] == id).ToArray();
                        FailIf(effects.Length != slots.Length,
                            context + $": checked native splash allocation differs: runtime={effects.Length}, native={slots.Length}.");
                        for (int i = 0; i < effects.Length; i++)
                            FailIf(_entities.InteractionSlot(effects[i]) != (slots[i]>>8)-0xd0 ||
                                effects[i].Position != new Vector2(rom[slots[i]+0xd],rom[slots[i]+0xb]) ||
                                effects[i].IsLava == spec.Water || effects[i].Visible != ((rom[slots[i]+0x1a] & 0x80) != 0) ||
                                effects[i].Initialized != (rom[slots[i]+4] != 0) ||
                                effects[i].Initialized && (effects[i].AnimationCounter != rom[slots[i]+0x20] ||
                                    effects[i].CurrentParameter != rom[slots[i]+0x21]),
                                context + ": splash physical slot/position/kind/visibility/animation differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls-seed.Calls != rom.RandomCalls ||
                            !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": ordered cues/shared RNG differ.");
                    });
                Step();
                for (int wait = 0; !_pushBlocks.Active && wait < 60; wait++) Step(input:angle);
                FailIf(!_pushBlocks.Active,$"Original water/lava block must be reachable through its collision geometry: water={spec.Water}, Link={_player.Position}, text={_dialogue.IsOpen}.");
                while (_pushBlocks.Active && rom[0xd146] > 1) Step();
                if (spec.Full)
                    for (int index = 0; index < 14; index++)
                    {
                        var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24,24),SoundId.MusNone));
                        int slot = 0xd000+_entities.InteractionSlot(puff)*256+0x40;
                        rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80;
                        rom[slot+0xb] = rom[slot+0xd] = 24;
                    }
                Step();
                FailIf(_pushBlocks.Active || _currentRoom.GetMetatile(center) != 0xa0 ||
                    _currentRoom.GetMetatile(center+direction*16) != destination ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSplash) != (spec.Full ? 0 : 1),
                    "Water/lava completion must retain terrain and delete the block even when its checked splash allocation fails.");
                _dialogue.ShowGameplayMessage("Splash continues",100); rom[0xcba0] = 1;
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                // Declare DISABLE_INTERACTIONS $02 after the real block/effect
                // handoff. The initialized splash's enabled bit$80 bypasses it.
                var freeze = new CrownEntranceFreeze();
                _entities.AddEntity(freeze); rom[0xcc8a] = 2;
                Step(3); freeze.FreezesRoomEntities = false; rom[0xcc8a] = 0;
                Step(36); Step(4,angle);
                FailIf(_entities.Entities<SplashEffect>().Count != 0 || _pushBlocks.Active ||
                    sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSplash) != (spec.Full ? 0 : 1),
                    "Splash must finish once and renewed contact must not recreate the block/effect.");
            }
        }
    }
}

using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorScroll()
    {
        int fixture = 0;
        foreach (int phase in new[] { 0,1,2 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xb3); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            Vector2 center = new(232,136),start = new(phase == 0 ? 220.25f : 204.25f,136.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_currentRoom.IsSolid(start),"Outgoing door requires original room4:b3 adjacent floor.");
            if (phase == 0)
            {
                // Declared boundary before the object pass. Reachable full-loop
                // allocation is exercised by the two initialized cases below.
                _keyDoors.UpdatePushAttempt(start,Vector2I.Right,Vector2.Zero);
                for (int contact = 0; contact < 10; contact++)
                    _keyDoors.UpdatePushAttempt(start,Vector2I.Right,Vector2.Right);
            }
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,(int)start.X,(int)start.Y);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom.CreateMenuView().LoadDungeon(5);
            if (phase == 0)
            {
                // nextToKeyDoor's original post-allocation bytes. Subid$71
                // is the source tile, transformed to small-key graphic in state0.
                rom[0xd040] = 1; rom[0xd041] = 0x1e; rom[0xd04b] = 0x8e; rom[0xd049] = 2;
                rom[0xd240] = 1; rom[0xd241] = 0x17; rom[0xd242] = 0x71;
                rom[0xd24b] = 136; rom[0xd24d] = 232;
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Approach(int count = 1,int angle = 0xff) =>
                StepSomariaMotionRom(rom,count,batch,angle,afterUpdate:() =>
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,$"Outgoing door phase{phase}, approach{++update}"));
            if (phase != 0)
            {
                Approach();
                for (int wait = 0; !_keyDoors.Opening && wait < 64; wait++) Approach(angle:8);
                if (phase == 2) Approach();
            }
            FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0,
                "Outgoing door must retain exactly one real key debit and its allocated state.");
            var source = _currentRoom;
            int counter = _keyDoors.OpeningCounter;
            // Supply another owner's collision-clear boundary; this isolates
            // outgoing lifetime, rather than claiming passage through a shut door.
            source.SetPositionTileAndCollision(center,0xa0,null,(long)_animationTicks);
            rom[0xcf8e] = 0xa0; rom[0xce8e] = 0;
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right,out int target),"Door scroll requires its imported dungeon neighbor.");
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(source.TilesetId).Unique |
                (source.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            _transitions.BeginScroll(_player,Vector2I.Right,target);
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            Vector2 scrollStart = _player.PrecisePosition;
            var scroll = new ScrollRom(true,1,(int)(scrollStart.X*256),(int)(scrollStart.Y*256),unique,sourceUnique);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8;
            FailIf(rom[0xd040] != 2 || rom[0xd240] != 2,"Native scroll marking must retain both outgoing allocations as enabled02.");
            int runtimeSoundStart = sounds.Requests.Count,nativeSoundStart = rom.Sounds.Count;
            // Incoming allocation/RNG and destination room bytes have their
            // own preload comparisons. This composition executes native scroll
            // control and the original outgoing interaction dispatcher/cleanup.
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool frozen = scroll[0xcd04] != 2;
                if (frozen)
                {
                    rom.AdvanceInteractions(_entities.FrameCounter);
                    scroll.Update();
                    if (scroll[0xcd04] == 2) rom.ClearOutgoingInteractions();
                }
                string context = $"Outgoing door phase{phase}, batch={batch}, update{++update}";
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) ||
                    _keyDoors.Opening != (rom[0xd040] != 0) || _keyDoors.OpeningCounter != rom[0xd046],
                    context+": scroll completion/reserved lifetime/counter differs.");
                Vector2 actual = new(((int)(_player.PrecisePosition.X*256)&0xffff)/256f,
                    ((int)(_player.PrecisePosition.Y*256)&0xffff)/256f);
                if (frozen) FailIf(actual != new Vector2(scroll.Word(0xd00c)/256f,scroll.Word(0xd00a)/256f),
                    context+": scroll Link fixed coordinates differ.");
                var keys = _entities.OutgoingEntities<DungeonKeyUseEffect>();
                FailIf(keys.Count != (rom[0xd240] == 0 ? 0 : 1),context+": outgoing key lifetime differs.");
                if (keys.Count != 0)
                    FailIf(keys[0].Initialized != (rom[0xd244] != 0) || keys[0].Counter != rom[0xd246] ||
                        keys[0].Z != unchecked((sbyte)rom[0xd24f]) || keys[0].Visible != ((rom[0xd25a]&0x80) != 0),
                        context+": outgoing key initialization/frozen state differs.");
                bool Relevant(int cue) => cue is SoundId.SndDoorClose or SoundId.SndGetSeed;
                FailIf(!sounds.Requests.Skip(runtimeSoundStart).Where(Relevant).SequenceEqual(
                    rom.Sounds.Skip(nativeSoundStart).Where(Relevant)),context+": outgoing door/key cue phase differs.");
            });
            Step();
            FailIf(_keyDoors.Opening != (phase != 0) || phase != 0 && _keyDoors.OpeningCounter != counter,
                "First scroll pass must delete pending state0 and retain initialized reserved doors.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || _keyDoors.Opening || _rooms.CurrentRoom.Id != target ||
                source.GetMetatile(center) != 0xa0,"Native scroll cleanup must release outgoing allocations without a delayed source write.");
            Step(3);
            FailIf(_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0,
                "Post-scroll gameplay must retain the key debit and leave no outgoing opener.");
        }
    }
}

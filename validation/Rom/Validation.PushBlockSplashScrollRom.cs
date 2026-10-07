using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePushBlockSplashScrollRom()
    {
        int fixture = 0;
        foreach (bool water in new[] { true,false })
        foreach (bool initialized in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x34); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(6.75f,88.25f);
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(start) || _currentRoom.GetMetatile(new(8,88)) != 0x24,
                "Splash scroll must exit through unchanged room0:34/$50 floor.");
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,6,88);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // Declare an allocated effect; actual block/hazard creation has
            // its original-room gameplay cases. This fixture isolates its
            // shared outgoing owner across a real collision-reached exit.
            var effect = _entities.Spawn<SplashEffect>(new EnemySplashSpawn(new(24.25f,88.5f),
                water ? HazardType.Water : HazardType.Lava));
            int slot = 0xd000+_entities.InteractionSlot(effect)*256+0x40;
            rom[slot] = 1; rom[slot+1] = water ? (byte)3 : (byte)4;
            rom[slot+0xb] = 88; rom[slot+0xd] = 24;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Compare()
            {
                string context = $"Splash scroll water={water}, initialized={initialized}, batch={batch}, update={++update}";
                bool alive = rom[slot] != 0;
                FailIf(effect.Finished == alive,context+": outgoing splash lifetime differs.");
                if (alive)
                    FailIf(effect.Initialized != (rom[slot+4] != 0) ||
                        effect.Visible != ((rom[slot+0x1a]&0x80) != 0) ||
                        effect.Initialized && (effect.AnimationCounter != rom[slot+0x20] ||
                            effect.CurrentParameter != rom[slot+0x21]),
                        context+": outgoing splash state/visibility/animation differs.");
                FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSplash) !=
                    rom.Sounds.Count(cue => cue == SoundId.SndSplash),context+": splash cue phase differs.");
            }
            if (initialized)
                StepSomariaMotionRom(rom,1,batch,afterUpdate:Compare);
            StepGameplayUpdates(1,Vector2.Left,batched:batch);
            rom.UpdateGameplay(0,0x20,24,_entities.FrameCounter);
            FailIf(!_transitions.ScrollActive || _rooms.CurrentRoom.Id != 0x33,
                "Unchanged original room0:34 left floor must reach its actual neighbor0:33.");
            Compare();
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(false,3,(int)(_player.PrecisePosition.X*256),
                (int)(_player.PrecisePosition.Y*256),unique,sourceUnique);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8;
            FailIf(rom[slot] != 0x82,"Native scroll marking must preserve the initialized effect's always-update bit.");
            StepGameplayUpdates(_transitions.ScrollTotalFrames,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceInteractions(_entities.FrameCounter); scroll.Update();
                if (scroll[0xcd04] == 2) rom.ClearOutgoingInteractions();
                Compare();
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2),"Splash and native scroll must complete in the same phase.");
            });
            FailIf(!effect.Finished || _entities.OutgoingEntities<SplashEffect>().Count != 0 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSplash) != 1,
                "Always-updating splash must finish before outgoing cleanup without a duplicate cue.");
            StepGameplayUpdates(3,Vector2.Zero,batched:batch);
            FailIf(_entities.Entities<SplashEffect>().Count != 0 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSplash) != 1,
                "Arrival must not resurrect or reinitialize the completed outgoing splash.");
        }
    }
}

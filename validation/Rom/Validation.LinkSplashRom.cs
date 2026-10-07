using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareSideViewSplashCoordinatesRom()
    {
        int fixture = 0;
        // Stay inside screenTransitionState2's clamped room bounds. The
        // upper edge selects room$7:$05's warp after objects; this bounded
        // Link/interaction wrapper does not execute that outer warp dispatch.
        foreach (int y in new[] { 8,56 })
        foreach (bool fullPool in new[] { false,true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(7,0x05); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Flippers,0); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(56.25f,y+0.5f)); _player.Face(Vector2I.Right);
            var seed = _random.CaptureState();
            FailIf(_player.PrecisePosition != new Vector2(56.25f,y+0.5f), "Side-view splash fixture must preserve declared input fractions.");
            var rom = new SideViewRom(_saveData,seed,_currentRoom,_player.PrecisePosition,_entities.FrameCounter); rom[0xd008] = 1;
            if (fullPool) for (int index = 0; index < 14; index++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                int slot = (0xd2+index)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80; rom[slot+0xb] = 144; rom[slot+0xd] = 224;
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.Update(); CompareSideViewRom(rom,$"Side-view splash y={y}, full={fullPool}, update{++update}");
                var effects = _entities.Entities<SplashEffect>().OrderBy(effect => _entities.InteractionSlot(effect)).ToArray();
                int[] slots = Enumerable.Range(0xd2,14).Select(page => page*256+0x40).Where(slot => rom[slot] != 0 && rom[slot+1] == 3).ToArray();
                FailIf(effects.Length != slots.Length,"Side-view entry must use checked physical splash capacity.");
                for (int index = 0; index < effects.Length; index++)
                    FailIf(_entities.InteractionSlot(effects[index]) != (slots[index]>>8)-0xd0 ||
                        effects[index].Position != new Vector2(rom[slots[index]+0xd],rom[slots[index]+0xb]) ||
                        effects[index].Position != new Vector2(56,(y-3)&255) ||
                        effects[index].AnimationCounter != rom[slots[index]+0x20] || effects[index].CurrentParameter != rom[slots[index]+0x21],
                        $"Side-view splash y={y}: native $fd00 offset must discard copied coordinate subpixels; runtime={effects[index].Position}, native={rom[slots[index]+0xd]},{rom[slots[index]+0xb]}.");
                FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),"Side-view splash cue order differs.");
            });
            Step();
            FailIf(!_player.SideScrollSwimming || _entities.Entities<SplashEffect>().Count != (fullPool ? 0 : 1),
                "Original room$7:$05's water must admit one pending entry request at the copied YH coordinate.");
            _dialogue.ShowMessage("Declared side-view splash pause.",100); rom[0xcba0] = 1;
            try { Step(16); }
            finally { _dialogue.Close(); rom[0xcba0] = 0; }
            FailIf(_entities.Entities<SplashEffect>().Count != 0,"Side-view splash must complete its native lifetime under text without retrying a failed creation.");
        }
    }

    private void CompareLinkSplashAllocationRom()
    {
        int fixture = 0;
        foreach (bool lava in new[] { false,true })
        foreach (bool fullPool in new[] { false,true })
        foreach (int pause in new[] { 0,1,2 })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x33); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8),0x2c,0,0);
            _currentRoom.SetPositionTileAndCollision(new(88,72),lava ? (byte)0xe4 : (byte)0xfa,0x10,0);
            FailIf(_currentRoom.GetTerrainInfo(new(88,72)).Hazard != (lava ? HazardType.Lava : HazardType.Water),
                "The declared outdoor $fa/$e4 native hazard tile must retain its water/lava classification.");
            _player.WarpTo(new(88.25f,55.5f)); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,88,55);
            rom.Word(0xd00a,55*256+128); rom.Word(0xd00c,88*256+64);
            rom[0xcc21] = 55; rom[0xcc22] = 88; rom[0xcc23] = 2;
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (fullPool) for (int index = 0; index < 14; index++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                int slot = (0xd2+index)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80; rom[slot+0xb] = 144; rom[slot+0xd] = 224;
            }
            var sounds = _sound.AttachPlayRequestAudit(); var disabled = _entities.InitializedObjectsDisabledSource;
            int update = 0;
            void Step(int count = 1,int angle = 0xff) => StepSomariaMotionRom(rom,count,batched,angle,afterUpdate:() =>
            {
                ComparePhysicalSplashesRom(rom,$"Link splash lava={lava}, full={fullPool}, pause={pause}, update{++update}");
                FailIf(!sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    "Link splash cue must originate once in its physical state0, or remain absent after failed checked allocation.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Link splash allocation/lifetime must preserve shared RNG.");
            });
            try
            {
                for (int walk = 0; !_player.IsDrowning && walk < 16; walk++) Step(1,16);
                FailIf(!_player.IsDrowning || _entities.Entities<SplashEffect>().Count != (fullPool ? 0 : 1) ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSplash) != (fullPool ? 0 : 1),
                    $"Link entry lava={lava}, full={fullPool}, pause={pause}, XY={_player.Position}, drowning={_player.IsDrowning}, swimming={_player.TopDownSwimming}, native state=${rom[0xd004]:x2}, swim=${rom[0xcc5d]:x2}, counter={rom[0xd007]}, effects={_entities.Entities<SplashEffect>().Count}, cues=[{string.Join(',',sounds.Requests)}]: one checked splash request before the interaction pass.");
                if (pause == 1) { _dialogue.ShowMessage("Declared splash lifetime.",100); rom[0xcba0] = 1; }
                if (pause == 2) { _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2; }
                Step(28);
                FailIf(_entities.Entities<SplashEffect>().Count != 0 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSplash) != (fullPool ? 0 : 1),
                    "Splash enabled$80 must advance under text/masks and delete after its retained terminal frame, without retrying failed creation.");
                _dialogue.Close(); rom[0xcba0] = 0; _entities.InitializedObjectsDisabledSource = disabled; rom[0xcc8a] = 0;
                Step(3);
            }
            finally { _dialogue.Close(); _entities.InitializedObjectsDisabledSource = disabled; }
        }
    }

    private void ComparePhysicalSplashesRom(SomariaRom rom, string context)
    {
        var effects = _entities.Entities<SplashEffect>().OrderBy(effect => _entities.InteractionSlot(effect)).ToArray();
        int[] slots = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
            .Where(slot => rom[slot] != 0 && rom[slot+1] is 3 or 4).ToArray();
        FailIf(effects.Length != slots.Length,$"{context}: physical splash count={effects.Length}/{slots.Length}.");
        for (int index = 0; index < effects.Length; index++)
        {
            var effect = effects[index]; int slot = slots[index];
            FailIf(_entities.InteractionSlot(effect) != (slot>>8)-0xd0 ||
                effect.Position != new Vector2(rom[slot+0xd],rom[slot+0xb]) || effect.IsLava != (rom[slot+1] == 4) ||
                effect.Initialized != (rom[slot+4] != 0) || effect.Visible != ((rom[slot+0x1a]&0x80) != 0) ||
                effect.Initialized && (effect.AnimationCounter != rom[slot+0x20] || effect.CurrentParameter != rom[slot+0x21] ||
                    effect.ZIndex != ObjectDrawPriority.FromVisible(rom[slot+0x1a])),
                $"{context}: physical splash slot/XY/kind/visibility/priority/counter/parameter differs at${slot:x4}.");
        }
    }
}

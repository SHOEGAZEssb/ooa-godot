using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateClinkNativeTiming()
    {
        foreach (bool batch in new[] { false, true })
        foreach (string mode in new[] { "normal", "text", "freeze", "scroll" })
        {
            LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(24,24));
            var even = _entities.Spawn<ClinkEffect>(new SwordBeamClinkSpawn(new(24,24)));
            var odd = _entities.Spawn<ClinkEffect>(new SwordBeamClinkSpawn(new(40,24)));
            FailIf(_entities.InteractionSlot(even) != 2 || _entities.InteractionSlot(odd) != 3,
                "Clinks must occupy native slots$d2/$d3.");
            var freeze = _entities.NonInteractionObjectsDisabledSource;
            var text = _entities.TextActiveSource;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            try
            {
                if (mode == "text") _entities.TextActiveSource = () => true;
                if (mode == "freeze") _entities.NonInteractionObjectsDisabledSource = () => true;
                if (mode == "scroll") _entities.BeginScreenTransition(4, _currentRoom, new(240,0), _player);
                Step();
                FailIf(!even.Visible || !odd.Visible || even.AnimationFrame != 0 || odd.ElapsedFrames != 1,
                    "Clink state0 must show both parities without advancing animation.");
                Step(3);
                FailIf(even.AnimationFrame != 0 || even.EffectVisible != ((_entities.FrameCounter & 1) == 0) ||
                    odd.EffectVisible == even.EffectVisible,
                    "Clink flicker must use global frame XOR page during native always-update dispatch.");
                Step();
                FailIf(even.AnimationFrame != 1, "Clink's fourth animation update selects its second frame.");
                Step(4);
                FailIf(even.Finished || odd.Finished || even.ElapsedFrames != 9,
                    "Clinks must retain the terminal animation parameter on update9.");
                Step();
                FailIf(!even.Finished || !odd.Finished, "Clinks must delete on update10.");
            }
            finally
            {
                _entities.NonInteractionObjectsDisabledSource = freeze;
                _entities.TextActiveSource = text;
                if (mode == "scroll") _entities.FinishScreenTransition();
            }
        }
        foreach (bool batch in new[] { false, true })
        foreach (bool bombable in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        {
            LoadValidationRoom(0, 0x69); _entities.Clear(); _player.WarpTo(new(24,70));
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWORD_00"));
            _currentRoom.SetPositionTileAndCollision(new(24,56), bombable ? (byte)0xc1 : (byte)0x3a, 0x0f, 0);
            for (int i = 0; i < 14 - free; i++)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(104,104),SoundId.MusNone));
            _sound.ClearPlayRequestAudit();
            _combat.ApplySwordTileHit(_player, 0, swordPoke: true);
            FailIf(_entities.Entities<ClinkEffect>().Count != free ||
                _sound.PlayRequestsFor(SoundId.SndClink) != 0 ||
                _sound.PlayRequestsFor(SoundId.SndClink2) != (bombable ? 1 : 0),
                "Sword-wall allocation must skip a full pool; only bombable sound precedes allocation.");
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            Step(1);
            FailIf(_sound.PlayRequestsFor(SoundId.SndClink) != (bombable ? 0 : free),
                "Ordinary wall sound requires successful allocation and a later state0 update.");
            Step(21);
            FailIf(_entities.Entities<ClinkEffect>().Count != 0,
                "Failed wall-clink allocation must not retry after the pool clears.");
            _combat.ApplySwordTileHit(_player, 0, swordPoke: true);
            FailIf(_entities.Entities<ClinkEffect>().Count != 1 ||
                _entities.InteractionSlot(_entities.Entities<ClinkEffect>()[0]) != 2,
                "Repeating wall contact must reuse the first released native interaction slot.");
            Step(10);
            FailIf(_entities.Entities<ClinkEffect>().Count != 0,
                "A repeated wall clink must use the shared ten-update lifetime.");
        }
        LoadValidationRoom(0, 0x60);
    }
}

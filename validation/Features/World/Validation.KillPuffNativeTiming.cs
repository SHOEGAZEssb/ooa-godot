using Godot;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateKillPuffNativeTiming()
    {
        foreach (bool batch in new[] { false, true })
        foreach (string mode in new[] { "normal", "text", "freeze", "scroll" })
        {
            LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(24,24));
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy);
            var puff = _entities.Spawn<KillEnemyPuffEffect>(new KillEnemyPuffSpawn(new(24,24)));
            FailIf(puff.Visible || puff.Initialized || _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != sounds,
                "INTERAC$08 must wait for state0 before showing or playing its sound.");
            var freeze = _entities.NonInteractionObjectsDisabledSource;
            var text = _entities.TextActiveSource;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batch);
            try
            {
                if (mode == "text") _entities.TextActiveSource = () => true;
                if (mode == "freeze") _entities.NonInteractionObjectsDisabledSource = () => true;
                if (mode == "scroll") _entities.BeginScreenTransition(4, _currentRoom, new(240,0), _player);
                Step();
                FailIf(!puff.Visible || !puff.Initialized || puff.AnimationFrame != 0 ||
                    _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != sounds + 1,
                    "Kill-puff state0 must show frame0 and play its sound once.");
                Step();
                FailIf(puff.AnimationFrame != 0, "Kill-puff frame0 must last two animation updates.");
                Step();
                FailIf(puff.AnimationFrame != 1, "Kill-puff must select frame1 on update3.");
                Step(18);
                FailIf(puff.Finished || puff.AnimationFrame != 6 || puff.ElapsedFrames != 21,
                    "Kill-puff must retain its terminal frame on update21.");
                Step();
                FailIf(!puff.Finished || _sound.PlayRequestsFor(OracleSoundEngine.SndKillEnemy) != sounds + 1,
                    "Kill-puff must delete on update22 without replaying its initialization sound.");
            }
            finally
            {
                _entities.NonInteractionObjectsDisabledSource = freeze;
                _entities.TextActiveSource = text;
                if (mode == "scroll") _entities.FinishScreenTransition();
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}

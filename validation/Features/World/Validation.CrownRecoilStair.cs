using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownRecoilStair()
    {
        // cutscene01 calls func_60e9 after Link's recoil. Explicit warps only
        // request state$0a, which state03 never consumes. Dungeon floor
        // fallbacks instead write wWarpTransition2 directly in bank4.
        foreach (bool batched in new[] { false, true })
        foreach (bool warpDisabled in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Recoil/stair fixture must use Crown4:a1's actual southern approach.");
            StepGameplayUpdates(10, Vector2.Up, batched: batched);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 30),
                "Ordinary movement must stop before the stair's activation window.");
            FailIf(!_player.ApplyEnemyContactDamage(_player.Position + new Vector2(0, 12),
                lethal ? _player.HealthQuarters : 1),
                "The isolated collision result must begin northward recoil.");
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, warpDisabled ? (byte)1 : (byte)0);
            bool observedRecoil = false;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                if (_player.Position.Y <= 25)
                    observedRecoil |= _player.KnockbackFrames > 0;
            });
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            int sounds = _sound.PlayRequestsFor(SoundId.SndEnterCave);
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || _player.Position.Y != 26,
                "The first three SPEED_140 recoil updates must remain outside the stair bound.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!observedRecoil || IsTransitioning == warpDisabled,
                $"Grounded recoil must select the stair at its first accepted position and respect wWarpsDisabled (lethal={lethal}).");
            if (warpDisabled)
            {
                _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
                StepGameplayUpdates(1, Vector2.Zero);
            }
            FailIf(!IsTransitioning || _transitions.AwaitingLinkWarpState ||
                _sound.PlayRequestsFor(SoundId.SndEnterCave) != sounds + 1,
                "Dungeon floor fallback must dispatch its direct fade even during lethal recoil.");
            if (lethal)
                ValidateDeathFloorStair(batched);
        }

        foreach (bool batched in new[] { false, true })
        foreach (int room in new[] { 0xa1, 0xa3 })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, room);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 1);
            _player.WarpTo(new(120, 40));
            StepGameplayUpdates(15, Vector2.Up, batched: batched);
            FailIf(_player.Position != new Vector2(120, 25) || IsTransitioning,
                "The spinning-death case must approach the disabled stair through actual floor geometry.");
            FailIf(!_player.ApplyDamage(_player.HealthQuarters), "The isolated damage result must be lethal.");
            StepGameplayUpdates(10, Vector2.Zero, batched: batched);
            FailIf(!_player.DeathAnimationActive, "Death must be spinning before releasing the warp gate.");
            int sounds = _sound.PlayRequestsFor(SoundId.SndEnterCave);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled, 0);
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!IsTransitioning || _transitions.AwaitingLinkWarpState != (room == 0xa3),
                "An active death spin must distinguish the explicit warp request from the floor fallback's direct fade.");
            if (room == 0xa3)
                ValidatePendingDeathWarp(batched, sounds);
            else
                ValidateDeathFloorStair(batched);
        }
        ReinitializeGameplayForValidation();
    }

    private void ValidatePendingDeathWarp(bool batched, int entranceSounds)
    {
        int ordinaryUpdates = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        // Isolated dispatch probes stand in for already initialized handlers.
        // One native ENEMY and one ordinary interaction must both be skipped.
        var enemy = new ItemPhaseValidationEntity(() => ordinaryUpdates++);
        var interaction = new ItemPhaseValidationEntity(() => ordinaryUpdates++);
        typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [enemy, 0]);
        typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [enemy]);
        typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [interaction]);
        var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200, 104), SoundId.MusNone));
        var drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new(200, 104)));
        int dropZ = drop.ZFixed, dropSpeed = drop.SpeedZ, dropCounter = drop.Counter;
        // updateItems still initializes state0 under mask$1e, then freezes
        // state1. ITEM$06 has a nil post handler, so clearing Link's parent
        // does not delete or advance the independent child here.
        Vector2 boomerangStart = new(200.25f, 104.5f);
        var boomerang = _entities.Spawn<BoomerangItem>(new BoomerangSpawn(boomerangStart, ObjectAngle.Right));
        int slowFades = _sound.PlayRequestsFor(SoundId.SndCtrlSlowFadeOut);
        int gameOverRequests = 0;
        void ObserveGameOver() => gameOverRequests++;
        _player.GameOverRequested += ObserveGameOver;
        try
        {
            StepGameplayUpdates(3, Vector2.Zero, batched: batched);
            FailIf(ordinaryUpdates != 0 || puff.ElapsedUpdates != 3 || _dialogue.IsOpen ||
                drop.ZFixed != dropZ || drop.SpeedZ != dropSpeed || drop.Counter != dropCounter ||
                boomerang.State != 1 || boomerang.Counter != 40 || boomerang.PrecisePosition != boomerangStart,
                "Pending warp mask$1e must freeze initialized handlers while permitting puff initialization and enabled-bit7 updates without opening text.");
            // Longer than an ordinary 32+32 stair fade, but shorter than the
            // four spin loops plus $4c collapsed updates of source state03.
            StepGameplayUpdates(61, Vector2.Zero, batched: batched);
            FailIf(_currentRoom.Id != 0xa3 || !_transitions.AwaitingLinkWarpState ||
                !_player.DeathAnimationActive || gameOverRequests != 0 ||
                boomerang.Finished || boomerang.Counter != 40 || boomerang.PrecisePosition != boomerangStart ||
                _sound.PlayRequestsFor(SoundId.SndEnterCave) != entranceSounds,
                "Pending lethal warp must stay in the source room while its death animation advances.");
            for (int update = 0; update < 200 && gameOverRequests == 0; update++)
                StepGameplayUpdates(1, Vector2.Zero);
            FailIf(gameOverRequests != 1 || _currentRoom.Id != 0xa3 ||
                _sound.PlayRequestsFor(SoundId.SndCtrlSlowFadeOut) != slowFades ||
                _sound.PlayRequestsFor(SoundId.SndEnterCave) != entranceSounds,
                "Death must reach game over in the source room without starting the selected stair warp.");
        }
        finally
        {
            _player.GameOverRequested -= ObserveGameOver;
        }
    }

    private void ValidateDeathFloorStair(bool batched)
    {
        // bank4.findWarpSourceAndDest @warpSourceNotFound writes transition2
        // directly, unlike an explicit warp. cutscene03 then owns the fade.
        int deadSounds = _sound.PlayRequestsFor(SoundId.SndLinkDead);
        int slowFades = _sound.PlayRequestsFor(SoundId.SndCtrlSlowFadeOut);
        Vector2 departure = _player.Position;
        float recoil = _player.KnockbackFrames;
        bool spinning = _player.DeathAnimationActive;
        int frame = _player.DeathAnimationFrame;
        int counter = _player.DeathAnimationCounter;
        StepGameplayUpdates(31, Vector2.Zero, batched: batched);
        FailIf(!IsTransitioning || _currentRoom.Id != 0xa1 ||
            _player.Position != departure || _player.KnockbackFrames != recoil ||
            _player.DeathAnimationActive != spinning || _player.DeathAnimationFrame != frame ||
            _player.DeathAnimationCounter != counter,
            "The direct source fade must freeze lethal recoil and any active death animation.");
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!IsTransitioning || _currentRoom.Id != 0xb1 ||
            _player.KnockbackFrames != 0 || !_player.IsDying || _player.DeathAnimationActive,
            "Full loading must clear native recoil while retaining wLinkDeathTrigger.");
        StepGameplayUpdates(32, Vector2.Zero, batched: batched);
        FailIf(IsTransitioning || _player.DeathAnimationActive,
            "Destination palette updates must finish before death resumes.");
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(!_player.DeathAnimationActive || _player.DeathAnimationFrame != 2 ||
            _player.DeathAnimationCounter != 8 || _player.DeathSpinLoopsRemaining != 4 ||
            _sound.PlayRequestsFor(SoundId.SndLinkDead) != deadSounds + 1 ||
            _sound.PlayRequestsFor(SoundId.SndCtrlSlowFadeOut) != slowFades,
            "Death must start from its initial frame after arrival without replaying the global slow fade.");
    }
}

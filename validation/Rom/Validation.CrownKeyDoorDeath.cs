using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownKeyDoorDeath()
    {
        foreach (bool beforeInitialization in new[] { true,false })
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0xb3); _entities.Clear();
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SMALL_KEY_03"));
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(204.25f,136.5f),center = new(232,136);
            _player.WarpTo(start); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(start),"Door death fixture must approach through original room4:b3 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,204,136);
            rom.Word(0xd00a,(int)(start.Y*256)); rom.Word(0xd00c,(int)(start.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom.CreateMenuView().LoadDungeon(5);
            var oldScreen = _entities.WorldToScreen;
            _entities.WorldToScreen = position => position-new Vector2(80,48);
            rom[0xffaa] = 48; rom[0xffac] = 80;
            var sounds = _sound.AttachPlayRequestAudit();
            bool runtimeHit = false,nativeHit = false; int update = 0;
            void RuntimeHit()
            {
                FailIf(!_player.ApplyDamage(_player.HealthQuarters) || !_player.IsDying,
                    "Door fixture lethal damage must publish pending death.");
                runtimeHit = true;
            }
            void NativeHit()
            {
                rom.ApplyLinkDamage(unchecked((byte)(-2*rom[0xc6aa])));
                // Resume the native dying handler selected after linkApplyDamage,
                // avoiding a second health check/damage call through state01.
                rom[0xd004] = 3; nativeHit = true;
            }
            if (beforeInitialization)
            {
                var observer = new ItemPhaseValidationEntity(() => {
                    if (!runtimeHit && _keyDoors.Opening) RuntimeHit();
                });
                _entities.AddEntity(observer); _entities.RegisterEnemySlot(observer,0);
            }
            void Step(int count = 1,int angle = 0xff)
            {
                StepGameplayUpdates(count,angle == 8 ? Vector2.Right : Vector2.Zero,batched:batch,afterUpdate:() => {
                    rom.AdvanceDeathPrelude();
                    rom.UpdateGameplay(0,angle == 8 ? 0x10 : 0,angle,_entities.FrameCounter,() => {
                        if (beforeInitialization && !nativeHit && rom[0xd040] != 0)
                        {
                            FailIf(rom[0xd044] != 0,"Lethal publication must precede reserved door state0.");
                            NativeHit();
                        }
                    });
                    string context = $"Door death before-init={beforeInitialization}, batch={batch}, update={++update}";
                    CompareKeyDoorGameplayRom(rom,seed,sounds.Requests,context);
                    FailIf(runtimeHit != nativeHit || _player.HealthQuarters != rom[0xc6aa] ||
                        _player.IsDying != (rom[0xcdd5] != 0) ||
                        _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                        _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1),
                        context+": lethal publication/health/fixed Link position/death handler differs.");
                    if (_player.DeathAnimationActive)
                        FailIf(_player.DeathAnimationCounter != rom[0xd020] ||
                            _player.DeathAnimationFrame != rom[0xd031] || _player.DeathSpinLoopsRemaining != rom[0xd006],
                            context+": death animation/counter/loop boundary differs.");
                    for (int y = 0; y < _currentRoom.HeightInTiles; y++)
                    for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                    {
                        int packed = y*16+x; Vector2 point = new(x*16+8,y*16+8);
                        FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+packed] ||
                            _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed] ||
                            _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(packed),
                            context+$": room buffers differ at${packed:x2}.");
                    }
                });
            }
            try
            {
                Step();
                for (int wait = 0; !_keyDoors.Opening && wait < 64; wait++) Step(angle:8);
                FailIf(!_keyDoors.Opening || _inventory.GetDungeonSmallKeys(5) != 0,
                    "Reachable death handoff must retain the allocated opener and exactly one key debit.");
                if (!beforeInitialization) { RuntimeHit(); NativeHit(); }
                Step(7); Step(3);
                FailIf(!_player.DeathAnimationActive || !_keyDoors.Opening || _keyDoors.OpeningCounter != 0 ||
                    _currentRoom.IsSolid(center) != beforeInitialization ||
                    sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != (beforeInitialization ? 0 : 2),
                    "Death must hold incstate before animation or scriptend after its exact six-update completion.");
                Step(32); Step(4,8);
                FailIf(!_keyDoors.Opening || _entities.Entities<DungeonKeyUseEffect>().Count != 0 ||
                    _inventory.GetDungeonSmallKeys(5) != 0 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndCtrlSlowFadeOut) != 1,
                    "Pending door must retain its script gate/debit while the key retires and death consumes renewed input.");
            }
            finally { _entities.WorldToScreen = oldScreen; }
        }
    }
}

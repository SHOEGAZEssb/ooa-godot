using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogEntry()
    {
        var signal = new BossShutterSignal();
        for (int value = 0; value < 256; value++)
        {
            signal.Clear();
            for (int i = 0; i < value; i++) signal.Opened();
            signal.Closed();
            int expected = value == 0 ? 0 : value - 1;
            if ((expected & 0x7f) == 0) expected &= 0x7f;
            FailIf(signal.Value != expected, $"doorController closing signal${value:x2} must preserve native byte decrement/bit7 clearing.");
            signal.Clear();
            for (int i = 0; i < value; i++) signal.Opened();
            signal.BeginBossEntry(); signal.BeginBossEntry();
            FailIf(signal.Value != (value | 0x80), "Boss initialization must set bit7 without replacing the low shutter count.");
        }
        signal.Clear();
        for (int i = 0; i < 256; i++) signal.Opened();
        FailIf(signal.Value != 0, "doorController open increments must retain byte wrapping.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var forced = typeof(Player).GetField("_forcedRoomEntryMovement",flags)!;
        LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(40,40));
        var entry = new BossEntryMovement(Vector2I.Right);
        // Normal Link consumes the force-state request without dispatching.
        // The next update enters state0b, which falls through to movement:
        // handler updates1-21 move,22 releases.
        for (int cycle = 0; cycle < 2; cycle++)
        {
            Vector2 start = _player.Position; entry.Arm();
            FailIf(!_player.NativeNormalStateForInteraction,
                "Arming boss entry must leave Link in state01 until the request is consumed.");
            entry.Update(_player);
            FailIf(_player.Position != start || !(bool)forced.GetValue(_player)! ||
                _player.NativeNormalStateForInteraction,
                "Force-state request consumption must retain position before the state0b handler runs.");
            for (int tick = 1; tick <= 21; tick++)
            {
                entry.Update(_player);
                FailIf(_player.Position != start + new Vector2(tick,0) || !(bool)forced.GetValue(_player)! ||
                    _player.NativeNormalStateForInteraction,
                    $"Boss entry handler update{tick} must move, including initialization and re-arming.");
            }
            entry.Update(_player);
            FailIf(_player.Position != start + new Vector2(21,0) || (bool)forced.GetValue(_player)! ||
                !_player.NativeNormalStateForInteraction,
                "Boss entry update22 must release without an extra movement.");
            entry.Update(_player);
            FailIf(_player.Position != start + new Vector2(21,0), "Completed forced entry must remain inert.");
        }
        void Step(Vector2 movement = default) =>
            StepGameplayUpdates(1, movement, [], [], batched: true);
        _saveData.SetRoomFlag(4,0xbf,0x80,false);
        // Isolate entry from Boss Key acquisition/opening; preserve the real
        // door's already-open flags on both sides and its actual geometry.
        _saveData.SetRoomFlag(4,0xbe,0x02,true);
        _saveData.SetRoomFlag(4,0xbf,0x08,true);
        LoadValidationRoom(4,0xbe); _player.WarpTo(new(200,88));
        FailIf(_currentRoom.IsSolid(_player.Position), "$4:$be Smog approach must start on actual floor.");
        for (int i = 0; !IsTransitioning && i < 80; i++) Step(Vector2.Right);
        FailIf(!IsTransitioning, "$4:$be east doorway must reach Smog's room through the imported dungeon adjacency.");
        int preloaded = 0;
        for (int i = 0; IsTransitioning && i < 160; i++)
        {
            var actors = _entities.Entities<SmogCharacter>();
            if (actors.Count != 0)
            {
                var sentinel = actors.Single();
                FailIf(sentinel.State != 8 || sentinel.SubId != 5 || sentinel.Visible || sentinel.CollisionEnabled ||
                    sentinel.Position != new Vector2(120,88) || sentinel.Speed != 8 || _entities.RoomEnemyCount != 1,
                    "Smog scroll preload must initialize one hidden sentinel with east-entry speed$08 and freeze its state/count.");
                FailIf(_entities.BossEntrySignal != 0x80, "Smog preload must set cc93 bit7 before destination shutters begin updating.");
                preloaded++;
            }
            Step();
        }
        FailIf(IsTransitioning || _currentRoom.Id != 0xbf || preloaded == 0,
            "Smog doorway transition must complete with an observable preloaded sentinel.");
        int moved = 0;
        bool countedOpenDoor = false;
        for (int i = 0; i < 24; i++)
        {
            float x = _player.Position.X; Step();
            FailIf(_player.NativeNormalStateForInteraction == (bool)forced.GetValue(_player)!,
                "Smog's actual entry loop must expose state0b only during its consumed forced walk.");
            countedOpenDoor |= _entities.BossEntrySignal == 0x81;
            if (_player.Position.X != x)
            {
                FailIf(_player.Position.X != x + 1, "Smog forced entry must use one pixel per update.");
                moved++;
            }
        }
        FailIf(moved != 21 || (bool)forced.GetValue(_player)!,
            "Smog must execute its short forced walk and release Link after scrolling.");
        Vector2 entryEnd = _player.Position;
        // Contact, respawn helper, count branch and setstate each yield
        // before the shutter's six-update close. Link's walk ends first.
        for (int i = 0; _entities.BossEntrySignal != 0 && i < 8; i++) Step();
        FailIf(_player.Position != entryEnd, "Waiting for the entry shutter must not add forced Link movement.");
        FailIf(!countedOpenDoor || _entities.BossEntrySignal != 0,
            "Smog entry shutter must increment cc93 to81, then close and clear both its count and bit7.");
        Step(); // Enemy dispatch sees the preceding interaction pass's signal.
        var intro = _entities.Entities<SmogCharacter>().Single(enemy => enemy.SubId == 0);
        FailIf(intro.Position != new Vector2(120,88) || _entities.RoomEnemyCount != 2 || !_entities.PlayerUpdatesFrozen,
            "Registered INTERAC$33 must spawn its single intro cloud after shutter completion and keep Link locked.");
        if (intro.State == 0) Step();
        FailIf(intro.State != 8,"The intro cloud must initialize on its first eligible enemy pass after allocation.");
        // End only the textbox, retaining the real intro and placed sentinel.
        // Its 60-update lifetime cannot finish in the isolated three updates below.
        _dialogue.Close();
        int retainedEnemyCount = _entities.RoomEnemyCount;
        // A later merge can rewrite a still-uninitialized cloud to subid6.
        // That repeats room initialization in normal scrollMode1, not the
        // scrolling mode8 captured when the placed sentinel was constructed.
        int stops = 0;
        void Sound(int id) { if (id == OracleSoundEngine.SndCtrlStopMusic) stops++; }
        _entities.SoundRequested += Sound;
        try
        {
            _entities.LockSmogLinkAndMenu();
            typeof(RoomEntityManager).GetMethod("DisableLinkCollisionsAndMenu",flags)!.Invoke(_entities,null);
            var child = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120,88),3));
            child.SetMergeSubId(6);
            Vector2 linkBefore = _player.Position;
            Step();
            FailIf(child.State != 8 || child.Speed != 1 || child.IsDead || _entities.RoomEnemyCount != retainedEnemyCount + 1 || stops != 1 ||
                (bool)forced.GetValue(_player)! || _player.Position != linkBefore ||
                _entities.LinkCollisionsAndMenuDisabled || !_entities.PlayerUpdatesFrozen || _entities.BossEntrySignal != 0x80,
                "Dynamic Smog state0 subid6 must reuse the placed room initializer in normal mode, clear the separate collision lock and retain the controller's Link lock without re-arming entry.");
            Step(); Step();
            FailIf(!child.IsDead || _entities.RoomEnemyCount != retainedEnemyCount || stops != 1 ||
                (bool)forced.GetValue(_player)! || _player.Position != linkBefore,
                "Reinitialized subid6 must delete once without a second entrance walk or repeated room setup.");
        }
        finally { _entities.SoundRequested -= Sound; }
        LoadValidationRoom(0,0x60);
        FailIf(_entities.BossEntrySignal != 0, "A new room must clear the previous boss's cc93 signal.");
        GD.Print("Validated boss-entry request/handler timing, re-arming, Smog's west-door preload/forced walk and dynamic normal-mode room reinitialization.");
    }
}

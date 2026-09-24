using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidatePuzzleTrapReset()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var record = new PuzzleTrapResetDatabase().GetRoomRecords(4,0x9b).Single();
        byte[] offsets = [0xf0,0xe0,0x01,0x02,0x10,0x20,0xff,0xfe];
        FailIf(record.Order != 2 || record.Interval != 30 || record.Delay != 60 ||
            !record.Offsets.SequenceEqual(offsets) || record.Warp.DestinationGroup != 4 ||
            record.Warp.DestinationRoom != 0x9b || record.Warp.DestinationPosition != 0x12 ||
            record.Warp.SourceTransition != 0 || record.Warp.DestinationTransition != 3,
            "INTERAC $90:$1f lost source probe order, counters or hardcoded reset warp.");
        Vector2 Center(int packed) => new((packed & 15)*16+8,(packed >> 4)*16+8);
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1,Vector2 move = default)
            {
                input.CaptureForValidation([],[],move);
                if (batch) scheduler.Advance(count/60.0,update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0/60.0,update);
            }
            LoadValidationRoom(4,0x9b);
            var initial = _entities.Entities<PuzzleTrapResetRoomEntity>().Single();
            FailIf(initial.State != 0 || initial.Counter != 0 || _entities.InteractionSlot(initial) != 4,
                "Trap reset must allocate at source order $02 with zero state/counter.");
            Step();
            FailIf(initial.State != 1 || initial.Counter != 0,"Trap state0 must only advance state.");
            Step();
            FailIf(initial.Counter != 255,"Initial zero counter must wrap to $ff on the next update.");
            // At $18 the second upward probe wraps to scratch byte $cef8.
            // bank3.init clears this tail; placement/wizzrobe/secret writers
            // stop below $cef0. Read the authoritative byte, not padding.
            foreach (int position in new[] { 0x08,0x19,0x1a,0x28,0x38,0x17,0x16 })
                _currentRoom.SetPositionTileAndCollision(Center(position),0x2c,0x0f,(long)_animationTicks);
            _currentRoom.SetPositionTileAndCollision(Center(0x19),0xa0,0,(long)_animationTicks);
            FailIf(initial.IsTrapped(0x18),"A known open probe must disprove a trap despite an unknown earlier scratch probe.");
            _currentRoom.SetPositionTileAndCollision(Center(0x19),0x2c,0x0f,(long)_animationTicks);
            FailIf(_entities.RuntimeState.ReadWramByte(0xcef8) != 0 || initial.IsTrapped(0x18),
                "The cleared upper scratch tail must disprove a trap even when all room probes are blocked.");
            try
            {
                _entities.RuntimeState.SetWramByte(0xcef8, 0x10);
                FailIf(!initial.IsTrapped(0x18),
                    "An explicitly staged nonzero raw scratch byte must count as blocked.");
                _currentRoom.SetPositionTileAndCollision(Center(0x19),0xa0,0,(long)_animationTicks);
                FailIf(initial.IsTrapped(0x18), "A later open room probe must still disprove the trap.");
            }
            finally { _entities.RuntimeState.SetWramByte(0xcef8, 0); }
            // At packed$08 the near-up probe itself wraps to$cef8. If its
            // paired script byte$cff8 is zero, source skips far-up$cee8.
            foreach (int position in new[] { 0x09, 0x0a, 0x18, 0x28, 0x07, 0x06 })
                _currentRoom.SetPositionTileAndCollision(Center(position), 0x2c, 0x0f, (long)_animationTicks);
            try
            {
                _runtimeState.SetWramByte(0xcef8, 0x10);
                _runtimeState.SetWramByte(0xcff8, 0);
                FailIf(!initial.IsTrapped(0x08),
                    "A zero script-layout byte must skip the wrapped far probe after a blocked near probe.");
                _runtimeState.SetWramByte(0xcff8, 1);
                bool rejectedUnknownFarProbe = false;
                try { initial.IsTrapped(0x08); }
                catch (NotSupportedException error)
                {
                    rejectedUnknownFarProbe = error.Message.Contains("$e8");
                }
                FailIf(!rejectedUnknownFarProbe,
                    "A nonzero script-layout byte must retain the unrepresented far$e8 probe, not silently skip it.");
                _currentRoom.SetPositionTileAndCollision(Center(0x09), 0xa0, 0, (long)_animationTicks);
                FailIf(initial.IsTrapped(0x08),
                    "A later open probe must disprove the trap despite an earlier unresolved far probe.");
            }
            finally
            {
                _runtimeState.SetWramByte(0xcef8, 0);
                _runtimeState.SetWramByte(0xcff8, 0);
            }
            LoadValidationRoom(4,0x9b);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var trap = _entities.Entities<PuzzleTrapResetRoomEntity>().Single();
                _player.WarpTo(Center(0x88));
                for (int i = 0; _player.Position.Y > 104 && i < 80; i++)
                {
                    Step(move:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Trap test approach entered a solid metatile.");
                }
                FailIf(_player.Position != Center(0x68),$"Trap test must approach floor $68 south of the owl; reached {_player.Position}, repeat {repeat}.");
                // Isolate the trap detector by completing the eight surrounding
                // block destinations after Link reaches the real center floor.
                foreach (byte offset in offsets)
                    _currentRoom.SetPositionTileAndCollision(Center((byte)(0x68+offset)),0x2c,0x0f,(long)_animationTicks);
                FailIf(!trap.IsTrapped(0x68),"All eight source probes blocked must identify a trap.");
                if (repeat == 0)
                {
                    foreach (byte offset in offsets)
                    {
                        Vector2 point = Center((byte)(0x68+offset));
                        _currentRoom.SetPositionTileAndCollision(point,0x2c,0,(long)_animationTicks);
                        FailIf(trap.IsTrapped(0x68),"Each individual open source probe must reject a trap.");
                        _currentRoom.SetPositionTileAndCollision(point,0x2c,0x10,(long)_animationTicks);
                        FailIf(!trap.IsTrapped(0x68),"Special raw collision $10 must remain blocked for the trap detector.");
                        _currentRoom.SetPositionTileAndCollision(point,0x2c,0x0f,(long)_animationTicks);
                    }
                    _currentRoom.SetPositionTileAndCollision(Center(0x58),0,0xff,(long)_animationTicks);
                    _currentRoom.SetPositionTileAndCollision(Center(0x48),0xa0,0,(long)_animationTicks);
                    FailIf(!trap.IsTrapped(0x68),"Zero-layout near edge must skip its open far probe.");
                    _currentRoom.SetPositionTileAndCollision(Center(0x58),0x2c,0x0f,(long)_animationTicks);
                    _currentRoom.SetPositionTileAndCollision(Center(0x48),0x2c,0x0f,(long)_animationTicks);
                }
                _sound.ClearPlayRequestAudit();
                Step((trap.Counter == 0 ? 256 : trap.Counter)-1);
                FailIf(trap.Counter != 1 || _entities.PlayerUpdatesFrozen,"Trap must wait through counter1 before checking.");
                if (repeat == 0)
                {
                    // checkLinkVulnerable ORs the raw counter: both signs
                    // reject. updateSpecialObjects ages it before INTERACTION.
                    foreach (int signedDuration in new[] { 3, -3 })
                    {
                        _player.ApplyInteractionInvincibility(signedDuration);
                        Step();
                        FailIf(trap.Counter != 30 || trap.State != 1 || _entities.PlayerMenusDisabled ||
                            _sound.PlayRequestsFor(OracleSoundEngine.SndError) != 0,
                            "Either sign of Link invincibility must reject the trap silently and reload its interval.");
                        Step(29);
                    }
                }
                // Negative on the first route, positive on the repeated one.
                // Both expire in Link's dispatch immediately before the scan.
                _player.ApplyInteractionInvincibility(repeat == 1 ? -1 : 1);
                Step();
                FailIf(_player.InvincibilityFrames != 0 || trap.State != 2 || trap.Counter != 60 || !_entities.PlayerUpdatesFrozen ||
                    !_entities.PlayerMenusDisabled || !_entities.WarpTilesDisabled || _sound.PlayRequestsFor(OracleSoundEngine.SndError) != 1,
                    "Vulnerable trapped Link must request one error cue and the 60-update Link/menu lock.");
                if (repeat == 2)
                {
                    LoadValidationRoom(0,0x60); Step(2);
                    FailIf(_entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled || _entities.WarpTilesDisabled,
                        "Room departure must release the reset handler's restrictions.");
                    break;
                }
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(7);
                    FailIf(trap.Counter != 60,"Initialized trap delay must freeze during text.");
                }
                finally { _entities.TextActiveSource = text; }
                Vector2 held = _player.Position;
                Step(59,Vector2.Down);
                FailIf(trap.Counter != 1 || _player.Position != held || !_entities.PlayerUpdatesFrozen || !_entities.WarpTilesDisabled,
                    "Reset delay must hold Link through update59 despite movement input.");
                Step();
                for (int i = 0; IsTransitioning && i < 90; i++) Step();
                FailIf(IsTransitioning || _rooms.ActiveGroup != 4 || _currentRoom.Id != 0x9b ||
                    _player.Position != Center(0x12) || _entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled || _entities.WarpTilesDisabled ||
                    ReferenceEquals(trap,_entities.Entities<PuzzleTrapResetRoomEntity>().Single()) ||
                    _currentRoom.GetMetatile(Center(0x48)) != 0xa5,
                    "Reset warp must reload 4:9b at $12, restore its tiles and release the lock.");
            }
        }
    }
}

using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookTileExchange()
    {
        void Step(int count = 1, bool press = false) =>
            StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched: true);
        _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
        _inventory.EquipA(TreasureId.SwitchHook);
        LoadValidationRoom(4, 0x7c);
        Vector2 diamond = new(136, 88), origin = new(184.875f, 88.25f);
        // Source rooms/ages/large/room047c.bin has $db at packed $58 and
        // unbroken $a0 floor to its right through column $0d.
        FailIf(_currentRoom.GetMetatile(diamond) != 0xdb || _currentRoom.IsSolid(origin),
            "Room 4:7c source diamond $58 or its real approach floor changed.");
        _player.WarpTo(origin); _player.Face(Vector2I.Left);
        Step(press: true);
        var controller = _entities.SwitchHook!;
        var item = controller.Item!;
        int approach = 0;
        while (item.State == 1 && approach++ < 50) Step();
        FailIf(item.State != 3 || item.Substate != 0 || !controller.ExchangeActive ||
            !_entities.PlayerMenusDisabled || !_entities.PlayerContactDisabled ||
            _currentRoom.GetMetatile(diamond) != 0xdb || _player.PrecisePosition != origin ||
            _entities.Entities<ClinkEffect>().Count == 0,
            "Hook did not latch the real diamond while leaving both original positions and the tile intact.");
        Step(17);
        FailIf(item.Substate != 0 || _currentRoom.GetMetatile(diamond) != 0xdb || controller.Helper is not null,
            "Latch animation must finish its17 updates before committing the break or allocating helper $de.");
        Vector2 cameraBeforeHelper = -_transitions.WorldToGameplayScreen(Vector2.Zero);
        Step();
        FailIf(item.Substate != 1 || item.ZHigh != 0 || _currentRoom.GetMetatile(diamond) != 0xa0 ||
            controller.Helper is not { Initialized: true } || controller.CameraFocus != diamond,
            "Lift start must replace $db with $a0 and initialize the later helper slot without moving Z.");
        Vector2 expectedCamera = new(Mathf.MoveToward(cameraBeforeHelper.X, 56, 1),
            Mathf.MoveToward(cameraBeforeHelper.Y, 24, 1));
        FailIf(-_transitions.WorldToGameplayScreen(Vector2.Zero) != expectedCamera,
            "The camera must observe helper $de on its creation update and move one pixel toward that focus.");
        Step(15);
        FailIf(!_player.TopDownAirborne || _player.TopDownAirSpeedZ != 0x01c0,
            "Link must observe the hook's prior negative Z in its next dispatch and accumulate14 gravity steps before lift update15.");
        FailIf(item.Substate != 1 || item.ZHigh != -15 || _player.SwitchHookZFixed != -15 * 256 ||
            _player.PrecisePosition != origin,
            "Lift changed XY or advanced to swap before its16th update.");
        Step();
        FailIf(item.Substate != 2 || item.ZHigh != -16 || _player.PrecisePosition != origin,
            "Lift update16 must enter swap state while retaining the original XY snapshots.");
        Step();
        FailIf(item.Substate != 3 || item.ZHigh != -16 || _player.PrecisePosition != diamond ||
            item.PrecisePosition != origin || _player.FacingVector != Vector2I.Right || controller.CameraFocus != diamond,
            "Swap must exchange full XY snapshots, center tile high bytes, reverse Link and retain helper camera focus.");
        Step(15);
        FailIf(item.Finished || item.ZHigh != -1 || !controller.ExchangeActive ||
            _currentRoom.GetMetatile(origin) != 0xa0,
            "Diamond was placed or controls released before lowering update16.");
        Step();
        FailIf(!item.Finished || controller.ExchangeActive || controller.Helper is not null || controller.CameraFocus is not null ||
            _entities.PlayerMenusDisabled || _entities.PlayerContactDisabled || _player.SwitchHookZFixed != 0 ||
            _currentRoom.GetMetatile(origin) != 0xdb || _currentRoom.GetMetatile(diamond) != 0xa0 || !_player.IsUsingSwitchHook,
            "Lowering completion must place the diamond, release helper/collision locks and retain the parent until the next update.");
        FailIf(!_player.TopDownAirborne || _player.TopDownAirSpeedZ != 0x40,
            "The final hook write restores Z=0 after Link's dispatch; its air state must remain live until the next update.");
        Step();
        FailIf(_player.IsUsingSwitchHook || _player.TopDownAirborne || _player.TopDownAirSpeedZ != 0,
            "Exchange completion must release the parent and resolve the retained air state on the following Link update.");
        Step(press: true);
        int repeat = 0;
        while (controller.Item is { Finished: false } && repeat++ < 130) Step();
        FailIf(controller.Item is not { Finished: true } || _currentRoom.GetMetatile(diamond) != 0xdb ||
            _currentRoom.GetMetatile(origin) != 0xa0 || _player.Position != origin.Floor(),
            $"Link could not repeat exchange: active={_player.IsUsingSwitchHook}, tile58={_currentRoom.GetMetatile(diamond):x2}, tile5b={_currentRoom.GetMetatile(origin):x2}, Link={_player.Position}, item={controller.Item?.State}/{controller.Item?.Substate}, repeat={repeat}, knockback={_player.KnockbackFrames}.");
        // Ordinary contact resumes on landing. A nearby Moblin can damage
        // Link now; its following knockback update must not be mistaken for
        // displacement by the exchange itself.
        Step();
        FailIf(_player.IsUsingSwitchHook, "Repeated exchange retained its parent after landing.");

        var random = CaptureOracleRandomForValidation();
        void BeginDiamond()
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x7c);
            _player.WarpTo(origin); _player.Face(Vector2I.Left);
            Step(press: true);
            int ticks = 0;
            while (controller.Item!.State == 1 && ticks++ < 45) Step();
            FailIf(controller.Item is not { State: 3, Substate: 0 }, "Repeat fixture failed to latch the source diamond.");
        }
        BeginDiamond();
        _player.DropHeldItemsForScript();
        Step();
        FailIf(controller.ExchangeActive || _currentRoom.GetMetatile(diamond) != 0xdb ||
            controller.Helper is not null || _player.PrecisePosition != origin,
            "Cancellation during the latch must leave the tile and Link in their original positions.");
        Step();
        BeginDiamond();
        Step(18); Step(5);
        FailIf(_player.SwitchHookZFixed != -5 * 256, "Warp cancellation fixture did not reach the lifted state.");
        _player.WarpTo(origin);
        FailIf(controller.Active || controller.Item is not null || controller.Helper is not null ||
            _player.SwitchHookZFixed != 0 || _entities.PlayerMenusDisabled || _entities.PlayerContactDisabled ||
            _currentRoom.GetMetatile(diamond) != 0xa0,
            "Warp cancellation must discard the helper and locks while preserving the committed tile removal.");
        (Vector2, Vector2, int, int, int, Vector2?) Sample(bool batched)
        {
            BeginDiamond();
            if (batched) Step(35); else for (int tick = 0; tick < 35; tick++) Step();
            var current = controller.Item!;
            return (_player.PrecisePosition, current.PrecisePosition, current.Substate, current.ZHigh,
                _currentRoom.GetMetatile(diamond), controller.CameraFocus);
        }
        FailIf(Sample(false) != Sample(true), "Exchange lift/swap differs between individual and batched gameplay updates.");

        foreach (int placementCase in new[] { 0, 1, 2 })
        {
            BeginDiamond();
            Step(34); // latch18 + lift16, before the swap update.
            Vector2 neighbor = new(200, 88);
            // Model a tile/collision writer changing the destination while
            // Link is lifted. Even one solid quadrant rejects the whole tile.
            _currentRoom.SetPositionTileAndCollision(origin, (byte)(placementCase == 2 ? 0xda : 0xa0), 1, 0);
            if (placementCase == 1) _currentRoom.SetPositionTileAndCollision(neighbor, 0xa0, 1, 0);
            Step();
            Vector2 expected = placementCase == 0 ? neighbor : origin.Floor();
            FailIf(controller.Item!.Position != expected,
                $"Diamond placement case{placementCase} lost whole-byte collision, opposite-direction neighbor or Somaria exception.");
            Step(16);
            if (placementCase == 1)
            {
                FailIf(_currentRoom.GetMetatile(origin) == 0xdb || _entities.Entities<RockDebrisEffect>().Count == 0,
                    "A diamond blocked at both candidate tiles must break through effect $06 on landing.");
            }
            else FailIf(_currentRoom.GetMetatile(expected) != 0xdb,
                "Diamond failed to persist on its valid fallback/Somaria destination.");
        }

        LoadValidationRoom(4, 0x6a);
        Vector2 pot = new(184, 24), potApproach = new(200, 24);
        FailIf(_currentRoom.GetMetatile(pot) != 0x10 || _currentRoom.IsSolid(potApproach),
            "Room 4:6a source pot $1b or adjacent approach floor changed.");
        _player.WarpTo(potApproach); _player.Face(Vector2I.Left);
        Step(press: true);
        int potTicks = 0;
        while (controller.Item is { Finished: false } && potTicks++ < 110) Step();
        FailIf(controller.Item is not { Finished: true } || _player.Position != pot ||
            _currentRoom.GetMetatile(pot) != 0xa0 || _currentRoom.GetMetatile(potApproach) != 0xa0 ||
            _entities.Entities<RockDebrisEffect>().Count == 0,
            "Switchable pot must break on landing rather than persist as a relocated tile.");
        LoadValidationRoom(4, 0x91);
        FailIf(controller.Active || controller.Helper is not null || controller.Item is not null,
            "Room exit retained a hook helper, item or parent.");
    }
}

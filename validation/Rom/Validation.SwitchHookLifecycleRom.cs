using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookLifecycleRom() => ValidateSwitchHookGameplayRom(false);

    private void ValidateSwitchHookTileExchangeRom() => ValidateSwitchHookGameplayRom(true);

    private void ValidateSwitchHookWaterExchangeRom() => ValidateSwitchHookGameplayRom(true, true);
    private void ValidateSwitchHookUnderwaterExchangeRom() => ValidateSwitchHookGameplayRom(true, underwater: true);

    private void ValidateSwitchHookAllocationRom() => ValidateSwitchHookGameplayRom(false, allocation: true);
    private void ValidateSwitchHookAirborneRom() => ValidateSwitchHookGameplayRom(false, airborne: true);

    private void ValidateSwitchHookGameplayRom(bool exchange, bool water = false, bool allocation = false, bool airborne = false, bool underwater = false)
    {
        int hostCase1 = 0;
        foreach (bool primary in underwater ? new[] { true } : new[] { false, true })
        foreach (int level in underwater ? new[] { 1 } : new[] { 1, 2 })
        foreach (int direction in underwater ? new[] { 0, 1 } : Enumerable.Range(0, 4))
        foreach (int terrain in airborne ? new[] { 0 } : allocation ? new[] { 4 } : exchange ? new[] { 3 } : new[] { 0, 1, 2 })
        foreach (int jumpUpdates in airborne ? new[] { 1, 15, 29 } : new[] { 0 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(underwater ? 5 : 0, underwater ? 0x2c : 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook, level);
            if (airborne) _inventory.GiveTreasure(TreasureId.Feather, 1);
            if (water) _inventory.GiveTreasure(TreasureId.Flippers, 0);
            if (underwater)
            {
                _inventory.GiveTreasure(TreasureId.Flippers,0);
                _inventory.GiveTreasure(TreasureId.MermaidSuit,0);
            }
            _inventory.EquipA(primary ? TreasureId.SwitchHook : airborne ? TreasureId.Feather : 0);
            _inventory.EquipB(primary ? airborne ? TreasureId.Feather : 0 : TreasureId.SwitchHook);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool wall = terrain is >= 1 and <= 3 && (x is 2 or 7 || y is 1 or 6);
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    wall ? terrain == 1 ? (byte)0x2e : terrain == 2 ? (byte)0xfd : (byte)0xdb : (byte)0xa0,
                    wall ? (byte)0x0f : (byte)0, 0);
                if (exchange && wall)
                    _currentRoom.SetUnderlyingMetatile(new(x * 16 + 8, y * 16 + 8), water ? (byte)0xfa : (byte)0x3a);
            }
            _player.WarpTo(new(80.25f, 64.5f)); _player.Face(Vector2I.Up);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 0, 80, 64);
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            int button = primary ? 1 : 2, update = 0;
            int liftStarted = -1, swapEntered = -1, loweringStarted = -1;
            BombEffect? capacityBomb = null;
            if (allocation)
            {
                // $d6 is reserved: five occupied $d7-$db slots block only
                // the chain. @noCollisionWithTile retries each flight update.
                var bombs = new BombDatabase().Data;
                for (int index = 0; index < 5; index++)
                {
                    var bomb = _entities.Spawn<BombEffect>(new BombSpawn(_player, bombs, 0, _ => { }));
                    if (index == 0) capacityBomb = bomb;
                    int slot = 0xd700 + index * 0x100;
                    rom[slot] = 1; rom[slot + 1] = 3;
                    rom[slot + 0x0b] = 64; rom[slot + 0x0d] = 80;
                }
            }
            void Step(int count = 1, bool press = false, int angle = 0xff, bool feather = false, bool held = false)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int directions = (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int itemsHeld = (press || held ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                int edge = (press ? button : 0) | (feather ? primary ? 2 : 1 : 0);
                StepGameplayUpdates(count, movement, MenuRomActions(itemsHeld | directions), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, itemsHeld | directions, angle, _entities.FrameCounter); edge = 0;
                    string context = $"Switch Hook L{level} A={primary} dir={direction} terrain={terrain} water={water} underwater={underwater} jumpUpdates={jumpUpdates} update={++update}";
                    var hook = _entities.SwitchHook!.Item;
                    bool nativeActive = rom[0xd600] != 0 && rom[0xd601] == 0x0a;
                    FailIf(_player.NativeItemUseActive != (rom[0xcc5f] != 0),context + ": wLinkUsingItem1 must survive child deletion until the parent clears.");
                    FailIf((hook is { Finished: false }) != nativeActive ||
                        _player.IsUsingSwitchHook != (rom[0xd200] != 0 && rom[0xd201] == 0x0a),
                        context + $": reserved weapon/parent deletion or release differs: runtime={hook?.State}/{hook?.Substate}/{hook?.ZHigh}/finished={hook?.Finished}/parent={_player.IsUsingSwitchHook}, native=${rom[0xd604]:x2}/${rom[0xd605]:x2}/${rom[0xd60f]:x2}/enabled={rom[0xd600]}/parent={rom[0xd200]}.");
                    if (underwater && _player.IsUsingSwitchHook)
                        FailIf(rom[0xd032] != 0xc0 + CarriedObjectMotion.DirectionIndex(_player.FacingVector),
                            context + ": underwater mode$2e must retain the source $c0-$c3 hook pose.");
                    if (nativeActive)
                    {
                        FailIf(hook!.State != rom[0xd604] || hook.Substate != rom[0xd605] || hook.Counter != rom[0xd606] ||
                            hook.Angle != rom[0xd609] || hook.ZHigh != (sbyte)rom[0xd60f] ||
                            hook.PrecisePosition != new Vector2(rom.Word(0xd60c) / 256.0f, rom.Word(0xd60a) / 256.0f) ||
                            hook.Visible != ((rom[0xd61a] & 0x80) != 0) ||
                            hook.CollisionEnabled != ((rom[0xd624] & 0x80) != 0),
                            context + $": hook state/count/XY differs: runtime={hook.State}/{hook.Substate}/{hook.Counter}/{hook.PrecisePosition}, native={rom[0xd604]}/{rom[0xd605]}/{rom[0xd606]}/{rom.Word(0xd60c) / 256.0f},{rom.Word(0xd60a) / 256.0f}.");
                        var animation = (EnemyAnimationPlayer)typeof(SwitchHookItem).GetField("_animation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hook)!;
                        // itemMimicBgTile replaces the hook animation with a
                        // static tile sprite. The port's tile texture owns
                        // that presentation; its retired hook clock is unused.
                        if (hook.State != 3 || hook.Substate == 0)
                            FailIf(animation.CurrentParameter != rom[0xd621] ||
                                (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(animation)! != rom[0xd620],
                                context + ": active hook animation parameter/counter differs.");
                    }
                    if (exchange)
                    {
                        int z = _player.SwitchHookZFixed;
                        FailIf((z & 0xffff) != rom.Word(0xd00e) ||
                            _entities.SwitchHook.ExchangeState != rom[0xccdd] ||
                            _entities.PlayerMenusDisabled != (rom[0xcbca] != 0),
                            context + $": exchange Link Z/state/menu lock differs: runtime={z}/{_entities.SwitchHook.ExchangeState}/{_entities.PlayerMenusDisabled}, native={rom.Word(0xd00e)}/${rom[0xccdd]:x2}/${rom[0xcbca]:x2}.");
                        FailIf(_player.TopDownSwimmingState != (rom[0xcc5d] & 15),
                            context + ": exchange landing swimming handoff differs.");
                        if (hook is { Finished: false, State: 3, Substate: 1 } && liftStarted < 0) liftStarted = update;
                        if (hook is { Finished: false, State: 3, Substate: 2 } && swapEntered < 0)
                        {
                            swapEntered = update;
                            FailIf(update - liftStarted != 16 || hook.ZHigh != -16,
                                context + ": exchange rise did not retain its exact sixteen-update boundary.");
                        }
                        if (hook is { Finished: false, State: 3, Substate: 3 } && loweringStarted < 0)
                        {
                            loweringStarted = update;
                            FailIf(update - swapEntered != 1 || hook.ZHigh != -16,
                                context + ": position exchange did not consume one update before lowering.");
                        }
                        if (hook is { Finished: true } && loweringStarted >= 0)
                        {
                            FailIf(update - loweringStarted != (water ? 14 : 16),
                                context + ": exchange finished before/after its sixteen lowering updates.");
                            loweringStarted = -1;
                        }
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 10; x++)
                        {
                            Vector2 point = new(x * 16 + 8, y * 16 + 8); int packed = y * 16 + x;
                            FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + packed] ||
                                _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + packed],
                                context + $": exchanged tile/collision differs at ${packed:x2}.");
                        }
                    }
                    int[] chains = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x0b).ToArray();
                    FailIf((hook?.ChainAllocated == true) != (chains.Length == 1),
                        context + $": chain allocation differs: runtime={hook?.ChainAllocated}, native={chains.Length}, slot $d7=${rom[0xd700]:x2}/${rom[0xd701]:x2}.");
                    if (chains.Length == 1)
                    {
                        int chain = chains[0];
                        FailIf(hook!.ChainPosition != new Vector2(rom[chain + 0x0d], rom[chain + 0x0b]) ||
                            hook.ChainZHigh != (sbyte)rom[chain + 0x0f] ||
                            hook.ChainVisible != ((rom[chain + 0x1a] & 0x80) != 0),
                            context + ": unconditional chain post-pass XY/Z/visibility differs.");
                    }
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008], context + ": Link movement/facing differs.");
                    if (airborne)
                    {
                        FailIf(_player.TopDownAirborne != (rom[0xcc5c] != 0) ||
                            (_player.ItemCreationZFixed & 0xffff) != rom.Word(0xd00e) ||
                            (_player.TopDownAirSpeedZ & 0xffff) != rom.Word(0xd014),
                            context + ": full Link Z/gravity/landing differs.");
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": full Feather/Hook sound order differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                    }
                    FailIf(!sounds.Requests.Where(id => id is 0xa7 or 0x8e or SoundId.SndClink)
                        .SequenceEqual(rom.Sounds.Where(id => id is 0xa7 or 0x8e or SoundId.SndClink)),
                        context + ": flight/clink sound order differs.");
                });
            }
            for (int repeat = 0; repeat < (water ? 1 : 2); repeat++)
            {
                liftStarted = swapEntered = loweringStarted = -1;
                if (airborne)
                {
                    Step(jumpUpdates, feather: true);
                    FailIf(!_player.TopDownAirborne, "Switch Hook air gate fixture did not launch equipped Feather.");
                    Step(1, press: true);
                    FailIf(_player.IsUsingSwitchHook || _entities.SwitchHook!.Item is { Finished: false },
                        "Fresh airborne Switch Hook allocated its parent or reserved weapon.");
                    _dialogue.ShowMessage("Rejected air Hook pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6, held: true);
                    _dialogue.Close(); rom[0xcba0] = 0;
                    Step(35, held: true);
                    FailIf(_player.TopDownAirborne || _player.IsUsingSwitchHook || _entities.SwitchHook!.Item is { Finished: false },
                        "Held Switch Hook input restarted after Feather landing without a new edge.");
                    Step();
                }
                Step(1, true, exchange && repeat == 1 ? 0xff : direction * 8);
                FailIf(_entities.SwitchHook!.Item?.Counter != (level == 1 ? 41 : 38),
                    "Switch Hook did not initialize the level-specific native extension counter.");
                if (airborne)
                {
                    Step(1, feather: true);
                    FailIf(_player.TopDownAirborne || !_player.IsUsingSwitchHook,
                        "Feather must reject a fresh jump while ParentItem2 holds the existing Switch Hook.");
                }
                Step(6, angle: (direction * 8 + 8) & 0x1f);
                if (allocation && repeat == 0)
                {
                    FailIf(_entities.SwitchHook.Item!.ChainAllocated || !_player.IsUsingSwitchHook,
                        "Full child pool blocked the reserved hook or allocated a chain.");
                    capacityBomb!.Discard();
                    rom.DeleteDynamicItem(0xd700);
                    FailIf(rom[0xd700] != 0, "Native itemDelete did not release capacity slot $d7.");
                    Step(2);
                    FailIf(!_entities.SwitchHook.Item.ChainAllocated,
                        "Flying hook did not retry chain allocation after a dynamic slot became free.");
                    // Release the remaining capacity occupants before their
                    // fuses introduce an unrelated damage/cancellation path.
                    foreach (var bomb in _entities.Entities<BombEffect>()) bomb.Discard();
                    for (int slot = 0xd800; slot <= 0xdb00; slot += 0x100) rom.DeleteDynamicItem(slot);
                }
                _dialogue.ShowMessage("Hook flight pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                int started = update;
                while (_player.IsUsingSwitchHook && update - started < 160) Step(3);
                FailIf(_player.IsUsingSwitchHook || _entities.SwitchHook.Item is not { Finished: true },
                    "Switch Hook failed to retract, delete its chain and release its parent.");
                if (exchange) FailIf(liftStarted < 0 || swapEntered < 0,
                    "Switch Hook fixture did not complete a native tile exchange.");
                Step(4);
            }
            if (water)
            {
                Step(10);
                FailIf(!_player.TopDownSwimming, "Switch Hook water landing did not cancel into swimming.");
                continue;
            }
            Step(1, true, direction * 8); Step(6);
            _playerWorld.ClearItemParents(_player); rom.ClearItemParents();
            Step(6);
            FailIf(_entities.SwitchHook!.Item is not { Finished: true },
                "Switch Hook post-pass failed to delete its child after parent cancellation.");
            Step(1, true, direction * 8); Step(6);
        }
        GD.Print($"Validated clean-US Switch Hook exchange={exchange}, water={water}, underwater={underwater}, allocation={allocation}, Feather air gates={airborne}, fractional initialization, repeated exchange, chain post-updates during dialogue, animation/sounds and parent lifecycle through split/batched application updates.");
    }
}

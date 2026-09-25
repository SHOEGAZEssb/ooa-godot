using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookFlight()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int count = 1, string? button = null, Vector2 movement = default) =>
            StepGameplayUpdates(count, movement, button is null ? [] : [button], button is null ? [] : [button], batched: true);
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureShield, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        _inventory.EquipB(InventoryState.ItemShield);
        LoadValidationRoom(4, 0x91);
        Vector2 origin = new(120, 136);
        FailIf(_currentRoom.IsSolid(origin), "Skull Dungeon entrance flight fixture must start on real walkable floor.");
        _player.WarpTo(origin);
        _player.Face(Vector2I.Up);
        _player.UpdateShieldForValidation(attackHeld: false, itemHeld: true);
        Step(button: "attack");
        var hook = _entities.SwitchHook!.Item!;
        FailIf(!_player.IsUsingSwitchHook || _player.IsUsingShield || !_player.StartedItemAnimationThisUpdate ||
            hook is not { State: 1, Counter: 41, ChainVisible: false } || hook.Position != new Vector2(120, 137),
            "Switch Hook A-button initialization lost the parent pulse, high-byte origin or unmoved state0.");
        Step(movement: Vector2.Right);
        FailIf(_player.Position != origin || _player.StartedItemAnimationThisUpdate || hook.Position != new Vector2(120, 135) ||
            !hook.ChainVisible || hook.ChainPosition != new Vector2(120, 137),
            "First flight update must move two pixels, freeze Link and draw chain segment2 using signed division.");
        var chainAnimation = (EnemyAnimationPlayer)typeof(SwitchHookItem)
            .GetField("_chainAnimation", flags)!.GetValue(hook)!;
        ValidateSwitchHookChainPixels(chainAnimation);
        Vector2 frozenPosition = hook.Position;
        int frozenCounter = hook.Counter;
        var textSource = _entities.TextActiveSource;
        _entities.TextActiveSource = () => true;
        Step();
        FailIf(hook.Position != frozenPosition || hook.Counter != frozenCounter || hook.ChainPosition != new Vector2(120, 138),
            "Dialogue must freeze initialized hook motion while the unconditional post pass advances chain segment1.");
        _entities.TextActiveSource = textSource;
        int completion = 0;
        while (_player.IsUsingSwitchHook && completion++ < 120) Step();
        FailIf(_player.IsUsingSwitchHook || !hook.Finished || completion >= 120 || _player.Position != origin,
            "Switch Hook failed to retract/release its parent without displacing Link.");
        _inventory.EquipB(InventoryState.ItemSwitchHook);
        Step(button: "item");
        FailIf(!_player.IsUsingSwitchHook || _entities.SwitchHook.Item == hook,
            "Switch Hook B-button could not repeat after completion.");
        var interrupted = _entities.SwitchHook.Item!;
        FailIf(!_player.ApplyEnemyContactDamage(origin + Vector2.Down * 20, 1) || interrupted.Finished,
            "Damage must request hook cancellation without deleting the child in the contact pass.");
        Step();
        FailIf(!interrupted.Finished || !_player.IsUsingSwitchHook,
            "Damaged hook must delete in the next item pass, retaining its parent until the following Link update.");
        Step();
        FailIf(_player.IsUsingSwitchHook, "Hook parent failed to observe the child deletion during knockback.");
        _player.WarpTo(origin);
        FailIf(_player.IsUsingSwitchHook || _entities.SwitchHook.Item is not null,
            "Player warp failed to cancel the hook parent and child.");

        (int, int, Vector2, Vector2) Run(bool batched)
        {
            _inventory.EquipA(InventoryState.ItemSwitchHook);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(origin); _player.Face(Vector2I.Up);
            Step(button: "attack");
            if (batched) Step(12); else for (int i = 0; i < 12; i++) Step();
            var item = _entities.SwitchHook!.Item!;
            return (item.State, item.Counter, item.Position, item.ChainPosition);
        }
        FailIf(Run(false) != Run(true), "Switch Hook differs between twelve individual updates and one twelve-update host frame.");
        LoadValidationRoom(4, 0x91);
        FailIf(_entities.SwitchHook!.Active || _entities.SwitchHook.Item is not null,
            "Room teardown retained a Switch Hook item or parent.");

        // Isolate source counters and cancellation states from room contacts.
        var data = new SwitchHookDatabase();
        var sounds = new List<int>();
        var flight = new SwitchHookItem(data, 1, origin, 0, sounds.Add);
        flight.UpdateItem(_currentRoom, origin, false, () => true);
        flight.UpdateItem(_currentRoom, origin, false, () => false);
        flight.UpdatePost(origin, true);
        FailIf(flight.ChainVisible || sounds.Count != 0, "Counter40 must be silent and a full dynamic item pool must suppress chain creation.");
        flight.UpdateItem(_currentRoom, origin, false, () => true);
        flight.UpdatePost(origin, true);
        FailIf(!flight.ChainVisible || !sounds.SequenceEqual(new[] { 0xa7 }),
            "Hook failed to retry chain allocation, or counter39 lost SND_SWITCH_HOOK.");
        typeof(SwitchHookItem).GetProperty("ZHigh", flags)!.SetValue(flight, -6);
        FailIf(flight.ChainZHigh != 0, "Chain height remains its own byte until itemCode0bPost copies weapon Z.");
        flight.UpdatePost(origin, true);
        typeof(SwitchHookItem).GetProperty("ZHigh", flags)!.SetValue(flight, -4);
        FailIf(flight.ChainZHigh != -6, "A related object must read the chain's copied Z, not the weapon's newer Z.");
        flight.RequestCancellation();
        flight.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(!flight.Finished, "Extension state1 failed to honor parent cancellation.");
        flight.Free();

        var timed = new SwitchHookItem(data, 1, origin, 0, _ => { });
        timed.UpdateItem(_currentRoom, origin, false, () => true);
        for (int tick = 0; tick < 40; tick++) timed.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(timed.State != 1 || timed.Counter != 1 || timed.Position != new Vector2(120, 57),
            $"Hook must move on extension updates1-40: state={timed.State}, counter={timed.Counter}, position={timed.Position}.");
        timed.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(timed.State != 2 || timed.Counter != 0 || timed.Position != new Vector2(120, 57),
            "Extension update41 must only enter retraction, without moving.");
        for (int tick = 0; tick < 36; tick++) timed.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(timed.Substate != 1 || timed.Counter != 3 || timed.Position != new Vector2(120, 129),
            "Retraction must stop when high Y enters Link's [-8,+7] window, then hold three updates.");
        timed.RequestCancellation(); // State2 does not inspect var2f bit5.
        for (int tick = 0; tick < 2; tick++) timed.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(timed.Finished || timed.Counter != 1 || timed.Position != origin,
            "The invisible retraction hold must follow Link and survive until its third update.");
        timed.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(!timed.Finished, "Retraction hold failed to delete on update3.");
        timed.Free();
        var longHook = new SwitchHookItem(data, 2, origin, 0, _ => { });
        longHook.UpdateItem(_currentRoom, origin, false, () => true);
        for (int tick = 0; tick < 37; tick++) longHook.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(longHook.State != 1 || longHook.Counter != 1 || longHook.Position != new Vector2(120, 26),
            "Long Hook must move three pixels on extension updates1-37.");
        longHook.UpdateItem(_currentRoom, origin, false, () => true);
        FailIf(longHook.State != 2 || longHook.Counter != 0 || longHook.Position != new Vector2(120, 26),
            "Long Hook extension update38 must enter retraction without another movement.");
        longHook.Free();
    }

    private void ValidateSwitchHookSourceData()
    {
        var database = new SwitchHookDatabase();
        for (int level = 1; level <= 2; level++)
        {
            var record = database.Level(level);
            FailIf(record.SpeedRaw != (level == 1 ? 80 : 120) ||
                record.ExtensionFrames != (level == 1 ? 41 : 38) || record.RetractedFrames != 3 ||
                record.LiftFrames != 16 || record.SoundMask != 3 || record.FlightSound != 0xa7 ||
                record.ExchangeSound != 0x8e || record.DiamondTile != 0xdb || record.SomariaTile != 0xda ||
                record.BreakSource != 8,
                $"Switch Hook level {level} lost the source's speed/counters, sounds or tile identities.");
        }
        FailIf(!LinkItemDatabase.Shared.ParentAnimationSignalsItemUse(0x0a),
            "Switch Hook parent must retain linkItemAnimationTable's $f6 item-start bit.");
        Vector2I[] positions = [new(0, 1), new(1, 3), new(0, 1), new(-1, 3)];
        int[] placement = [16, -1, -16, 1];
        string[] initialOam = ["8,0,0,64;8,8,0,96", "8,0,6,32;8,8,4,32",
            "8,0,0,0;8,8,0,32", "8,0,4,0;8,8,6,0"];
        string[] latchedOam = ["8,0,2,64;8,8,2,96", "8,0,10,32;8,8,8,32",
            "8,0,2,0;8,8,2,32", "8,0,8,0;8,8,10,0"];
        var actor = new Node2D();
        var animation = new EnemyAnimationPlayer(actor, 6);
        // spr_switch_hook is the standalone image loaded into VRAM $8520;
        // its local source tile origin is zero, while Item.oamTileIndexBase is $52.
        animation.Load(OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_switch_hook.png"),
            database.HookGraphics.Select(g => g.Animation).ToArray(), 0, 1);
        for (int direction = 0; direction < 4; direction++)
        {
            var offset = database.Offset(direction);
            FailIf(offset.Position != positions[direction] || offset.Z != 0 || offset.PlacementOffset != placement[direction],
                $"Switch Hook direction {direction} lost its original hook/diamond offsets.");
            var graphic = database.Graphic(direction + 2);
            FailIf(graphic.Sprite != "spr_switch_hook" || graphic.TileBase != 0x52 || graphic.OamFlags != 9 ||
                graphic.Collision != 0x8d || graphic.Radius != new Vector2I(6, 6) ||
                graphic.Damage != 0xfe || graphic.Health != 0x7e,
                "ITEM_SWITCH_HOOK $0a lost its source graphics or itemAttributes row.");
            var frames = OracleGraphicsCache.GetAnimationDefinition(graphic.Animation).Frames;
            FailIf(!frames.Select(f => f.Duration).SequenceEqual(new[] { 1, 4, 4, 4, 4, 127 }) ||
                !frames.Select(f => f.Parameter).SequenceEqual(new[] { 0, 0, 0, 0, 0, 128 }) ||
                !frames.Select(f => f.EncodedOam).SequenceEqual(new[] {
                    initialOam[direction], latchedOam[direction], initialOam[direction],
                    latchedOam[direction], initialOam[direction], latchedOam[direction] }),
                $"Switch Hook direction {direction} lost source duration, parameter or flipped OAM boundaries.");
            animation.SetAnimation(direction + 2);
            for (int tick = 1; tick <= 16; tick++)
            {
                animation.Advance();
                FailIf(animation.CurrentParameter != 0,
                    $"Switch Hook latch released early at animation update {tick}.");
            }
            animation.Advance();
            FailIf(animation.CurrentParameter != 0x80 || animation.FrameIndex != 5,
                "Switch Hook latch must write $80 on animation update17; state3 observes it on the next dispatch.");
            FailIf(animation.CurrentTexture.GetImage().IsInvisible(), "Switch Hook latch graphic rendered empty.");
        }
        var chain = database.Chain;
        FailIf(chain.Sprite != "spr_common_sprites" || chain.TileBase != 0x16 || chain.OamFlags != 9 ||
            chain.Collision != 0x12 || chain.Radius != Vector2I.Zero || chain.Damage != 0 || chain.Health != 0x7e ||
            chain.Animation != "127,0@8,4,0,0",
            "ITEM_SWITCH_HOOK_CHAIN $0b lost the aliased itemAnimation1e7ad/item20OamDataPointers data.");
        for (int index = 0; index < 2; index++)
        {
            var frames = OracleGraphicsCache.GetAnimationDefinition(database.Graphic(index).Animation).Frames;
            FailIf(frames.Length != 1 || frames[0].Duration != 127 || frames[0].Parameter != 0,
                "Switch Hook base animation aliases must retain their single $7f-duration frame.");
        }
        actor.Free();
    }

    private void ValidateSwitchHookChainPixels(EnemyAnimationPlayer animation)
    {
        // Independently read spr_common_sprites.png tile pair $16/$17 (x=88,
        // y=0): a 4x4 link at cell-local (2,6). itemOamData's (8,4) places
        // it at (14,14) in the centered frame. standardSpritePaletteData $01
        // supplies black and RGB5 ($03,$10,$1f); shade 0 is transparent.
        string[] link = ["0110", "1221", "1221", "0110"];
        uint[] colors = [0, 0x000000ff, 0x1883ffff];
        using var image = animation.CurrentTexture.GetImage();
        FailIf(image.GetSize() != new Vector2I(32, 32) || animation.CurrentOffset != new Vector2(-16, -16),
            "ITEM_SWITCH_HOOK_CHAIN $0b lost its centered OAM frame origin.");
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            int shade = x is >= 14 and < 18 && y is >= 14 and < 18 ? link[y - 14][x - 14] - '0' : 0;
            Color pixel = image.GetPixel(x, y);
            FailIf(shade == 0 ? pixel.A != 0 : pixel.ToRgba32() != colors[shade],
                $"ITEM_SWITCH_HOOK_CHAIN $0b pixel ({x},{y}) differs from resident bank-1 tile $16; expected shade {shade}, got ${pixel.ToRgba32():x8}.");
        }
    }
}

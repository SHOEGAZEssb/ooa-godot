using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGuardSomariaRemovalRom()
    {
        foreach (bool attacking in new[] { false, true })
        foreach (string gate in new[] { "accepted", "xy", "height", "invincibility", "pending", "disabled" })
        foreach (bool batched in RomHostSchedules(gate == "accepted" ? 0 : 1))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            LoadValidationRoom(attacking ? 5 : 1, attacking ? 0xd1 : 0x35);
            var guard = _entities.EntityAdapters<AmbiGuardRoomEntity>().First();
            foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a, guard)).ToArray())
                _entities.RetireEntity(other);
            byte floor = Enumerable.Range(0, _currentRoom.WidthInTiles * _currentRoom.HeightInTiles)
                .Select(i => _currentRoom.GetTerrainInfo(new(i % _currentRoom.WidthInTiles * 16 + 8, i / _currentRoom.WidthInTiles * 16 + 8)))
                .First(info => info.Collision == 0 && info.Type == TerrainType.Normal && info.Hazard == HazardType.None).Tile;
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _inventory.GiveTreasure(TreasureId.CaneOfSomaria, 1);
            _inventory.EquipA(TreasureId.CaneOfSomaria); _inventory.EquipB(0);
            _player.WarpTo(new(88, 96)); _player.Face(Vector2I.Up);
            guard.Actor.Position = new(24, 24);
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(guard.Actor.SubId != (attacking ? 0x82 : 8) || guard.DimitriCollisionMode != 0x5a ||
                guard.DimitriCollisionType != 0x54 || guard.Actor.Health != 10,
                "Actual tossing/attacking guard initialization must retain source identity $54 and patrol mode $5a/HP $0a.");
            // Hold initialized enemy/part AI, retaining the original live
            // collision scan. This isolates the block handoff from capture.
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x0c);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 88, 96);
            bool text = false;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => text;
            int updates = 0, contacts = 0;
            const int enemy = 0xd080;
            rom[enemy] = 1; rom[enemy + 1] = 0x54; rom[enemy + 2] = (byte)guard.Actor.SubId;
            rom[enemy + 0x25] = 0x5a; rom[enemy + 0x26] = rom[enemy + 0x27] = 6;
            rom[enemy + 0x28] = 0xfc; rom[enemy + 0x29] = 10; rom[enemy + 0x3e] = 1;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? 1 : 0;
                StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched, () =>
                {
                    rom[0xcba0] = text ? (byte)1 : (byte)0;
                    rom[0xcc8a] = _runtimeState.ReadWramByte(WramAddress.wDisabledObjects);
                    rom.Update(edge, press ? 1 : 0, _entities.FrameCounter); edge = 0;
                    // Declared post-AI target inputs; native item state and
                    // terrain persist independently through actual handlers.
                    rom[enemy + 0xb] = (byte)guard.Actor.Position.Y;
                    rom[enemy + 0xd] = (byte)guard.Actor.Position.X;
                    rom[enemy + 0xf] = unchecked((byte)(guard.Actor.ZFixed >> 8));
                    rom[enemy + 0x24] = guard.Actor.CollisionEnabled ? (byte)0xd4 : (byte)0x54;
                    int[] before = rom.Blocks;
                    bool unmarked = before.Any(slot => (rom[slot + 0x2f] & 0x20) == 0);
                    if (!text) rom.AdvanceEnemyAndPartCollisions();
                    if (unmarked && before.Any(slot => (rom[slot + 0x2f] & 0x20) != 0)) contacts++;
                    string context = $"Guard Somaria ${guard.Actor.SubId:x2}/{gate}, batch={batched}, update={++updates}";
                    var blocks = _entities.EntityAdapters<SomariaBlockRoomEntity>().ToArray();
                    int[] slots = rom.Blocks;
                    FailIf(blocks.Length != slots.Length, context + ": block lifetime differs.");
                    for (int i = 0; i < slots.Length; i++)
                    {
                        SomariaBlock block = blocks[i].Block; int slot = slots[i];
                        FailIf(block.State != rom[slot + 4] || block.Substate != rom[slot + 5] ||
                            block.Flags != rom[slot + 0x2f] || block.Health != rom[slot + 0x29] ||
                            (block.DamageToApply & 255) != rom[slot + 0x25] || block.ContactFlags != rom[slot + 0x2a] ||
                            block.Collision != rom[slot + 0x24] || block.Position != new Vector2(rom[slot + 0xd], rom[slot + 0xb]),
                            context + $": block state/flag differs: flags=${block.Flags:x2}/${rom[slot + 0x2f]:x2}.");
                    }
                    Vector2 point = new(88, 72); const int packed = 0x45;
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00 + packed] ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + packed] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(packed),
                        context + ": solid tile/collision/restored underlying floor differs.");
                    FailIf(guard.Actor.Health != 10 || rom[enemy + 0x29] != 10 ||
                        guard.Actor.NativeHitPending != ((rom[enemy + 0x2a] & 0x80) != 0) ||
                        guard.Actor.Seen != 0 || guard.Actor.Alert != 0 || guard.Actor.Delay != 0,
                        context + ": effect $2d must not damage or notify the guard.");
                });
            }
            try
            {
                for (int repeat = 0; repeat < (gate == "accepted" ? 3 : 1); repeat++)
                {
                    guard.Actor.Position = new(24, 24);
                    Step(1, press: true); Step(24);
                    var block = _entities.Entities<SomariaBlock>().Single();
                    // itemCode18 centers on the tile, then subtracts two Y pixels.
                    FailIf(block.State != 3 || block.Flags != 0 || block.Position != new Vector2(88, 70) ||
                        _currentRoom.GetMetatile(block.Position) != 0xda,
                        "Actual Cane input must finish phase-in and place solid tile $da before guard contact.");
                    guard.Actor.Position = block.Position + Vector2.Right * (gate == "xy" ? 32 : 8);
                    if (gate == "height") typeof(AmbiGuardCharacter).GetProperty(nameof(AmbiGuardCharacter.ZFixed),
                        BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(guard.Actor, -0x0800);
                    if (gate == "invincibility") { guard.Actor.InvincibilityCounter = 3; rom[enemy + 0x2b] = 3; }
                    if (gate == "pending") { guard.Actor.DeferNativeHitStatus(); rom[enemy + 0x2a] = 0x80; }
                    if (gate == "disabled") guard.Actor.GaleCollisionDisabled = true;
                    Step();
                    bool accepted = gate == "accepted";
                    FailIf(block.Finished || block.Flags != (accepted ? 0x20 : 0) ||
                        contacts != (accepted ? repeat + 1 : 0),
                        "Source effect $2d must mark an eligible block after objects, retaining the solid tile until the next eligible item update.");
                    text = true; Step(2); text = false;
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x1c); Step(2);
                    FailIf(block.Finished, "Text and DISABLE_ITEMS $10 must preserve a marked solid block.");
                    if (!accepted || repeat == 2)
                    {
                        guard.Actor.Position = new(24, 24);
                        _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
                    }
                    _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x0c); Step(2);
                    FailIf(!block.Finished || _currentRoom.GetMetatile(new(88, 72)) != floor ||
                        _currentRoom.GetTerrainInfo(new(88, 72)).Collision != 0,
                        "Next eligible block update must restore the floor and remove its collision exactly once.");
                    Step(16);
                }
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Validated guard patrol Somaria removal through actual Cane creation, native collision/item updates, rejection gates, freezes, tile restoration, repeat and pending cancellation.");
    }
}

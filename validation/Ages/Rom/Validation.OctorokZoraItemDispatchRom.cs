using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateOctorokItemDispatchRom() => CompareOctorokZoraItemDispatchRom(false);
    private void ValidateRiverZoraItemDispatchRom() => CompareOctorokZoraItemDispatchRom(true);

    private static int OctorokItemStun(EnemyCharacter enemy) =>
        enemy is OctorokCharacter octorok ? octorok.StunCounter : 0;

    private void CompareOctorokZoraItemDispatchRom(bool zora)
    {
        foreach (bool batched in new[] { false, true })
        foreach (int sub in zora ? new[] { 0 } : new[] { 0, 1, 2 })
        foreach (bool somaria in new[] { false, true })
        foreach (int phase in zora ? new[] { 0x0a, 0x0b, 8 } : new[] { 9 })
        {
            bool eligible = !zora || phase == 0x0b;
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, zora ? 0x43 : 0x33);
            EnemyCharacter target;
            if (zora)
            {
                target = _entities.Entities<RiverZoraCharacter>().First();
                foreach (var other in _entities.EntityAdapters<IRoomEntity>().Where(a => !ReferenceEquals(a.Node, target)).ToArray())
                    _entities.RetireEntity(other);
                _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(152, 120)); _player.BeginCutsceneControl();
                int wait = 0;
                while (((RiverZoraCharacter)target).State != phase && wait++ < 1024)
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(((RiverZoraCharacter)target).State != phase || target.CollisionEnabled != eligible,
                    $"River Zora must reach actual source state ${phase:x2} with its corresponding collision gate.");
                _player.EndCutsceneControl();
                // Keep the reached state available for repeated weapon use.
                // Source mask $04 freezes ENEMY AI, not the collision scan.
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4);
            }
            else
            {
                _entities.Clear();
                var database = EnemyDatabase.Shared;
                var source = new RoomObjectRecord(0, 0x33, 0, RoomObjectKind.FixedEnemy, 9, sub, 2, 1, 72, 112, 0x47, 0);
                var adapter = new RoomEnemyFactory(_entities, database, new RoomEntityResources(_rooms), _rooms)
                    .CreateOrderedEnemy(database.EnemyHandlers.ResolveHandler(source), source, _currentRoom,
                        new(112, 72), 0, 0, EnemyPlacementContext.Unrestricted)!;
                _entities.RegisterEnemySlot(adapter, 0); _entities.AddEntity(adapter);
                target = (OctorokCharacter)adapter.Node;
                ((OctorokCharacter)target).SetStateForValidation(OctorokState.Standing, 200, angle: ObjectAngle.Left);
            }
            byte floor = Enumerable.Range(0, _currentRoom.HeightInTiles)
                .SelectMany(y => Enumerable.Range(0, _currentRoom.WidthInTiles)
                    .Select(x => _currentRoom.GetTerrainInfo(new(x * 16 + 8, y * 16 + 8))))
                .First(info => info.Collision == 0 && info.Type == TerrainType.Normal && info.Hazard == HazardType.None).Tile;
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _inventory.GiveTreasure(somaria ? TreasureId.CaneOfSomaria : TreasureId.Boomerang, somaria ? 1 : 0);
            _inventory.EquipA(somaria ? TreasureId.CaneOfSomaria : TreasureId.Boomerang); _inventory.EquipB(0);
            _player.WarpTo(new(80, 72)); _player.Face(Vector2I.Right);
            target.Position = new(somaria ? 136 : 112, 72); target.Health = 9; target.InvincibilityCounter = 0;
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 80, 72)
                { HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            int id = zora ? 8 : 9, rawDamage = zora || sub == 2 ? 0xfc : 0xfe;
            FailIf((zora ? ((RiverZoraCharacter)target).Record.RawDamage : ((OctorokCharacter)target).Record.RawDamage) != rawDamage ||
                target.CollisionBounds.Size != new Vector2(12, 12),
                $"ENEMY ${id:x2}:${sub:x2} must import source damage ${rawDamage:x2} and radius $06/$06.");
            int edge = 0, held = 0, updates = 0, observations = 0;
            bool paused = false, cancelled = false;
            var previousText = _entities.TextActiveSource;
            _entities.TextActiveSource = () => paused;
            SomariaRom? status = null;
            int statusStart = -1;
            void CopyTarget()
            {
                const int enemy = 0xd080;
                native[enemy] = 1; native[enemy + 1] = (byte)id; native[enemy + 2] = (byte)sub;
                native[enemy + 4] = zora ? (byte)((RiverZoraCharacter)target).State : (byte)((OctorokCharacter)target).State;
                native[enemy + 6] = zora ? (byte)((RiverZoraCharacter)target).Counter : (byte)((OctorokCharacter)target).Counter1;
                native[enemy + 9] = zora ? (byte)0 : (byte)((OctorokCharacter)target).Angle;
                native.Word(enemy + 10, (int)(target.Position.Y * 256)); native.Word(enemy + 12, (int)(target.Position.X * 256));
                native[enemy + 0x24] = (byte)(id | (target.CollisionEnabled ? 0x80 : 0));
                native[enemy + 0x25] = zora ? (byte)0x0f : (byte)0x10;
                native[enemy + 0x26] = native[enemy + 0x27] = 6;
                native[enemy + 0x28] = (byte)rawDamage; native[enemy + 0x29] = (byte)target.Health;
                native[enemy + 0x2a] = target.NativeHitPending ? (byte)0x80 : (byte)0;
                native[enemy + 0x2b] = unchecked((byte)target.InvincibilityCounter);
                native[enemy + 0x2c] = (byte)target.KnockbackAngle; native[enemy + 0x2d] = (byte)target.KnockbackCounter;
                native[enemy + 0x2e] = (byte)OctorokItemStun(target); native[enemy + 0x3e] = 1;
            }
            void AddObserver() => _entities.AddEntity(new PartMovementScratchProbe(() =>
            {
                // Target post-AI state/pose is a declared boundary. Original
                // Link, item parents/children and collision code execute here.
                if (!cancelled) CopyTarget(); else native[0xd080] = 0;
                native[0xcc8a] = _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects);
                native[0xcba0] = paused ? (byte)1 : (byte)0;
                native.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter);
                observations++;
            }));
            AddObserver();
            void Step(int count = 1, bool press = false)
            {
                edge = press ? 1 : 0; held = press ? 1 : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    edge = 0;
                    string context = $"ENEMY ${id:x2}:${sub:x2} phase=${phase:x2} {(somaria ? "Somaria" : "Boomerang")}, batch={batched}, update={++updates}";
                    FailIf(observations != updates, context + ": missing source item/collision pass.");
                    if (!cancelled)
                    {
                        FailIf(target.Health != native[0xd0a9] || target.InvincibilityCounter != unchecked((sbyte)native[0xd0ab]) ||
                            target.KnockbackCounter != native[0xd0ad] || target.KnockbackAngle != native[0xd0ac] ||
                            target.NativeHitPending != ((native[0xd0aa] & 0x80) != 0) || OctorokItemStun(target) != native[0xd0ae],
                            $"{context}: contact status differs: HP={target.Health}/{native[0xd0a9]}, inv={target.InvincibilityCounter}/{unchecked((sbyte)native[0xd0ab])}, stun={OctorokItemStun(target)}/{native[0xd0ae]}, hit={target.NativeHitPending}/{native[0xd0aa]:x2}.");
                    }
                    var items = _entities.Entities<BoomerangItem>();
                    var nativeItems = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(at => native[at] != 0 && native[at + 1] == 6).ToArray();
                    FailIf(items.Count != nativeItems.Length, context + ": Boomerang allocation/catch differs.");
                    if (items.Count == 1)
                    {
                        var item = items[0]; int at = nativeItems[0];
                        FailIf(item.State != native[at + 4] || item.Counter != native[at + 6] || item.Angle != native[at + 9] ||
                            item.PrecisePosition != new Vector2(native.Word(at + 12)/256f, native.Word(at + 10)/256f) ||
                            SomariaPrivate<bool>(item, "_collisionPending") != (native[at + 0x2a] != 0),
                            context + ": Boomerang collision publication, return or catch state differs.");
                    }
                    var blocks = _entities.Entities<SomariaBlock>();
                    var nativeBlocks = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(at => native[at] != 0 && native[at + 1] == 0x18).ToArray();
                    FailIf(blocks.Count != nativeBlocks.Length, context + ": Somaria allocation/deletion differs.");
                    if (blocks.Count == 1)
                    {
                        var block = blocks[0]; int at = nativeBlocks[0];
                        FailIf(block.State != native[at + 4] || block.Substate != native[at + 5] || block.Counter != native[at + 6] ||
                            block.Flags != native[at + 0x2f] || (block.DamageToApply & 0xff) != native[at + 0x25] ||
                            block.ContactFlags != native[at + 0x2a] || block.Health != native[at + 0x29],
                            context + ": Somaria pending damage, flags or state differs.");
                    }
                    if (status is not null && updates > statusStart)
                    {
                        status[0xcba0] = paused ? (byte)1 : (byte)0;
                        status.AdvanceEnemies(_entities.FrameCounter);
                        var octorok = (OctorokCharacter)target;
                        FailIf(target.Health != status[0xd0a9] || target.InvincibilityCounter != unchecked((sbyte)status[0xd0ab]) ||
                            target.KnockbackCounter != status[0xd0ad] || OctorokItemStun(target) != status[0xd0ae] ||
                            target.Position != new Vector2(status.Word(0xd08c)/256f, status.Word(0xd08a)/256f) ||
                            octorok.Counter1 != status[0xd086] || (int)octorok.State != status[0xd084],
                            context + ": independent native stun/recoil lifetime differs.");
                    }
                });
            }
            void BeginIndependentStatus()
            {
                status = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 80, 72);
                status[0xd080] = 1; status[0xd081] = 9; status[0xd082] = (byte)sub;
                status.AdvanceEnemies(_entities.FrameCounter); // Original properties/animation initialization.
                foreach (int offset in Enumerable.Range(0, 0x11).Concat(Enumerable.Range(0x24, 0x0b)).Append(0x3e))
                    status[0xd080 + offset] = native[0xd080 + offset];
                statusStart = updates;
            }
            try
            {
                Step();
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    status = null;
                    FailIf(target.NativeHitPending || OctorokItemStun(target) != 0,
                        "Repeated weapon use must begin after the previous hit/stun handoff has completed.");
                    if (somaria) target.Position = new(136, 72);
                    Step(1, press: true);
                    if (somaria)
                    {
                        Step(24);
                        FailIf(_entities.Entities<SomariaBlock>().Count != 1,
                            $"ENEMY ${id:x2}:${sub:x2} phase=${phase:x2}, batch={batched}, repeat={repeat}: actual Cane use must create one block (updates={updates}, parent=${native[0xd284]:x2}).");
                        var block = _entities.Entities<SomariaBlock>().Single();
                        FailIf(block.State != 3, "Actual Cane input must create a solid ITEM $18 before the declared target overlap.");
                        target.Position = block.Position + Vector2.Right * 8;
                        if (!zora) ((OctorokCharacter)target).SetStateForValidation(OctorokState.Standing, 200, angle: ObjectAngle.Left);
                        Step();
                        FailIf(zora ? ((block.Flags & 0x20) != 0) != eligible || target.Health != 9 :
                            target.Health != 9 - 4 * (repeat + 1) || target.InvincibilityCounter != 21 || target.KnockbackCounter != 11,
                            "Somaria must use the species' source effect: $2d removal or $2f damage exchange.");
                        if (!zora) BeginIndependentStatus();
                        paused = true; Step(2); paused = false; Step();
                        if (zora) FailIf((_entities.Entities<SomariaBlock>().Count == 0) != eligible,
                            "Only a surfaced River Zora requests the source bit-$20 block removal.");
                        Step(24);
                    }
                    else
                    {
                        bool contacted = false;
                        for (int wait = 0; !contacted && wait < 60; wait++)
                        {
                            Step();
                            contacted = zora ? target.NativeHitPending : OctorokItemStun(target) == 120;
                        }
                        FailIf(contacted != eligible, "Actual Boomerang input must respect the source enemy's collision gate.");
                        if (!zora) BeginIndependentStatus();
                        paused = true; Step(2); paused = false;
                        if (zora && eligible)
                        {
                            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
                            Step(); // Let the real enemy update consume JUST_HIT.
                            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 4);
                            FailIf(target.NativeHitPending || ((RiverZoraCharacter)target).State != 0x0b,
                                "A surfaced River Zora must consume the hit without ending its surfaced state.");
                        }
                        if (!zora)
                        {
                            int wait = 0;
                            while (OctorokItemStun(target) != 0 && wait++ < 121) Step(2);
                            FailIf(OctorokItemStun(target) != 0, "Octorok stun must expire after 120 odd-frame decrements.");
                            Step(2);
                        }
                        else
                        {
                            int wait = 0;
                            while (_entities.Entities<BoomerangItem>().Count != 0 && wait++ < 80) Step(2);
                        }
                        FailIf(_entities.Entities<BoomerangItem>().Count != 0, "Boomerang must return and catch before repeated use.");
                    }
                }
                status = null; target.Position = new(136, 72);
                Step(1, press: true); Step(somaria ? 24 : 3);
                FailIf(somaria ? _entities.Entities<SomariaBlock>().Count != 1 : _entities.Entities<BoomerangItem>().Count != 1,
                    "Cancellation must start with an actual active weapon child.");
                _entities.ClearPhysicalPlayerItems(); native.ClearPhysicalItems();
                _entities.Clear(); native[0xd080] = 0; cancelled = true; AddObserver(); Step(2);
            }
            finally { _entities.TextActiveSource = previousText; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); }
        }
        ReinitializeGameplayForValidation();
    }
}

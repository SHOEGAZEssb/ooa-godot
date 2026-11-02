
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBombs(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var controller = ((PlayerWorld)player.World).Bombs;
        var data = new BombDatabase(rooms.AssetRoot).Data;
        CheckSharedBombNativeData(rooms.SaveData.Game,data);
        var breakables = new BreakableTileDatabase(rooms.AssetRoot);
        var app = new ApplicationValidationFixture(root);
        Vector2? origin = null;
        for (int y = 32; y <= 96 && origin is null; y += 8)
        for (int x = 24; x <= 128; x += 8)
        {
            Vector2 center = new(x,y);
            bool clear = !player.World.Collides(center) && !player.World.Collides(center + Vector2.Up) &&
                player.World.GetActiveTerrain(center).Terrain.Hazard == HazardType.None &&
                player.World.GetActiveTerrain(center + Vector2.Up).Terrain.Hazard == HazardType.None;
            foreach (Vector2 release in new[] { Vector2.Zero,Vector2.Up })
            foreach (var probe in data.BreakProbes)
            {
                Vector2 point = center + release + probe.Offset;
                clear &= !breakables.TryGet(rooms.CurrentRoom.ActiveCollisions,rooms.CurrentRoom.GetMetatile(point),out var tile) ||
                    !tile.AllowsSource(BreakableTileDatabase.SourceBomb);
            }
            if (clear) { origin = new(x,y); break; }
        }
        FailIf(origin is null,$"{rooms.SaveData.Game}: native Bomb comparison needs reachable floor without unrelated tile destruction.");
        string foreign = rooms.AssetRoot == AgesGameContent.AssetRoot ? SeasonsGameContent.AssetRoot : AgesGameContent.AssetRoot;
        bool rejected = false;
        try { _ = new BombController(inventory,entities,rooms,root._sound.PlaySound,new BombDatabase(foreign)); }
        catch (ArgumentException) { rejected = true; }
        FailIf(!rejected,"Bomb input must reject a foreign pack's item metadata.");
        foreach (bool primary in new[] { true,false })
        {
            entities.Clear(); player.WarpTo(origin!.Value); player.Face(Vector2I.Up);
            inventory.GiveTreasure(TreasureId.Bombs,0x10);
            inventory.EquipA(primary ? TreasureId.Bombs : 0); inventory.EquipB(primary ? 0 : TreasureId.Bombs);
            inventory.SetScriptedHealth(inventory.MaxHealthQuarters); session.StatusBar.SynchronizeHealth();
            session.StatusBar.SynchronizeRupees();
            var random = entities.Random.CaptureState();
            var native = new BombRom(bombs:inventory.Bombs,seed:random.Rng1 | random.Rng2 << 8,game:rooms.SaveData.Game);
            native[0xd004] = 1; native[0xd029] = 1;
            native[0xd00b] = (byte)player.Position.Y; native[0xd00d] = (byte)player.Position.X;
            native[0xc6aa] = (byte)inventory.HealthQuarters; native[0xc6ab] = (byte)inventory.MaxHealthQuarters;
            native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc30] = (byte)rooms.CurrentRoom.Id;
            native[0xcc2e] = (byte)(rooms.CurrentRoom.Width > 160 ? 1 : 0);
            native[0xcc86] = (byte)rooms.CurrentRoom.Height; native[0xcc87] = (byte)rooms.CurrentRoom.Width;
            native[0xcc33] = (byte)rooms.CurrentRoom.ActiveCollisions; native[0xcc34] = (byte)rooms.CurrentRoom.TilesetFlags;
            for (int y = 0; y < rooms.CurrentRoom.HeightInTiles; y++)
            for (int x = 0; x < rooms.CurrentRoom.WidthInTiles; x++)
            {
                Vector2 point = new(x * 16 + 8,y * 16 + 8);
                native[0xcf00 + y * 16 + x] = rooms.CurrentRoom.GetMetatile(point);
                native[0xce00 + y * 16 + x] = (byte)rooms.CurrentRoom.GetTerrainInfo(point).Collision;
            }
            var audit = root._sound.AttachPlayRequestAudit();
            string button = primary ? "attack" : "item";
            int updates = 0;
            void Compare()
            {
                var actors = entities.Entities<BombEffect>().Where(bomb => !bomb.Finished).ToArray();
                int[] slots = native.BombSlots;
                BombParentState parent = native[0xd200] == 0 ? BombParentState.Idle : native[0xd204] switch
                {
                    2 => BombParentState.Lifting, 3 => BombParentState.Holding, 4 => BombParentState.Throwing,
                    _ => throw new InvalidOperationException($"Native Bomb parent ${native[0xd204]:x2} is not represented.")
                };
                FailIf(controller.State != parent || inventory.Bombs != native[0xc6b0] || actors.Length != slots.Length ||
                    player.IsCarryingObject != (native[0xcc5a] == 0x83),
                    $"{rooms.SaveData.Game}: shared Bomb parent/ammo/allocation/carry ownership differs on update {updates}.");
                for (int index = 0; index < slots.Length; index++)
                {
                    var actor = actors[index]; int slot = slots[index];
                    BombState state = native[slot + 4] switch
                    {
                        0 or 2 => native[slot + 5] < 2 ? BombState.Held : BombState.Thrown,
                        1 => BombState.Grounded, 0xff => BombState.Exploding,
                        _ => throw new InvalidOperationException($"Native Bomb state ${native[slot + 4]:x2} is not represented.")
                    };
                    Vector2 position = new(native.Word(slot + 0x0c) / 256f,native.Word(slot + 0x0a) / 256f);
                    FailIf(actor.AssetRoot != rooms.AssetRoot || actor.State != state || actor.PrecisePosition != position ||
                        actor.ZFixed != unchecked((short)native.Word(slot + 0x0e)) || actor.AnimationCounter != native[slot + 0x20],
                        $"{rooms.SaveData.Game}: shared Bomb child differs on update {updates}: state={actor.State}/{state}, position={actor.PrecisePosition}/{position}, Z={actor.ZFixed}/{unchecked((short)native.Word(slot + 0x0e))}, animation={actor.AnimationCounter}/{native[slot + 0x20]}.");
                    if (state == BombState.Thrown)
                        FailIf(actor.SpeedZ != unchecked((short)native.Word(slot + 0x14)) || actor.SpeedRaw != native[slot + 0x10],
                            $"{rooms.SaveData.Game}: native Bomb throw motion differs on update {updates}.");
                    if (state == BombState.Exploding)
                        FailIf(actor.ExplosionRadius != native[slot + 0x26] || actor.ExplosionCollisionEnabled != ((native[slot + 0x24] & 0x80) != 0),
                            $"{rooms.SaveData.Game}: native Bomb explosion collision extent differs on update {updates}.");
                }
                var after = entities.Random.CaptureState();
                FailIf(after.Rng1 != native[0xff94] || after.Rng2 != native[0xff95],$"{rooms.SaveData.Game}: Bomb processing consumed a different native random stream.");
            }
            void Step(int count, bool press = false)
            {
                app.Step(count,Vector2.Zero,held:press ? [button] : [],pressed:press ? [button] : [],batched:batched,
                    afterUpdate:() => { native.Update(press,press,primary); updates++; Compare(); });
            }
            Step(1,true); Step(19);
            FailIf(controller.State != BombParentState.Holding,"Both file-entry worlds must reach the common native Bomb holding state.");
            root._scene.Dialogue.ShowMessage("Bomb fuse pause.",player.Position.Y); native[0xcba0] = 1;
            Step(5);
            root._scene.Dialogue.Close(); native[0xcba0] = 0;
            if (!primary) Step(1,true);
            Step(140);
            FailIf(controller.Active || entities.Entities<BombEffect>().Any() || inventory.HealthQuarters != native[0xc6aa],
                $"{rooms.SaveData.Game}: Bomb completion must release the parent/child and preserve native own-explosion damage.");
            int[] sounds = audit.Requests.Where(request => request == data.PickupSound || request == data.ThrowSound ||
                request == data.LandingSound || request == data.ExplosionSound).ToArray();
            FailIf(!sounds.SequenceEqual(native.Sounds),$"{rooms.SaveData.Game}: Bomb lifecycle cue order differs, A={primary}: runtime={string.Join(',',sounds.Select(id => $"${id:x2}"))}, native={string.Join(',',native.Sounds.Select(id => $"${id:x2}"))}.");
        }

        inventory.GiveTreasure(TreasureId.Bombs, 0x10);
        inventory.EquipA(TreasureId.Bombs);
        inventory.EquipB(0);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            player.WarpTo(origin!.Value);
            app.Step(1, Vector2.Zero, held: ["attack"], pressed: ["attack"], batched: batched);
            app.Step(19, Vector2.Zero, batched: batched);
            FailIf(controller.State != BombParentState.Holding || !player.IsCarryingObject,
                $"{rooms.SaveData.Game}: completed Bomb input must remain reusable before a room reset.");
            BombEffect held = controller.Bomb!;
            player.WarpTo(origin.Value);
            FailIf(controller.Active || player.IsCarryingObject || !held.Finished,
                $"{rooms.SaveData.Game}: the shared room-reset path must cancel the held Bomb and release its parent.");
            app.Step(20, Vector2.Zero, batched: batched);
            FailIf(entities.Entities<BombEffect>().Any() || inventory.Bombs != (attempt == 0 ? 0x09 : 0x08),
                $"{rooms.SaveData.Game}: cancelled Bombs must retire without leaking slots or consuming extra ammo.");
        }
    }

    private static void CheckSharedBombNativeData(OracleGame game, BombRecord data)
    {
        var rom = ValidationRom.LoadCleanUs(game);
        int weightsAt = 7 * 0x4000 + (game == OracleGame.Ages ? 0x24ba : 0x2258);
        int offsetsAt = 6 * 0x4000 + (game == OracleGame.Ages ? 0x151e : 0x1468);
        var weights = BraceletWeightDatabase.For(data.AssetRoot);
        for (int weight = 0; weight < 6; weight++)
        {
            var actual = weights.Weight(weight); int at = weightsAt + weight * 4;
            FailIf(actual.Gravity != rom.Span[at] || actual.InitialSpeedZ != rom.Span[at + 1] - 256 ||
                actual.SpeedRaw != rom.Span[at + 2] || actual.TossSpeedRaw != rom.Span[at + 3],
                $"{game}: shared carried-object weight ${weight:x2} differs from native itemWeights.");
        }
        for (int weight = 0; weight < 5; weight++)
        for (int frame = 0; frame < 4; frame++)
        for (int direction = 0; direction < 4; direction++)
        {
            int at = offsetsAt + weight * 32 + frame * 8 + direction * 2;
            Vector2I expected = new(unchecked((sbyte)rom.Span[at + 1]),unchecked((sbyte)rom.Span[at]));
            FailIf(weights.LiftOffset(weight,frame,direction) != expected,
                $"{game}: native carried-object Z/X offset differs at ${weight:x2}/${frame:x2}/${direction:x2}.");
        }
        FailIf(data.HazardGroup != (game == OracleGame.Ages ? 0 : 4) || data.HazardRoom != (game == OracleGame.Ages ? 0x50 : 0xef) ||
            data.HazardSignal != 0xcfc0 || data.Collision != (game == OracleGame.Ages ? 0x18 : 0x17),
            $"{game}: Bomb collision identity or native landed-hazard signal borrowed the other game.");
        int pointers = 7 * 0x4000 + (game == OracleGame.Ages ? 0x2723 : 0x24c1);
        int oam = 7 * 0x4000 + (game == OracleGame.Ages ? 0x28bc : 0x265a);
        int Word(int at) => rom.Span[at] | rom.Span[at + 1] << 8;
        string[] definitions = [data.FuseAnimation,data.ExplosionAnimation];
        for (int animation = 0; animation < 2; animation++)
        {
            int start = 7 * 0x4000 + (Word(pointers + animation * 2) & 0x3fff);
            var frames = OracleGraphicsCache.GetAnimationDefinition(definitions[animation]).Frames;
            FailIf(frames.Length != (animation == 0 ? 11 : 7),$"{game}: Bomb animation extent differs.");
            for (int frame = 0; frame < frames.Length; frame++)
            {
                int at = start + frame * 3;
                int cells = (game == OracleGame.Ages ? 0x13 : 0x12) * 0x4000 + (Word(oam + rom.Span[at + 1]) & 0x3fff);
                var encoded = new System.Collections.Generic.List<string>();
                for (int cell = 0; cell < rom.Span[cells]; cell++)
                {
                    int offset = cells + 1 + cell * 4;
                    encoded.Add($"{rom.Span[offset]},{rom.Span[offset + 1]},{rom.Span[offset + 2]},{rom.Span[offset + 3]}");
                }
                FailIf(frames[frame].Duration != rom.Span[at] || frames[frame].Parameter != rom.Span[at + 2] ||
                    frames[frame].EncodedOam != string.Join(';',encoded),
                    $"{game}: native Bomb animation/OAM differs at ${animation:x2}/${frame:x2}.");
            }
        }
    }
}

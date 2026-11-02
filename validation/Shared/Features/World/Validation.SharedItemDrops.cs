
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedItemDrops(ApplicationValidationFixture app, Player player, RoomSession rooms,
        RoomEntityManager entities, OracleSoundEngine sound, bool batched)
    {
        var memory = rooms.RuntimeState;
        bool seasons = memory.Game == OracleGame.Seasons;
        string context = $"{memory.Game}, batched={batched}";
        var data = entities.ItemDrops;
        var save = rooms.SaveData;
        foreach (var (id, expected) in new (int,int)[] { (0x0f,seasons ? 0xff : 0x85), (0x3c,seasons ? 0xae : 0x60),
            (0x42,seasons ? 0x44 : 0x85), (0x64,seasons ? 0xff : 0xe0), (0x7e,seasons ? 0xef : 0xff) })
            FailIf(data.EnemyTableRecord(id) != expected, $"{context}: native itemDropTables row ${id:x2} changed.");
        var detached = OracleSaveData.CreateStandardGame(memory.Game);
        FailIf(data.ChooseDrop(0x81,3,0,saveData:detached) != ItemDropDatabase.Heart ||
            data.ChooseDrop(0x81,0,0,saveData:detached) is not null,
            $"{context}: native breakable table $81 / probability2 / set1 boundaries changed.");
        detached.SetMinimapLocation(1,0);
        FailIf(data.ResolveAvailableDrop(ItemDropDatabase.Fairy,saveData:detached) != (seasons ? 1 : 0) ||
            data.ResolveAvailableDrop(ItemDropDatabase.Heart,saveData:detached) != (seasons ? 0x0c : 1) ||
            data.ResolveAvailableDrop(ItemDropDatabase.FiftyOreChunks,saveData:detached) != (seasons ? 0x0e : null),
            $"{context}: Subrosia remapping must precede native availability checks; Ages must retain its own rules.");
        detached.SetMinimapLocation(3,0);
        FailIf(data.ResolveAvailableDrop(ItemDropDatabase.Heart,saveData:detached) != 1 ||
            data.ResolveAvailableDrop(ItemDropDatabase.OneOreChunk,saveData:detached) != (seasons ? 0x0c : null),
            $"{context}: ore availability tests minimap bit0, while remapping requires group $01 exactly.");
        FailIf(data.Rule(1).Parameter != 4 || data.Rule(1).DoubledParameter != 8 ||
            data.Rule(0x0e).Treasure != 0x37 || data.Rule(0x0e).Parameter != 0x0b || data.Rule(0x0e).DoubledParameter != 0x0c ||
            data.GetVisual(15).Animation != "127@8,0,0,0;8,8,2,0",
            $"{context}: native heart/ore rewards or two-cell $0f OAM changed.");
        if (seasons)
        {
            var withoutLocation = new InventoryState(TreasureDatabase.For(rooms.AssetRoot));
            withoutLocation.GiveTreasure(0x37,1);
            FailIf(data.ResolveAvailableDrop(0x0c,withoutLocation) is not null,
                "An obtained ore treasure flag must not substitute for native Subrosia minimap availability.");
        }

        Vector2? dry = null;
        for (int y = 32; y < 104 && dry is null; y += 8)
        for (int x = 24; x < 104; x += 8)
        {
            var start = new Vector2(x,y);
            bool clear = true;
            for (int step = 0; step <= 32; step++)
                clear &= !player.World.Collides(start + Vector2.Right * step) &&
                    player.World.GetActiveTerrain(start + Vector2.Right * step).Terrain.Hazard == HazardType.None;
            if (clear) { dry = start; break; }
        }
        FailIf(dry is null,$"{context}: pickup fixture requires a native dry walking approach.");
        var audit = sound.AttachPlayRequestAudit();
        var inventory = player.Inventory;
        int originalRing = inventory.ActiveRing;
        inventory.SetScriptedActiveRing(0xff);
        player.WarpTo(dry!.Value);
        inventory.SetScriptedHealth(4);
        int maturity = save.GashaMaturity;
        var heart = entities.Spawn<ItemDropEffect>(new ItemDropSpawn(1,dry.Value + Vector2.Right * 24));
        Step(1);
        FailIf(heart.State != DropState.Bouncing || heart.SpeedZ != -0x160 || heart.ZFixed != 0 || audit.Requests.Count != 0,
            $"{context}: PART$01 must initialize before integrating height, enabling collection or emitting a cue.");
        AwaitGround(heart);
        WalkToCollect(heart);
        Step(16); // The HUD fills four quarters on global four-update boundaries before its cue.
        FailIf(inventory.HealthQuarters != 8 || save.GashaMaturity != maturity + 4 ||
            !inventory.HasTreasure(TreasureId.HeartRefill) || audit.RequestsFor(SoundId.SndGainHeart) != 1,
            $"{context}: real heart collection must use native giveTreasure, including obtained flags and Gasha maturity: " +
            $"health={inventory.HealthQuarters}, maturity={save.GashaMaturity}/{maturity}, obtained={inventory.HasTreasure(TreasureId.HeartRefill)}, cues={string.Join(',',audit.Requests)}.");
        entities.Clear();
        // A full PART pool still consumes the native probability/item rolls.
        var randomProbe = new OracleRandom(); randomProbe.RestoreState(entities.Random.CaptureState());
        int roll = randomProbe.Next().Value & 63;
        byte[] probability = [0x08,0x10,0x08,0x20,0x80,0x01,0x80,0x10];
        int expectedCalls = 1 + ((probability[roll / 8] & (1 << (roll & 7))) != 0 ? 1 : 0);
        for (int slot = 0; slot < 16; slot++)
            entities.Spawn<ItemDropEffect>(new ItemDropSpawn(1,dry.Value + Vector2.Right * 24));
        int calls = entities.Random.Calls;
        entities.SpawnBreakableDrop(1,dry.Value + Vector2.Right * 24);
        FailIf(entities.PartSlotAvailable || entities.Entities<ItemDropEffect>().Count != 16 || entities.Random.Calls != calls + expectedCalls,
            $"{context}: drop selection must consume source RNG before the failed PART allocation.");
        entities.Clear();
        audit.Clear();
        entities.AddEntity(new QueuedDropHazardProbe(dry.Value));
        for (int slot = 0; slot < 13; slot++) entities.Spawn<GrassDebrisEffect>(new GrassDebrisSpawn(dry.Value));
        Step(1);
        FailIf(entities.Entities<FallingDownHoleEffect>().Count != 0 || entities.Entities<SplashEffect>().Count != 0 ||
            audit.RequestsFor(SoundId.SndFallInHole) != 0 || audit.RequestsFor(SoundId.SndSplash) != 0,
            $"{context}: committing queued drop hazard requests must honor a full native INTERACTION pool without effects or cues.");
        entities.Clear();

        if (seasons)
        {
            inventory.SetScriptedActiveRing((int)RingId.GreenJoy);
            int ore = inventory.OreChunks, rupees = inventory.Rupees;
            save.SetRoomFlag(rooms.ActiveGroup,rooms.CurrentRoom.Id,0x20,false);
            player.WarpTo(dry.Value);
            var drop = entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0e,dry.Value + Vector2.Right * 24));
            AwaitGround(drop);
            WalkToCollect(drop);
            FailIf(inventory.OreChunks != ore + 100 || inventory.Rupees != rupees ||
                !save.HasRoomFlag(rooms.ActiveGroup,rooms.CurrentRoom.Id,0x20),
                "Seasons native $0e with Green Joy must grant 100 ore, retain rupees and set room flag$20.");
            entities.Clear();
            inventory.SetScriptedActiveRing(0xff);
            player.WarpTo(dry.Value);
            memory.SetField("wMagnetGloveState",1);
            var pulled = entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0c,dry.Value + Vector2.Right * 24));
            Step(2);
            FailIf(pulled.PrecisePosition != dry.Value + Vector2.Right * 21.5f,
                $"Seasons ore Magnet pull must apply native SPEED_280 in its first bounce update: {pulled.PrecisePosition}.");
            memory.SetField("wDisabledObjects",8);
            Vector2 frozen = pulled.PrecisePosition;
            Step(4);
            FailIf(pulled.PrecisePosition != frozen,"Seasons ore movement must respect the shared initialized PART freeze gate.");
            memory.SetField("wDisabledObjects",0);
            memory.SetField("wMagnetGloveState",0);
            Step(1);
            FailIf(pulled.PrecisePosition != frozen,"Clearing native Magnet state must stop ore pulling before the next eligible update.");
            entities.Clear();
        }
        inventory.SetScriptedActiveRing(originalRing);

        // Shared drop retirement requests its own checked splash, with no
        // game-specific terrain event subscription.
        player.WarpTo(dry.Value);
        audit.Clear();
        entities.OnItemDropEnteredHazard(dry.Value + Vector2.Right * 24,HazardType.Water);
        Step(1);
        FailIf(entities.Entities<SplashEffect>().Count != 1 || audit.RequestsFor(SoundId.SndSplash) != 1,
            $"{context}: the common drop hazard owner must dispatch one native splash.");
        entities.Clear();
        var hole = entities.Spawn<FallingDownHoleEffect>(new FallingDownHoleSpawn(dry.Value + Vector2.Right * 24));
        Step(34);
        FailIf(!hole.Finished || entities.Entities<FallingDownHoleEffect>().Count != 0,
            $"{context}: common falling-hole construction and terminal retirement must work in both packs.");
        entities.Clear();

        void Step(int count) => app.Step(count,Vector2.Zero,batched:batched);
        void AwaitGround(ItemDropEffect drop)
        {
            for (int update = 0; update < 64 && drop.State != DropState.Grounded; update++) Step(1);
            FailIf(drop.State != DropState.Grounded || !drop.CollisionEnabled,$"{context}: a native dry drop failed to finish bouncing.");
        }
        void WalkToCollect(ItemDropEffect drop)
        {
            for (int update = 0; update < 32 && !drop.Collected; update++) app.Step(1,Vector2.Right,batched:batched);
            FailIf(!drop.Collected,$"{context}: native drop was not collectible through actual dry-room geometry.");
        }
    }

    private sealed class QueuedDropHazardProbe(Vector2 position) : INativeInteractionRoomEntity, IFixedRoomEntity
    {
        public Node2D? PresentationNode => null;
        public void SetTransitionDrawOffset(Vector2 offset) { }
        public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        {
            spawns.Add(new FallingDownHoleSpawn(position));
            spawns.Add(new EnemySplashSpawn(position,HazardType.Water));
        }
    }
}


namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedObjectDispatcher()
    {
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x45);
            _entities.Clear();
            Check(new ApplicationValidationFixture(this), _entities, _rooms, _runtimeState);

            var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect();
                var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
                save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0, 0xb6, 3, 2, 0x48, 0x78);
                save.SetRoomFlag(0, 0xb6, 0x50);
                root.SeedFileSlot(0, save);
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, batched);
                root.CompleteBootLoading();
                root.Step(1, batched); root.Step(1, batched, "inventory"); root.Step(32, batched);
                root.Step(1, batched, "attack"); root.Step(1, batched);
                root.Step(1, batched, "attack"); root.Step(32, batched);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons file entry omitted gameplay.");
                session.Entities.Clear();
                Check(new ApplicationValidationFixture(root), session.Entities, session.Rooms, session.Memory);
            }
            finally { root.Free(); }

            void Check(ApplicationValidationFixture app, RoomEntityManager entities, RoomSession rooms, OracleRuntimeState memory)
            {
                var order = new List<string>();
                var probe = new ObjectDispatcherProbe(rooms, memory, order);
                byte mountingLock = memory.ReadField("wDisallowMountingCompanion");
                entities.AddEntity(probe);
                try
                {
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        order.Clear();
                        memory.SetField("wDisallowMountingCompanion", 0x5a);
                        // bank0.updateAllObjects: the special/Link pass clears
                        // signals before interactions; grabbed objects follow
                        // interactions, before animation/pushing and tile VRAM.
                        app.Step(4, Vector2.Zero, batched: batched, afterUpdate: () =>
                        {
                            FailIf(!order.SequenceEqual(new[] { "signals", "interaction", "held" }) ||
                                rooms.PendingTileGraphics != 1 || memory.ReadField("wLinkPushingDirection") != 0xff,
                                $"{memory.Game}: real object update must clear signals, run interactions and place held children before publishing pushing/at most four queued tiles; " +
                                $"order={string.Join(',', order)}, queued={rooms.PendingTileGraphics}, push=${memory.ReadField("wLinkPushingDirection"):x2}.");
                            rooms.UpdateChangedTileGraphics(1);
                            order.Clear();
                        });
                    }
                }
                finally
                {
                    entities.RetireEntity(probe);
                    memory.SetField("wDisallowMountingCompanion", mountingLock);
                }
            }
        }
    }

    private sealed class ObjectDispatcherProbe(RoomSession rooms, OracleRuntimeState memory, List<string> order)
        : INativeInteractionRoomEntity, IFixedRoomEntity, IAfterPlayerUpdateRoomEntity, IHeldObjectPositionRoomEntity
    {
        public Node2D? PresentationNode => null;
        public void SetTransitionDrawOffset(Vector2 offset) { }
        public void AfterPlayerUpdate()
        {
            // specialObjects.s clears this byte only under ROM_AGES.
            if (memory.ReadField("wDisallowMountingCompanion") != (memory.Game == OracleGame.Ages ? 0 : 0x5a))
                throw new InvalidOperationException("The shared special-object pass lost the native Ages-only mounting-lock clear.");
            order.Add("signals");
        }
        public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
        {
            order.Add("interaction");
            memory.SetField("wLinkPushingDirection", 0x42);
            for (byte position = 0; position < 5; position++)
            {
                byte tile = rooms.CurrentRoom.GetMetatile(new Vector2(position * 16 + 8, 8));
                if (!rooms.TrySetTile(position, tile)) throw new InvalidOperationException("The five-entry native tile queue setup failed.");
            }
        }
        public void UpdateHeldPosition(Player player)
        {
            if (rooms.PendingTileGraphics != 5 || memory.ReadField("wLinkPushingDirection") != 0x42)
                throw new InvalidOperationException("Held positioning must precede tile graphics and Link's animation signal publication.");
            order.Add("held");
        }
    }
}

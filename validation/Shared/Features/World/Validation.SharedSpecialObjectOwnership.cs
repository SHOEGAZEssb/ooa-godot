
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedSpecialObjectOwnership()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x45);
            Check(new ApplicationValidationFixture(this), _entities);
            var root = new FrontendValidationRoot(); AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect();
                var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
                save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0, 0xb6, 3, 2, 0x48, 0x78); save.SetRoomFlag(0, 0xb6, 0x50);
                root.SeedFileSlot(0, save);
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, false); root.CompleteBootLoading();
                root.Step(1, false); root.Step(1, false, "inventory"); root.Step(32, false);
                root.Step(1, false, "attack"); root.Step(1, false); root.Step(1, false, "attack"); root.Step(32, false);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Missing Seasons gameplay session.");
                Check(new ApplicationValidationFixture(root), session.Entities);
            }
            finally { root.Free(); }

            void Check(ApplicationValidationFixture app, RoomEntityManager entities)
            {
                var memory = entities.RuntimeState;
                entities.Clear();
                CompanionRuntimeState.Clear(memory, CompanionRuntimeState.Read(memory).Id);
                memory.SetWramByte(0xd0ff, 0xa5); memory.SetWramByte(0xd140, 0x5a);
                memory.SetWramByte(0xd10a, 0x40); memory.SetWramByte(0xd10c, 0xc0);
                memory.SetWramByte(0xd13d, 0x96); memory.SetWramByte(0xd13e, 0x69);
                var old = new SpecialObjectSlotValidationEntity(0x0b, memory) { NativeSpecialObjectPosition = new(72, 64) };
                entities.AddEntity(old);
                FailIf(memory.ReadWramByte(0xd100) != 1 || memory.ReadWramByte(0xd101) != 0x0b ||
                    old.NativeSpecialObjectPosition != new Vector2(72.75f, 64.25f) ||
                    memory.ReadWramByte(0xd13d) != 0x96 || memory.ReadWramByte(0xd13e) != 0x69,
                    $"{memory.Game}: an idle source birth must bind its real slot, preserve lows/script bytes and publish the high-coordinate input.");
                byte[] before = memory.CaptureState().Wram;
                bool rejected = false;
                try { entities.Spawn(new DimitriCompanionSpawn(new(88, 88), 2, 0, 0x45)); }
                catch (InvalidOperationException error) when (error.Message.Contains("companionSpawner")) { rejected = true; }
                FailIf(!rejected || !before.SequenceEqual(memory.CaptureState().Wram),
                    $"{memory.Game}: checked allocation must fail before content construction or native scratch writes.");
                rejected = false;
                try { entities.AddEntity(old); }
                catch (InvalidOperationException error) when (error.Message.Contains("already bound")) { rejected = true; }
                FailIf(!rejected, "The same native body cannot be attached twice.");

                memory.SetWramByte(0xd110, 0x28);
                entities.SetSpecialObjectBarrierCoordinate(horizontal: true, 0x39);
                entities.SetSpecialObjectBarrierCoordinate(horizontal: false, 0x49);
                FailIf(old.NativeSpecialObjectPosition != new Vector2(57.75f, 73.25f) ||
                    memory.ReadWramByte(0xd10c) != 0xc0 || memory.ReadWramByte(0xd10d) != 0x39 ||
                    memory.ReadWramByte(0xd10a) != 0x40 || memory.ReadWramByte(0xd10b) != 0x49 ||
                    memory.ReadWramByte(0xd110) != 0 || memory.ReadWramByte(0xd13e) != 0x69,
                    "A native barrier write must project into the actual reserved body, retain fractions/script bytes and clear speed.");

                old.Finished = true;
                FailIf(!entities.SpecialObjectSlotAvailable, "A completed owner must release the native allocation before its list entry is removed.");
                var successor = new SpecialObjectSlotValidationEntity(0x0b, memory) { NativeSpecialObjectPosition = new(88, 72) };
                entities.AddEntity(successor);
                app.Step(2, Vector2.Zero, batched: batched);
                FailIf(old.Updates != 0 || successor.Updates != 2 || memory.ReadWramByte(0xd100) != 1 ||
                    !entities.EntityAdapters<IRoomEntity>().Contains(successor),
                    "Only the bound body may dispatch; retiring a finished same-ID predecessor must preserve its successor.");

                // commonCode.s:companionRetIfInactive reads $a0; masks for
                // interactions, enemies and parts cannot freeze SPECIALOBJECTs.
                foreach (byte mask in new byte[] { 2, 4, 8 })
                {
                    int updates = successor.Updates;
                    memory.SetField("wDisabledObjects", mask);
                    app.Step(3, Vector2.Zero, batched: batched);
                    FailIf(successor.Updates != updates + 3,
                        $"{memory.Game}: category mask${mask:x2} must preserve the real special-object pass.");
                }
                memory.SetField("wDisabledObjects", 0);

                // commonCode.s: an admitted animal visit runs its terrain tail,
                // including raw var3c warp publication and negative-Z visibility.
                memory.SetWramByte(0xd10f, 0xff); memory.SetWramByte(0xd11a, 0x82);
                memory.SetWramByte(0xd13c, 0x5a); memory.SetField("wWarpsDisabled", 0);
                app.Step(1, Vector2.Zero, batched: batched);
                FailIf(memory.ReadField("wWarpsDisabled") != 0x5a || memory.ReadWramByte(0xd11a) != 0xc2 || memory.ReadWramByte(0xd10f) != 0xff,
                    $"{memory.Game}: the actual shared special-object pass omitted the animal terrain/warp tail.");
                memory.SetWramByte(0xd10f, 0); memory.SetWramByte(0xd11a, 0);
                memory.SetWramByte(0xd13c, 0); memory.SetField("wWarpsDisabled", 0);

                CompanionRuntimeState.BeginIdle(memory, 0x0c, new(104, 80));
                memory.SetWramByte(0xd103, 0x23); memory.SetWramByte(0xd13e, 0x5c);
                byte[] overwrite = Enumerable.Range(0, 0x40).Select(offset => memory.ReadWramByte(0xd100 + offset)).ToArray();
                var overwritten = new SpecialObjectSlotValidationEntity(0x0c, memory);
                entities.AddEntity(overwritten);
                entities.RetireEntity(successor);
                FailIf(entities.EntityAdapters<IRoomEntity>().Contains(successor) ||
                    !overwrite.SequenceEqual(Enumerable.Range(0, 0x40).Select(offset => memory.ReadWramByte(0xd100 + offset))),
                    "An explicit native overwrite transfers presentation without clearing the successor's object union.");
                Vector2 retiredPosition = successor.NativeSpecialObjectPosition;
                entities.SetSpecialObjectBarrierCoordinate(horizontal: true, 0x49);
                FailIf(overwritten.NativeSpecialObjectPosition != new Vector2(73, 80) ||
                    successor.NativeSpecialObjectPosition != retiredPosition || memory.ReadWramByte(0xd10d) != 0x49,
                    "A retained consumer must address the current native body after overwrite, leaving its retired predecessor untouched.");
                entities.RetireEntity(overwritten);
                FailIf(Enumerable.Range(0, 0x40).Any(offset => memory.ReadWramByte(0xd100 + offset) != 0) ||
                    memory.ReadWramByte(0xd0ff) != 0xa5 || memory.ReadWramByte(0xd140) != 0x5a,
                    "Actual reserved-owner deletion must clear exactly its native $40 bytes.");

                CompanionRuntimeState.BeginCutscene(memory, 0x11, 0, new(24, 40));
                var credits = new SpecialObjectSlotValidationEntity(0x11, memory, credits: true);
                entities.AddEntity(credits);
                CompanionRuntimeState.ReplaceCutscene(memory, 0x10, 0, new(40, 56));
                FailIf(entities.SpecialObjectPresentationAvailable || !entities.EntityAdapters<IRoomEntity>().Contains(credits),
                    "A native same-slot credits replacement must retain its existing body reference.");
                CompanionRuntimeState.BeginCutscene(memory, 0x12, 0, new(56, 72));
                var maple = new SpecialObjectSlotValidationEntity(0x12, memory);
                entities.AddEntity(maple);
                FailIf(entities.EntityAdapters<IRoomEntity>().Contains(credits) || memory.ReadWramByte(0xd101) != 0x12,
                    "A credits body must not claim a successor merely by reading the successor's raw ID.");
                entities.RetireEntity(maple);

                var retained = new SpecialObjectSlotValidationEntity(0x0c, memory) { NativeSpecialObjectPosition = new(72, 64) };
                entities.AddEntity(retained);
                CompanionRuntimeState.Begin(memory, 0x0c, new(72.75f, 64.25f), 1);
                entities.Clear();
                FailIf(entities.SpecialObjectSlotAvailable || !entities.SpecialObjectPresentationAvailable || memory.ReadWramByte(0xd100) != 3,
                    "Retained memory and a missing presentation must remain distinct from a free native slot.");
                var restored = new SpecialObjectSlotValidationEntity(0x0c, memory);
                entities.AddEntity(restored);
                FailIf(restored.NativeSpecialObjectPosition != new Vector2(72.75f, 64.25f),
                    "Reconstruction must adopt the native fixed words without another birth write.");
                entities.RetireEntity(restored);
            }
        }
    }
}

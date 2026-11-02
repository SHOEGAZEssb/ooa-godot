
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedRememberedInitialization(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms; var save = rooms.SaveData;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var memory = rooms.RuntimeState; var player = root._scene.Player;
        var app = new ApplicationValidationFixture(root);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id, kills = save.MapleKillCounter;
        var remembered = CompanionRuntimeState.ReadRemembered(memory);
        byte load = save.Game == OracleGame.Ages ? memory.ReadField("wcc05") : (byte)0;
        byte mountY = (byte)save.LastAnimalMountY, mountX = (byte)save.LastAnimalMountX;
        string[] stateFields = ["wCompanionStates", "wDimitriState", "wMooshState"];
        byte[] stateValues = save.Game == OracleGame.Ages ? stateFields.Select(field => save.ReadField(field)).ToArray() : [];
        byte dimitriState=save.ReadField("wDimitriState");
        byte mooshState=save.ReadField("wMooshState");
        byte rickyState=save.ReadField("wRickyState");
        void ClearSlot()
        {
            entities.Clear();
            for (int address = 0xd100; address < 0xd140; address++) memory.SetWramByte(address, 0);
            memory.SetField("wcc85", 0); memory.SetField("wDisabledObjects", 0);
        }
        try
        {
            ClearSlot(); CompanionRuntimeState.ForgetRemembered(memory); save.SetMapleKillCounter(0);
            if (save.Game == OracleGame.Ages)
            {
                foreach (string field in stateFields) save.WriteField(field, 0x80);
                root.LoadDebugRoom(0, 1);
            }
            else root._seasonsOpening!.DebugTravel(0, 0x12);
            var room = rooms.CurrentRoom;
            Vector2[] floor = Enumerable.Range(1, room.HeightInTiles - 2)
                .SelectMany(y => Enumerable.Range(1, room.WidthInTiles - 2).Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .Where(position => !player.World.Collides(position) && room.GetTerrainInfo(position + new Vector2(0, 5)).Collision == 0).ToArray();
            Vector2 point = floor.First();
            foreach (int id in save.Game == OracleGame.Ages ? new[] { 0x0b, 0x0c, 0x0d } : new[] { 0x0b, 0x0c, 0x0d, 0x13 })
            {
                ClearSlot();
                memory.SetWramByte(0xd10a, 0x40); memory.SetWramByte(0xd10c, 0xc0);
                CompanionRuntimeState.Remember(memory, id, rooms.ActiveGroup, room.Id, point);
                save.SetLastAnimalMountPoint(0x91, 0x72);
                if (save.Game == OracleGame.Ages)
                {
                    memory.SetField("wcc05", 1);
                    entities.LoadRoom(rooms.ActiveGroup, room);
                    FailIf(CompanionRuntimeState.AnyActive(memory) || entities.EntityAdapters<IRoomEntity>().OfType<IRoomInitializedCompanion>().Any(),
                        "Ages initializeRoom bit2 must gate remembered admission before body construction.");
                    memory.SetField("wcc05", 5);
                    var native = new BraceletRom(room, player.Position, 2, 0xff);
                    native.LoadSave(save); native[0xcc2d] = (byte)rooms.ActiveGroup;
                    for (int offset = 0; offset < 5; offset++) native[0xcc24 + offset] = memory.ReadWramByte(0xcc24 + offset);
                    for (int address = 0xd100; address < 0xd140; address++) native[address] = memory.ReadWramByte(address);
                    native.LoadRememberedCompanion();
                    entities.LoadRoom(rooms.ActiveGroup, room);
                    for (int address = 0xd100; address < 0xd140; address++)
                        FailIf(memory.ReadWramByte(address) != native[address], $"Remembered ${id:x2} native byte ${address:x4} differs before the first gameplay update.");
                    FailIf(!entities.EntityAdapters<IRoomEntity>().OfType<IRoomInitializedCompanion>().Single().RoomInitialization!.Pending,
                        "Remembered admission must not execute native state0 during construction.");
                    player.WarpTo(floor.Last());
                    memory.SetField("wDisabledObjects", 0x20);
                    app.Step(1, Vector2.Zero, batched: batched);
                    FailIf(memory.ReadWramByte(0xd104) != 0 || memory.ReadWramByte(0xd105) != 1,
                        $"Remembered ${id:x2}: Ages state0 must take its first reservation yield even under the companion mask.");
                    app.Step(1, Vector2.Zero, batched: batched);
                    FailIf(memory.ReadWramByte(0xd100) != 1 || memory.ReadWramByte(0xd101) != id || memory.ReadWramByte(0xd104) != 1,
                        $"Remembered ${id:x2}: the next eligible state0 visit must initialize and transfer the native body exactly once.");
                    var position = CompanionRuntimeState.Read(memory).Position;
                    app.Step(3, Vector2.Zero, batched: batched);
                    FailIf(CompanionRuntimeState.Read(memory).Position != position || memory.ReadWramByte(0xd104) != 1,
                        $"Remembered ${id:x2}: initialized state1 must obey the live companion mask.");
                    memory.SetField("wDisabledObjects", 0);
                    app.Step(1, Vector2.Zero, batched: batched);
                    FailIf(!CompanionRuntimeState.IsActive(memory, id), "The initialized companion must survive mask release.");
                    player.EndCutsceneControl();
                }
                else if(id is 0x0b or 0x0c or 0x0d)
                {
                    save.WriteField(id==0x0b?"wRickyState":id==0x0c?"wDimitriState":"wMooshState",0x80);
                    var native=new BraceletRom(room,player.Position,2,0xff,game:OracleGame.Seasons);
                    native.LoadSave(save);native[0xcc2d]=(byte)rooms.ActiveGroup;
                    for(int offset=0;offset<5;offset++)native[0xcc24+offset]=memory.ReadWramByte(0xcc40+offset);
                    for(int at=0xd100;at<0xd140;at++)native[at]=memory.ReadWramByte(at);
                    native.LoadRememberedCompanion();entities.LoadRoom(rooms.ActiveGroup,room);
                    for(int at=0xd100;at<0xd140;at++)FailIf(memory.ReadWramByte(at)!=native[at],
                        $"Seasons remembered ${id:x2} byte${at:x4} differs before state0.");
                    FailIf(!entities.EntityAdapters<IRoomEntity>().OfType<IRoomInitializedCompanion>().Single().RoomInitialization!.Pending,
                        $"Seasons remembered ${id:x2} must bind its native pending state0 owner before dispatch.");
                    player.WarpTo(floor.Last());native.Word(0xd00a,(int)(player.PrecisePosition.Y*256));native.Word(0xd00c,(int)(player.PrecisePosition.X*256));
                    memory.SetField("wDisabledObjects",0x20);native[0xcc8a]=0x20;
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:()=>native.UpdateLinkItems(entities.FrameCounter,0));
                    FailIf(memory.ReadWramByte(0xd104)!=native[0xd104] || native[0xd104]!=1 ||
                        entities.EntityAdapters<IRoomEntity>().Count(actor=>id==0x0b?actor is RickyCompanionRoomEntity:
                            id==0x0c?actor is DimitriCompanionRoomEntity:actor is MooshCompanionRoomEntity)!=1,
                        $"Seasons remembered ${id:x2} must initialize and transfer in its first eligible visit, without the Ages reservation yield.");
                    Vector2 position=CompanionRuntimeState.Read(memory).Position;
                    app.Step(3,Vector2.Zero,batched:batched);
                    FailIf(CompanionRuntimeState.Read(memory).Position!=position || memory.ReadWramByte(0xd104)!=1,
                        $"Seasons initialized ${id:x2} must obey the live companion mask after state0.");
                    memory.SetField("wDisabledObjects",0);
                }
                else
                {
                    bool unsupported = false;
                    try { entities.LoadRoom(rooms.ActiveGroup, room); }
                    catch (NotSupportedException error) when (error.Message.Contains("loadRememberedCompanion") && error.Message.Contains($"SPECIALOBJECT ${id:x2}")) { unsupported = true; }
                    FailIf(!unsupported || memory.ReadWramByte(0xd100) != 1 || memory.ReadWramByte(0xd101) != id ||
                        memory.ReadWramByte(0xd104) != 0 || memory.ReadWramByte(0xd10a) != 0x40 || memory.ReadWramByte(0xd10c) != 0xc0 ||
                        save.LastAnimalMountY != point.Y || save.LastAnimalMountX != point.X || entities.EntityAdapters<IRoomEntity>().OfType<IRoomInitializedCompanion>().Any(),
                        "Seasons must copy its native remembered header/mount highs, then diagnose unbound content instead of skipping the engine mechanism or treating ID$13 as an Ages raft.");
                }
            }
            if (save.Game == OracleGame.Ages)
            {
                foreach (int id in new[] { 0x0b, 0xff })
                {
                    ClearSlot();
                    CompanionRuntimeState.Remember(memory, id, 0, 1, point);
                    memory.SetField("wcc05", 5);
                    memory.SetWramByte(0xd104, id == 0x0b ? (byte)5 : (byte)0);
                    bool unsupported = false;
                    try { entities.LoadRoom(0, room); }
                    catch (NotSupportedException error) when (error.Message.Contains("loadRememberedCompanion") &&
                        error.Message.Contains($"SPECIALOBJECT ${id:x2}") && (id == 0xff || error.Message.Contains("state $05"))) { unsupported = true; }
                    FailIf(!unsupported || memory.ReadWramByte(0xd100) != 1 || memory.ReadWramByte(0xd101) != id ||
                        memory.ReadWramByte(0xd104) != (id == 0x0b ? 5 : 0),
                        "Opaque remembered state and unknown bodies must diagnose after native writes without silently clearing the slot or initializing state0.");
                }
                int flag = new RaftDatabase().Behavior.ChangedRoomsFlag;
                bool changedRooms = save.HasGlobalFlag(flag);
                try
                {
                    ClearSlot(); save.SetGlobalFlag(flag, false);
                    CompanionRuntimeState.Remember(memory, 0x13, 1, 0xa7, new(88, 88));
                    for (int slot = 2; slot < 16; slot++)
                    {
                        var puff = new StationaryDebrisEffect(); puff.Initialize(rooms.AssetRoot, new(24, 24), SoundId.MusNone);
                        entities.AddEntity(new InteractionSlotValidationEntity(puff, _ => { }));
                    }
                    var destination = rooms.Load(1, 0xa7);
                    FailIf((destination.TilesetFlags & 0x80) == 0, "Remembered raft capacity fixture requires the source past tileset.");
                    memory.SetField("wScrollMode", 8);
                    entities.BeginScreenTransition(1, destination, new(0, 128), EnemyPlacementContext.Unrestricted, player);
                    FailIf(entities.EntityAdapters<RaftInteractionRoomEntity>().Any() || CompanionRuntimeState.AnyActive(memory),
                        "The shared remembered raft request must fail before constructing content when all fourteen interaction slots are retained.");
                    entities.FinishScreenTransition(); memory.SetField("wScrollMode", 1);
                    entities.LoadRoom(1, destination);
                    entities.UpdateNativeInteractionPass(player, entities.FrameCounter);
                    FailIf(entities.EntityAdapters<RaftInteractionRoomEntity>().Single().Position != new Vector2(88, 88) || CompanionRuntimeState.AnyActive(memory),
                        "Free-capacity remembered raft dispatch must construct its waiting interaction presenter without claiming the special-object slot.");
                }
                finally { save.SetGlobalFlag(flag, changedRooms); }
            }
        }
        finally
        {
            root._scene.Dialogue.Close(); player.EndCutsceneControl(); ClearSlot();
            CompanionRuntimeState.ForgetRemembered(memory); save.SetMapleKillCounter(0);
            if (save.Game == OracleGame.Ages)
            {
                memory.SetField("wcc05", load);
                for (int index = 0; index < stateFields.Length; index++) save.WriteField(stateFields[index], stateValues[index]);
                root.LoadDebugRoom(originalGroup, originalRoom);
            }
            else{save.WriteField("wDimitriState",dimitriState);save.WriteField("wMooshState",mooshState);save.WriteField("wRickyState",rickyState);root._seasonsOpening!.DebugTravel(originalGroup, originalRoom);}
            CompanionRuntimeState.Remember(memory, remembered.Id, remembered.Group, remembered.Room, new(remembered.X, remembered.Y));
            save.SetLastAnimalMountPoint(mountY, mountX); save.SetMapleKillCounter(kills);
            memory.SetField("wScrollMode", 1); entities.EnableLinkCollisionsAndMenu(); player.EndCutsceneControl();
        }
    }

    private void ValidateSharedRememberedCompanionInitializationRom()
    {
        int cases = 0;
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var data = RememberedCompanionInitializationData.For(OracleAssetPack.ForGame(game));
            var save = OracleSaveData.CreateStandardGame(game);
            var memory = OracleRuntimeState.CreateInitialized(game);
            var room = game == OracleGame.Ages ? _world.LoadRoom(0, 1) : new SeasonsWorldData().LoadRoom(0x97, 0, 0);
            var native = new BraceletRom(room, new(80, 80), 2, 0xff, game: game);
            foreach (int id in new[] { 0, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x13, 0xff })
            foreach (int mismatch in new[] { 0, 1, 2 })
            foreach (byte enabled in new byte[] { 0, 1, 2, 3, 0x80 })
            foreach (byte tileset in new byte[] { 0, 8, 0x80, 0x88 })
            foreach (byte state in new byte[] { 0, 5 })
            foreach (int free in new[] { 0, 1, 14 })
            {
                Vector2 position = free switch { 0 => new(0, 0), 1 => new(0x48, 0x38), _ => new(0xff, 0x80) };
                save.SetLastAnimalMountPoint(0x91, 0x72);
                native.LoadSave(save);
                CompanionRuntimeState.Remember(memory, id, mismatch == 1 ? 1 : 0, mismatch == 2 ? 2 : 1, position);
                for (int offset = 0; offset < 5; offset++) native[0xcc24 + offset] = memory.ReadWramByte((game == OracleGame.Ages ? 0xcc24 : 0xcc40) + offset);
                for (int offset = 0; offset < 64; offset++)
                {
                    byte value = (byte)(offset * 7 + 0x21);
                    native[0xd100 + offset] = value; memory.SetWramByte(0xd100 + offset, value);
                }
                native[0xd100] = enabled; memory.SetWramByte(0xd100, enabled);
                native[0xd104] = state; memory.SetWramByte(0xd104, state);
                native[0xcc2d] = 0; native[0xcc30] = 1; native[0xcc34] = tileset;
                for (int slot = 2; slot < 16; slot++)
                    for (int offset = 0; offset < 64; offset++) native[0xd040 + slot * 256 + offset] = 0;
                for (int slot = 2; slot < 16 - free; slot++) native[0xd040 + slot * 256] = 2;
                native.LoadRememberedCompanion();
                bool candidate = data.TryInitialize(0, 1, tileset, save, memory, out var admission);
                bool allocated = candidate && (!admission.IsInteraction || free != 0);
                bool predicate = id != 0 && mismatch == 0 && enabled == 0;
                bool raft = game == OracleGame.Ages && id == 0x13;
                bool expected = predicate && (!raft || (tileset & 0x80) != 0 && free != 0);
                string context = $"{game} remembered${id:x2}/mismatch{mismatch}/enabled${enabled:x2}/tileset${tileset:x2}/state${state:x2}/free${free:x2}";
                FailIf(allocated != expected || candidate && (admission.SpecialObjectId != id || admission.Position != position || admission.IsInteraction != raft),
                    context + ": native remembered predicate or allocation request differs.");
                for (int offset = 0; offset < 64; offset++)
                    FailIf(memory.ReadWramByte(0xd100 + offset) != native[0xd100 + offset], context + $": native SPECIALOBJECT byte ${0xd100 + offset:x4} differs.");
                FailIf(save.LastAnimalMountY != native[0xc638] || save.LastAnimalMountX != native[0xc639] ||
                    native.RandomCalls != 0 || (game == OracleGame.Ages || !allocated) && (save.LastAnimalMountY != 0x91 || save.LastAnimalMountX != 0x72),
                    context + ": only successful Seasons loading copies the mount highs; initialization must not consume RNG.");
                if (expected && raft)
                {
                    int at = 0xd040 + (16 - free) * 256;
                    FailIf(admission.InteractionId != 0xe6 || admission.InteractionSubId != 2 ||
                        native[at] != 1 || native[at + 1] != 0xe6 || native[at + 2] != 2 || native[at + 11] != position.Y || native[at + 13] != position.X,
                        context + ": Ages remembered raft must request INTERAC $e6:$02 at its first free slot, without claiming D1.");
                }
                cases++;
            }
            byte[] before = memory.CaptureState().Wram;
            byte[] saved = save.Serialize();
            bool rejected = false;
            try { data.TryInitialize(0, 1, 0, OracleSaveData.CreateStandardGame(game == OracleGame.Ages ? OracleGame.Seasons : OracleGame.Ages), memory, out _); }
            catch (ArgumentException) { rejected = true; }
            FailIf(!rejected || !memory.CaptureState().Wram.SequenceEqual(before) || !save.Serialize().SequenceEqual(saved),
                "Cross-pack remembered initialization must fail before changing runtime or save bytes.");
        }
        GD.Print($"Validated shared remembered initialization against both original native routines: {cases} cases, identity/occupancy gates, retained state/fractions, native mount copies, Ages raft dispatch/capacity requests and no RNG.");
    }
}


namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSeasonsMooshInitialization(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms; var save = rooms.SaveData;
        if (save.Game != OracleGame.Seasons) return;
        var memory = rooms.RuntimeState; var entities = root._seasonsOpening!.Entities;
        var player = root._scene.Player; var inventory = player.Inventory;
        var app = new ApplicationValidationFixture(root);
        int oldGroup = rooms.ActiveGroup, oldRoom = rooms.CurrentRoom.Id, oldAnimal = inventory.AnimalCompanion;
        byte oldFlags = save.ReadField("wMooshState");
        void Clear()
        {
            entities.Clear(); player.ClearNativeItemParents(); CompanionRuntimeState.ForgetRemembered(memory);
            for (int at = 0xd100; at < 0xd140; at++) memory.SetWramByte(at, 0);
            memory.SetField("wLinkObjectIndex", 0xd0); memory.SetField("wDisabledObjects", 0);
            memory.SetField("wMenuDisabled", 0); memory.SetField("wDisableScreenTransitions", 0);
            memory.SetField("wWarpsDisabled", 0); memory.SetField("wDisableWarpTiles", 0);
        }
        try
        {
            foreach (var (roomId, flags, assigned) in new (int, int, bool)[]
            {
                (0x97,0x80,true), (0x97,0x80,false), (0x97,0,true), (0x97,0x20,true), (0x97,0,false),
                (0x2f,0x20,true), (0x2f,0,true), (0x90,0,true), (0x90,0x20,true), (0x90,0x80,false), (0x90,0,false)
            })
            {
                Clear(); root._seasonsOpening.DebugTravel(0, roomId); Clear();
                var room = rooms.CurrentRoom;
                Vector2 position = (from y in Enumerable.Range(1, room.HeightInTiles - 2)
                    from x in Enumerable.Range(1, room.WidthInTiles - 2)
                    let p = new Vector2(x * 16 + 8, y * 16 + 8)
                    where Enumerable.Range(-6, 31).All(dy => Enumerable.Range(-6, 13).All(dx =>
                        !room.IsSolid(p + new Vector2(dx, dy)) && room.GetTerrainInfo(p + new Vector2(dx, dy)).Hazard == HazardType.None))
                    select p).First();
                save.WriteField("wMooshState", (byte)flags); inventory.AssignAnimalCompanion(assigned ? 0x0d : 0x0b);
                inventory.EquipA(0); inventory.EquipB(0);
                player.WarpTo(position + new Vector2(0, 24)); player.Face(Vector2I.Up); player.ResetEnemyInvincibility();
                entities.Spawn(new MooshCompanionSpawn(position, 2, 0, roomId));
                var native = new BraceletRom(room, player.PrecisePosition, 0, -1, game: OracleGame.Seasons);
                native.LoadSave(save); native.InitializeWalkingAnimation();
                for (int at = 0xd100; at < 0xd140; at++) native[at] = memory.ReadWramByte(at);
                var sounds = root._sound.AttachPlayRequestAudit(); int updates = 0;
                byte? interactionOamFlags = null;
                void Write(int address, byte value) { memory.SetWramByte(address, value); native[address] = value; }
                void Step(int count = 1) => app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.UpdateLinkItems(entities.FrameCounter, 0, beforeInteractions: () =>
                    {
                        if (interactionOamFlags is byte value) native[0xd01c] = value;
                    });
                    string context = $"Seasons Moosh room${roomId:x2}/flags${flags:x2}/assigned{assigned}/batch{batched}/update{++updates}";
                    foreach (int offset in new[] { 0,1,3,4,5,6,7,8,9,10,11,12,13,14,15,0x10,0x14,0x15,0x1a,0x1b,0x1c,0x20,0x21,0x24,0x26,0x27,0x2b,0x30,0x3d,0x3f })
                        FailIf(memory.ReadWramByte(0xd100 + offset) != native[0xd100 + offset],
                            context + $": byte${offset:x2} runtime${memory.ReadWramByte(0xd100 + offset):x2}/native${native[0xd100 + offset]:x2}.");
                    FailIf(memory.ReadField("wDisabledObjects") != native[0xcc8a] ||
                        memory.ReadField("wMenuDisabled") != native[0xcc02], context + ": native input masks differ.");
                    FailIf(player.CompanionRideActive != (native[0xcc2c] == 0xd1) ||
                        player.Position != new Vector2(native[0xd00d], native[0xd00b]), context + ": Link handoff differs.");
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds), context + ": cue order differs.");
                });
                Step();
                bool ordinary = (flags & 0x80) != 0 || assigned && ((flags & 0x20) != 0 ? roomId != 0x2f : roomId != 0x90);
                FailIf(memory.ReadWramByte(0xd104) != (ordinary ? 1 : 0x0a), "Seasons mooshState0 selected the wrong native dispatch.");
                Step(3);
                if (ordinary) continue;
                if (native[0xd103] == 2)
                {
                    Write(0xd12b, 10); Step(12);
                    // companionScript_mooshInSpoolSwamp writes var03=$05 and var3f=$03 after rescue.
                    Write(0xd103, 5); Write(0xd13f, 3); Step(3);
                    save.WriteField("wMooshState", 0x20); native.LoadSave(save);
                }
                else
                {
                    Write(0xd13d, 1); Step(); Step(2);
                    FailIf(memory.ReadField("wDisabledObjects") != 0x81, "Moosh's script button signal must write the native $81 mask.");
                    memory.SetField("wDisabledObjects", 0); native[0xcc8a] = 0;
                    save.WriteField("wMooshState", 0x80); native.LoadSave(save);
                }
                Step(65);
                FailIf(!player.CompanionRideActive || player.CompanionJumpActive ||
                    entities.Entities<MooshCompanionRoomEntity>().Count != 1, "Seasons Moosh's native force-mount handoff did not finish in the common body.");
                if (roomId == 0x90 && assigned)
                {
                    byte previousBackup = memory.ReadWramByte(0xd01b), previousFlags = memory.ReadWramByte(0xd01c);
                    var puff = new StationaryDebrisEffect(); puff.Initialize(rooms.AssetRoot, position, SoundId.MusNone);
                    var producer = new InteractionSlotValidationEntity(puff, _ => memory.SetWramByte(0xd01c, interactionOamFlags!.Value));
                    entities.AddEntity(producer);
                    Write(0xd01b, 8);
                    // Stage a later interaction writer; func_410d must consume it in this same update.
                    foreach (byte flagsToCopy in new byte[] { 0x0a, 0x0d, 8 }) { interactionOamFlags = flagsToCopy; Step(); }
                    entities.RetireEntity(producer); interactionOamFlags = null;
                    memory.SetWramByte(0xd01b, previousBackup); memory.SetWramByte(0xd01c, previousFlags);
                }
            }
        }
        finally
        {
            Clear(); save.WriteField("wMooshState", oldFlags); save.WriteField("wAnimalCompanion", (byte)oldAnimal);
            typeof(InventoryState).GetProperty(nameof(InventoryState.AnimalCompanion))!.SetValue(inventory, oldAnimal);
            root._seasonsOpening.DebugTravel(oldGroup, oldRoom);
        }
    }
}

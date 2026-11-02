
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSharedOpenedChests()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var save = OracleSaveData.CreateStandardGame(game);
            var memory = OracleRuntimeState.CreateInitialized(game);
            var content = game == OracleGame.Ages ? (IRoomContent)new AgesRoomContent(null, null) :
                new SeasonsRoomContent(new SeasonsWorldData());
            var rooms = new RoomSession(content, 0, game == OracleGame.Ages ? 0x49 : 0xf5,
                static () => 0, static () => { }, save, memory);
            var chests = new ChestDatabase(OracleAssetPack.ForGame(game));
            var cases = game == OracleGame.Ages ? new[] { (0, 0x49), (2, 0xf7), (5, 0xc0), (4, 0xce) } :
                new[] { (0, 0xf5), (1, 0x41), (0, 0xb6) };
            foreach ((int group, int id) in cases)
            foreach (bool opened in new[] { false, true })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                save.SetRoomFlag(group, id, OracleSaveData.RoomFlagItem, false);
                OracleRoomData baseline = rooms.Load(group, id);
                var native = new BraceletRom(baseline, Vector2.Zero, 2, 0xff, game: game);
                native[0xcc2d] = (byte)group;
                save.SetRoomFlag(group, id, OracleSaveData.RoomFlagItem, opened);
                native.LoadSave(save);
                native[0xcfff] = 0x55;
                memory.SetWramByte(0xcfff, 0x55);
                chests.ApplyOpenedChest(group, baseline, save, memory, 0);
                native.ApplyOpenedChest();
                for (int position = 0; position < 0xc0; position++)
                    FailIf(baseline.ReadNativeLayoutByte((byte)position, memory) != native[0xcf00 + position],
                        $"{game}: room {group:x1}:{id:x2}, opened={opened}, repeat={repeat}, layout ${position:x2} differs from native replaceOpenedChest.");
                FailIf(memory.ReadWramByte(0xcfff) != native[0xcfff],
                    $"{game}: room {group:x1}:{id:x2}, opened={opened}: a missing chest must preserve the native E=$ff script alias write.");
                var expected = native.GetChestData();
                bool found = chests.TryGetRoomChest(group, id, out ChestRecord chest);
                FailIf(found ? (chest.Position, chest.TreasureId, chest.SubId) != expected :
                    expected != (0xff, 0x28, 0),
                    $"{game}: room {group:x1}:{id:x2} must select the native first room match and missing-row contents/position.");
                OracleRoomData actual = rooms.Load(group, id);
                // applyAllTileSubstitutions subsequently dispatches the genuine
                // room2:f7 exception, which opens both chests when ITEM is set.
                if (opened && game == OracleGame.Ages && (group, id) == (2, 0xf7))
                    native.Call(4, 0x676b);
                foreach (ChestRecord record in chests.GetRoomRecords(group, id))
                    FailIf(actual.ReadNativeLayoutByte((byte)record.Position, memory) != native[0xcf00 + record.Position],
                        $"{game}: room {group:x1}:{id:x2}, opened={opened}: initialized chest tile ${record.Position:x2} lost native common/room-specific ordering.");
                FailIf(memory.ReadWramByte(0xcfff) != native[0xcfff],
                    $"{game}: room {group:x1}:{id:x2}: room initialization lost the native missing-chest script alias.");
                if (game == OracleGame.Ages && (group, id) is (2, 0xf7) or (5, 0xc0))
                {
                    var records = chests.GetRoomRecords(group, id);
                    FailIf(records.Count != 2 || chest.Position != records[0].Position,
                        $"Ages {group:x1}:{id:x2}: the later duplicate room row must not replace the native first lookup.");
                }
            }
        }
    }
}

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static BraceletRom SharedNativeEnemyReference(RoomSession rooms, Player player, int id)
    {
        var game = rooms.SaveData.Game;
        var clean = ValidationRom.LoadCleanUs(game);
        int properties = game == OracleGame.Ages ? 0xfdd4b : 0xfdd71;
        var native = new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,0xff,game:game);
        native.LoadSave(rooms.SaveData);
        native[0xcbca] = 1; // Both fixtures disable contact while comparing AI and producer order.
        // Keep the resident item-drop sheet and add this native enemy sheet.
        native[0xcc0a] = clean.Span[properties + id * 4];
        native[0xcc2d] = (byte)rooms.ActiveGroup;
        int target = game == OracleGame.Ages ? 0xffb0 : 0xffae;
        native[target] = (byte)player.Position.Y; native[target + 1] = (byte)player.Position.X;
        native[target + 2] = (byte)player.Position.Y; native[target + 3] = (byte)player.Position.X;
        return native;
    }
}

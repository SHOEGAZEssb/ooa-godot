using Godot;

namespace oracleofages;

/// <summary>
/// The source bytes read by initTextbox and initTextboxStuff. They describe
/// the displayed scene, which need not contain the live gameplay Link object.
/// </summary>
internal readonly record struct DialogueScreenContext(
    byte LinkY, byte CameraY, byte ScreenOffsetY, byte ScrollY)
{
    internal static DialogueScreenContext Gameplay(float linkY, float cameraY = 0) =>
        new(ToByte(linkY), ToByte(cameraY), 0,
            unchecked((byte)(ToByte(cameraY) - OracleRoomData.GameplayScreenTop)));

    internal static DialogueScreenContext FullScreen(float linkY) =>
        new(ToByte(linkY), 0, 0, 0);

    internal int AutomaticPosition => ((LinkY - CameraY) & 0xff) < 0x48 ? 2 : 0;

    internal int ScreenY(int tilemapY) =>
        (tilemapY + ((CameraY + ScreenOffsetY + 4) & 0xf8) - ScrollY) & 0xff;

    private static byte ToByte(float coordinate) =>
        unchecked((byte)Mathf.FloorToInt(coordinate));
}

namespace Oracles;

public partial class Player
{
    private byte[]? _danceGrid, _danceAnimations;
    internal bool SubrosianDanceActive => _inventory.RuntimeState.Game == OracleGame.Seasons &&
        _inventory.RuntimeState.ReadWramByte(0xd001) == 1;

    internal void RequestSubrosianDance(byte[] grid, byte[] animations)
    {
        _danceGrid = grid; _danceAnimations = animations;
        SetDanceLinkId(1);
    }
    internal void SetDanceLinkId(int id)
    {
        var memory = _inventory.RuntimeState;
        memory.SetField("wLinkIDOverride", (byte)(0x80 | id));
        for (int offset = 2; offset <= 5; offset++) memory.SetWramByte(0xd000 + offset, 0);
    }
    internal void CancelSubrosianDance()
    {
        var memory = _inventory.RuntimeState;
        if (!SubrosianDanceActive && memory.ReadField("wLinkIDOverride") != 0x81) return;
        memory.SetField("wLinkIDOverride", 0); memory.SetWramByte(0xd001, 0);
        memory.SetWramByte(0xd004, 1); memory.SetWramByte(0xd005, 0);
        _danceGrid = _danceAnimations = null; ResetLinkWalkAnimation(); QueueRedraw();
    }
    private void SetDanceAnimation(int mode)
    {
        var pointers = _danceAnimations ?? throw new InvalidOperationException("subrosiaDanceLink.s: SPECIALOBJECT$01 has no selected-pack animation binding.");
        LinkNativeAnimationDatabase.For(AssetRoot).SetPointer(_inventory.RuntimeState, mode,
            pointers[mode * 2] | pointers[mode * 2 + 1] << 8, "specialObject01AnimationDataPointers");
        QueueRedraw();
    }
    private bool AdvanceSubrosianDance()
    {
        var memory = _inventory.RuntimeState;
        if (memory.Game != OracleGame.Seasons) return false;
        int requested = memory.ReadField("wLinkIDOverride");
        if (requested is 0x80 or 0x81)
        {
            memory.SetField("wLinkIDOverride", 0); memory.SetWramByte(0xd001, (byte)(requested & 0x7f));
            if (requested == 0x80)
            { _nativeInitializationPending = true; _danceGrid = _danceAnimations = null; ResetLinkWalkAnimation(); return false; }
        }
        if (!SubrosianDanceActive) return false;
        int state = memory.ReadWramByte(0xd004);
        if (state == 0)
        {
            ClearNativeItemParents(); memory.SetWramByte(0xd000, (byte)(memory.ReadWramByte(0xd000) | 3));
            memory.SetWramByte(0xd004, 1); Face(Vector2I.Down);
            SetScriptedCoordinateHigh(true, 0x68); SetScriptedCoordinateHigh(false, 0x38);
            SetDanceAnimation(0); Visible = true; return true;
        }
        if (state <= 3 && memory.ReadWramByte(0xcfd0) != 0)
        {
            bool failed = memory.ReadWramByte(0xcfd0) == 0xff;
            memory.SetWramByte(0xd004, (byte)(failed ? 5 : 4)); SetDanceAnimation(failed ? 2 : 1);
            memory.SetWramByte(0xd01a, 0x80); return true;
        }
        switch (state)
        {
            case 1:
                int keys = memory.ReadField("wGameKeysJustPressed");
                int button = (keys & 0x10) != 0 ? 0 : (keys & 0x20) != 0 ? 1 : (keys & 1) != 0 ? 2 : -1;
                if (button < 0) return true;
                if (memory.ReadWramByte(0xcfd8) == 0xff) memory.SetWramByte(0xcfd8, (byte)button);
                if (button == 2)
                { memory.SetWramByte(0xd004, 3); Face(Vector2I.Up); SetDanceAnimation(1); return true; }
                memory.SetWramByte(0xd004, 2); memory.SetWramByte(0xd037, 0);
                int packed = ((int)Position.Y & 0xf0) | ((int)Position.X >> 4);
                var grid = _danceGrid!;
                // Only the first column contains positions; reject a corrupt off-grid native state.
                int index;
                for (index = 0; index < grid.Length && grid[index] != packed; index += 2) { }
                if (index >= grid.Length) throw new NotSupportedException($"subrosiaDance_nextTileLookup: Link is off grid at ${packed:x2}.");
                int direction = (button == 0 ? grid[index + 1] : grid[index + 1] >> 4) & 3;
                Face(direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left });
                memory.SetWramByte(0xd009, (byte)(direction * 8)); SetDanceAnimation(0); return true;
            case 2:
                AdvanceNativeLinkAnimation();
                Vector2 before = Position;
                memory.SetWramByte(0xd038, (byte)before.Y); memory.SetWramByte(0xd039, (byte)before.X);
                int speed = memory.ReadWramByte(0xcfd3); memory.SetWramByte(0xd010, (byte)speed);
                SetScriptedPosition(NativeObjectMovement.ApplySpeed(memory, OracleObjectPosition.FromPixels(_precisePosition), speed,
                    memory.ReadWramByte(0xd009)).PrecisePosition);
                SetScriptedCoordinateHigh(true, Math.Clamp((int)Position.X, 0x18, 0x68));
                SetScriptedCoordinateHigh(false, Math.Clamp((int)Position.Y, 0x18, 0x68));
                int distance = Math.Abs((int)before.X - (int)Position.X) + Math.Abs((int)before.Y - (int)Position.Y);
                if (distance == 0) { memory.SetWramByte(0xd004, 1); return true; }
                int travelled = (memory.ReadWramByte(0xd037) + distance) & 255; memory.SetWramByte(0xd037, (byte)travelled);
                if (travelled < 16) return true;
                SetScriptedPosition(new(((int)Position.X & 0xf0) | 8, ((int)Position.Y & 0xf0) | 8));
                memory.SetWramByte(0xd004, 1); return true;
            case 3:
                AdvanceNativeLinkAnimation(); if (memory.ReadWramByte(0xd021) == 0xff) memory.SetWramByte(0xd004, 1);
                QueueRedraw(); return true;
            case 4: AdvanceNativeLinkAnimation(); QueueRedraw(); return true;
            case 5: return true;
            default: throw new NotSupportedException($"subrosiaDanceLink.s: state ${state:x2}.");
        }
    }
}

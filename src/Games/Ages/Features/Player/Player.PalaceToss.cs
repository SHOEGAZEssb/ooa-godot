using Godot;

namespace Oracles;

public partial class Player
{
    private bool _palaceTossRequested;
    private int _palaceTossSubstate = -1, _palaceTossCounter;
    internal int PalaceTossSubstate => _palaceTossSubstate;
    internal void RequestPalaceToss() => _palaceTossRequested = true;
    internal void CancelPalaceToss()
    {
        _palaceTossRequested = false; _palaceTossSubstate = -1; _scriptedLinkAnimationMode = null;
    }
    private bool AdvancePalaceToss()
    {
        if (_palaceTossRequested)
        { _palaceTossRequested = false; _palaceTossSubstate = 0; return true; }
        if (_palaceTossSubstate < 0) return false;
        if (_world.DialogueOpen) return true;
        if (_palaceTossSubstate == 0)
        {
            _palaceTossSubstate = 1; _palaceTossCounter = 0x14;
            SetScriptedPosition(new(0x50 + _precisePosition.X % 1, 0x38 + _precisePosition.Y % 1));
            _topDownAirSpeedZ = -0x180;
            _scriptedLinkAnimationMode = 2; QueueRedraw(); return true;
        }
        if (_palaceTossSubstate == 1)
        {
            var position = OracleObjectPosition.FromPixels(_precisePosition).Add(0x100, 0);
            SetScriptedPosition(position.PrecisePosition);
            if (OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed, ref _topDownAirSpeedZ, 0x20))
            {
                int bounce = unchecked((short)-_topDownAirSpeedZ) >> 1;
                if (bounce > -0x80 || bounce == 0) _palaceTossSubstate = 2;
                else _topDownAirSpeedZ = bounce;
            }
            _topDownAirborne = _topDownAirZFixed < 0; QueueRedraw(); return true;
        }
        if (--_palaceTossCounter == 0)
        {
            _palaceTossSubstate = -1; _scriptedLinkAnimationMode = null;
            _topDownAirborne = false; _walking = false; Face(Vector2I.Down);
        }
        QueueRedraw(); return true;
    }
}

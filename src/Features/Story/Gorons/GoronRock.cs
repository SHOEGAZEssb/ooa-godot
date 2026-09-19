using Godot;

namespace oracleofages;

internal sealed class GoronRock(NpcCharacter actor, Vector2 position, int angle,
    int speed, int speedZ, int z, bool falling, int slot)
{
    internal int Slot => slot;
    internal NpcCharacter Actor => actor;
    internal bool Falling => falling;
    internal Vector2 Position => position;
    private bool _initialized;
    internal bool Update(System.Func<byte> random, int[] positions)
    {
        if (!_initialized)
        {
            _initialized=true;
            if (!falling) return false;
            int index=(random()&15)*2;
            position=new(positions[index+1],positions[index]);
            actor.Position=position;
            z=(-(int)position.Y-8)*256;
        }
        if (OracleObjectMath.UpdateSpeedZ(ref z,ref speedZ,falling?0x10:0x18))
        { return true; }
        if (!falling) actor.Position=OracleObjectMovement.Shared.ApplySpeed(ref position,speed,angle);
        actor.SetScriptDrawOffset(new Vector2(0,z>>8));
        return false;
    }
}

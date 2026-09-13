using Godot;

namespace oracleofages;

internal static class EnemyStunMotion
{
    // bank0.s:enemyStandardUpdate/@stunned, objectUpdateSpeedZAndBounce.
    internal static Vector2 Update(Vector2 position, int state, int frameCounter,
        ref int counter, ref int z, ref int speedZ)
    {
        if ((frameCounter & 1) != 0 && --counter < 30 && (counter & 1) != 0)
        {
            float high = Mathf.Floor(position.X);
            position.X = ((int)high ^ 1) + position.X - high;
        }
        if (state < 8 || (((z >> 8) - 1) & 0xff) < 8)
        {
            speedZ = 0;
            return position;
        }
        z = unchecked((short)(z + speedZ));
        if (z < 0)
            speedZ = unchecked((short)(speedZ + 0x20));
        else
        {
            z = 0;
            int bounce = unchecked((short)-speedZ) >> 1;
            // compareHlToBc compares the unsigned words, including equality
            // at $ff80. A downward speed of $00ff still produces that bounce.
            speedZ = (ushort)bounce > 0xff80 || bounce == 0 ? 0 : bounce;
        }
        return position;
    }
}

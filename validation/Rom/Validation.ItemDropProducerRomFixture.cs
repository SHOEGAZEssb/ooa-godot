using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void SeedItemDropProducersRom(EnemyStatusRom rom)
    {
        // Object opcode$fa places invisible ENEMY$59 in the same ordered
        // pool as live enemies. Its state0 still consumes the common RNG
        // draw. Seed actual retained producers, including during text/preload.
        var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
        foreach(var pair in slots)
        {
            if(pair.Key.Node is not ItemDropProducer producer) continue;
            int s=0xd080+pair.Value*256;
            for(int offset=0;offset<64;offset++) rom[s+offset]=0;
            rom[s]=1; rom[s+1]=0x59;
            rom[s+2]=(byte)SomariaPrivate<int>(producer,"_subId");
            rom[s+11]=(byte)producer.Position.Y; rom[s+13]=(byte)producer.Position.X;
        }
    }
}

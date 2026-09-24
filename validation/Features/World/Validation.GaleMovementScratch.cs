using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGaleMovementScratch()
    {
        // Isolate ecom_galeSeedEffect's counter boundary. CrownWhispSeeds
        // separately covers entry, dispatch and deletion in the gameplay loop.
        byte[][] words = [[0xe0, 0xff, 0, 0], [0, 0, 0x20, 0], [0x20, 0, 0, 0], [0, 0, 0xe0, 0xff]];
        for (int direction = 0; direction < 4; direction++)
        {
            var actor = new ValidationMovementCharacter { Position = new(80, 80) };
            actor.BindMovementMemory(_runtimeState);
            var gale = new GaleSeedEnemyMotion(actor);
            for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
            gale.Begin(new(80, 80), -1, () => (byte)(direction * 8));
            for (int update = 0; update < 29; update++)
            {
                gale.Update(0);
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != 0xa5,
                        "Gale's 29 shaking updates must not execute objectApplySpeed.");
            }
            FailIf(gale.Counter != 1, "Gale shaking must stop immediately before counter zero.");
            for (int update = 0; update < 2; update++)
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, 0xa5);
                gale.Update(0);
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != words[direction][i],
                        $"Gale angle ${direction * 8:x2} must publish SPEED_020 from the counter-zero update onward.");
            }
            actor.Free();
        }
    }
}

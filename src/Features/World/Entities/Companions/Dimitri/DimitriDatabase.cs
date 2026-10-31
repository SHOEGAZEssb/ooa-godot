using System;

namespace Oracles;

internal sealed class DimitriDatabase
{
    public string AssetRoot { get; }

    internal DimitriVisualDatabase Visual { get; }
    private ObjectCollisionDatabase Collisions => ObjectCollisionDatabase.For(AssetRoot);
    internal bool AcceptsMouthCollision(int type) => type >= 0 && type < 128 &&
        Collisions.EnemyEnabled(type,ItemCollisionType.DimitriMouth);
    internal bool CanSwallow(int mode) => mode >= 0 && mode < Collisions.ModeCount
        ? Collisions.Effect(mode,ItemCollisionType.DimitriMouth)==CollisionEffect.Effect25
        : throw new InvalidOperationException($"{AssetRoot}: Dimitri mouth lacks enemy collision mode ${mode:x2}.");
    internal DimitriDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        for(int mode=0;mode<Collisions.ModeCount;mode++)
        {
            int effect=Collisions.Effect(mode,ItemCollisionType.DimitriMouth);
            if(effect is not (CollisionEffect.None or CollisionEffect.Effect1c or CollisionEffect.Effect25 or CollisionEffect.Effect26))
                throw new NotSupportedException($"{AssetRoot}: objectCollisionTable.s mode${mode:x2}, ITEMCOLLISION_DIMITRI_MOUTH has unsupported effect${effect:x2}.");
        }

        Visual=new(AssetRoot);
    }
}

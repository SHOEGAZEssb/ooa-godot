using Godot;
using System;

namespace oracleofages;

// checkGrabbableObjects projects Link's high coordinates, then tests the
// buffered object against Link (the reverse of ordinary enemy contact).
internal sealed class BraceletGrabGeometry
{
    private readonly Vector2I[] _offsets = new Vector2I[4];
    private readonly int[] _zSubtract = new int[4];
    private readonly int[] _zRadius = new int[4];
    internal BraceletGrabGeometry()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/bracelet_grab_geometry.tsv",
            new GeneratedTableSchema("checkGrabbableObjects", GeneratedTableKeySemantics.Unique,
                ["direction","x","y","z-subtract","z-radius","source"], ["direction"], headerRequired: true));
        if (table.Rows.Count != 4) throw new InvalidOperationException("checkGrabbableObjects requires four ordered direction rows.");
        for (int i = 0; i < 4; i++)
        {
            var row = table.Rows[i];
            if (row.Decimal(0,0,3) != i) throw row.Invalid(0,"ordered direction");
            _offsets[i] = new(row.Decimal(1,-128,127),row.Decimal(2,-128,127));
            _zSubtract[i] = row.Decimal(3,0,127); _zRadius[i] = row.Decimal(4,1,127);
            _ = row.RequiredString(5);
        }
    }
    internal bool Overlaps(Rect2 linkBounds, int linkZ, int direction, Rect2 objectBounds, int objectZ, bool pendingCollision)
    {
        if (direction is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(direction));
        if (pendingCollision || !RoomEntityManager.ObjectCollisionZOverlaps(linkZ-_zSubtract[direction],objectZ,_zRadius[direction])) return false;
        Vector2 center = linkBounds.GetCenter().Floor() + _offsets[direction];
        center = new((byte)(int)center.X,(byte)(int)center.Y);
        return RoomEntityManager.ObjectCollisionXYOverlaps(new(center-linkBounds.Size/2,linkBounds.Size), objectBounds);
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateNativePlacementDeclarations()
    {
        var data = NativeObjectStreamDatabase.For(AgesGameContent.AssetRoot);
        var records = data.RoomObjects().ToArray();
        string Coordinate(int value) => value < 0 ? "-1" : value.ToString("x2");
        string Row(RoomObjectRecord record)
        {
            string kind = record.Kind switch
            {
                RoomObjectKind.RandomEnemy => "R", RoomObjectKind.FixedEnemy => "F", RoomObjectKind.ParameterEnemy => "B",
                RoomObjectKind.ReservingPart => "P", RoomObjectKind.ParameterPart => "Q", RoomObjectKind.ItemDrop => "I",
                _ => throw new InvalidOperationException($"Unexpected native placement {record.Kind}.")
            };
            return FormattableString.Invariant($"{record.Group}\t{record.Room:x2}\t{record.Order}\t{kind}\t{record.Id:x2}\t{record.SubId:x2}\t{record.Flags:x2}\t{record.Count}\t{Coordinate(record.Y)}\t{Coordinate(record.X)}\t{Coordinate(record.PackedPosition)}\t{record.ConditionMask:x2}\t{record.Var03:x2}");
        }
        // Captured from the independently source-parsed, ROM-cross-checked pre-migration stream.
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', records.Select(Row).OrderBy(row => row, StringComparer.Ordinal)))));
        FailIf(records.Length != 1150 || digest != "FB3B3F46B69D97739495A26B9C1C89AA4BA779955376D1C9C3BF4114310DEACE",
            "Native declarations changed the 1,150 source placement identities, orders, flags, counts, coordinates, masks or var03 bytes.");
        foreach (var record in records)
            FailIf(!record.Source.Contains(data.At(record.NativeAddress, record.Source).Source, StringComparison.Ordinal),
                $"{record.Source}: native placement lost its instruction boundary.");
        foreach (var room in records.GroupBy(record => (record.Group, record.Room)))
            FailIf(!room.Select(record => record.Order).SequenceEqual(Enumerable.Range(0, room.Count())),
                $"Native room ${room.Key.Group:x1}:${room.Key.Room:x2} declaration order is not contiguous.");
    }
}

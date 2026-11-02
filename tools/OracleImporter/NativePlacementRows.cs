using System.Globalization;

namespace Oracles.Importer;

public static class NativePlacementRows
{
    public static string[] Read(string root)
    {
        static IEnumerable<string[]> Rows(string path, int width)
        {
            int line = 0;
            foreach (string text in File.ReadLines(path))
            {
                line++;
                if (text.Length == 0 || text.StartsWith('#')) continue;
                string[] fields = text.Split('\t');
                if (fields.Length != width) throw new InvalidDataException($"{path}:{line}: expected {width} fields.");
                yield return fields;
            }
        }
        static int Hex(string value) => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var instructions = new Dictionary<int, NativeObjectInstruction>();
        foreach (var row in Rows(Path.Combine(root, "objects/room_object_streams.tsv"), 14))
        {
            if (row[3] is not ("0" or "1") || string.IsNullOrWhiteSpace(row[13]))
                throw new InvalidDataException($"{root}: malformed native instruction ${row[0]}.");
            var instruction = new NativeObjectInstruction(Hex(row[0]), Hex(row[1]), Hex(row[2]), row[3] == "1",
                Hex(row[4]), Hex(row[5]), Hex(row[6]), Hex(row[7]), Hex(row[8]), Hex(row[9]), Hex(row[10]), Hex(row[11]), Hex(row[12]), row[13]);
            if (instruction.Next <= instruction.Address) throw new InvalidDataException($"{row[13]}: native instruction does not advance.");
            instructions.Add(instruction.Address, instruction);
        }
        var labels = Rows(Path.Combine(root, "objects/room_object_labels.tsv"), 3).ToDictionary(row => row[0], row => Hex(row[1]), StringComparer.Ordinal);
        var result = new List<string> { "# group\troom\torder\tkind\tid\tsubid\tflags\tcount\ty\tx\tpacked-position\tcondition-mask\tvar03" };
        foreach (var record in NativeObjectDeclarations.Read(instructions, labels))
        {
            string kind = record.Kind switch
            {
                RoomObjectKind.RandomEnemy => "R", RoomObjectKind.FixedEnemy => "F", RoomObjectKind.ParameterEnemy => "B",
                RoomObjectKind.ReservingPart => "P", RoomObjectKind.ParameterPart => "Q", RoomObjectKind.ItemDrop => "I",
                _ => throw new InvalidDataException($"{record.Source}: unsupported placement kind.")
            };
            static string Position(int value) => value < 0 ? "-1" : value.ToString("x2");
            result.Add(FormattableString.Invariant($"{record.Group}\t{record.Room:x2}\t{record.Order}\t{kind}\t{record.Id:x2}\t{record.SubId:x2}\t{record.Flags:x2}\t{record.Count}\t{Position(record.Y)}\t{Position(record.X)}\t{Position(record.PackedPosition)}\t{record.ConditionMask:x2}\t{record.Var03:x2}"));
        }
        return result.ToArray();
    }
}

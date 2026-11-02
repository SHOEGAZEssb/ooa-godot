using Oracles.Importer;

namespace Oracles.Importer.Tests;

internal static class NativePlacementRowsTests
{
    internal static void Run(string root)
    {
        string folder = Path.Combine(root, "native placements");
        Directory.CreateDirectory(Path.Combine(folder, "objects"));
        string program = Path.Combine(folder, "objects/room_object_streams.tsv");
        File.WriteAllLines(program, new[]
        {
            "4000\t4006\t07\t1\t00\t62\t00\t00\t00\t00\t00\t0000\tff\tmain.s:vine",
            "4006\t4007\tff\t1\t00\t00\t00\t00\t00\t00\t00\t0000\tff\tmain.s:end",
            "4010\t4012\t00\t1\t00\t00\t00\t00\t00\t00\t00\t0000\t01\tenemy.s:condition",
            "4012\t4018\t07\t1\t00\t20\t00\t18\t28\t00\t00\t0000\tff\tenemy.s:fixed",
            "4018\t401f\t09\t1\t02\t13\t22\t88\ta8\t00\t07\t0000\tff\tenemy.s:part",
            "401f\t4020\tfe\t1\t00\t00\t00\t00\t00\t00\t00\t0000\tff\tenemy.s:return"
        });
        File.WriteAllLines(Path.Combine(folder, "objects/room_object_labels.tsv"), new[]
        {
            "group0Map98ObjectData\t4000\tmain.s:root",
            "group0Map98EnemyObjectData\t4010\tenemy.s:root"
        });
        string[] rows = NativePlacementRows.Read(folder);
        if (!rows.Skip(1).SequenceEqual(new[]
        {
            "0\t98\t0\tF\t62\t00\t00\t1\t00\t00\t00\tff\t00",
            "0\t98\t1\tF\t20\t00\t00\t1\t18\t28\t12\t01\t00",
            "0\t98\t2\tQ\t13\t22\t00\t1\t88\ta8\t8a\t01\t07"
        })) throw new InvalidOperationException("Native metadata lost direct/pointer ordering, conditions, coordinates or var03.");
        File.WriteAllText(program, "4000\t4006\t07\n");
        bool rejected = false;
        try { NativePlacementRows.Read(folder); }
        catch (InvalidDataException error) { rejected = error.Message.Contains("room_object_streams.tsv:1"); }
        if (!rejected) throw new InvalidOperationException("Malformed native metadata did not report its table and row.");
    }
}

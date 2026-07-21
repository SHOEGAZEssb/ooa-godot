using Godot;
using System;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateGeneratedTableReader()
    {
        var schema = new GeneratedTableSchema(
            "validation records",
            GeneratedTableKeySemantics.Unique,
            [
                "kind", "key", "hex", "decimal", "boolean", "float",
                "message", "optional"
            ],
            ["kind", "key"],
            headerRequired: true);
        const string path = "validation://generated-table.tsv";
        const string source =
            "# kind`tkey`thex`tdecimal`tboolean`tfloat`tmessage`toptional\r\n" +
            "first\tA\tff\t-2\t1\t1.5\tSGk=\t\r\n" +
            "second\tB\t00\t4\t0\t-0.25\tV29ybGQ=\ttail\r\n";
        GeneratedTable table = GeneratedTable.ParseForValidation(path, source, schema);
        if (table.Rows.Count != 2 ||
            table.Rows[0].LineNumber != 2 || table.Rows[1].LineNumber != 3 ||
            table.Rows[0].RequiredString(0) != "first" ||
            table.Rows[1].RequiredString(0) != "second" ||
            table.Rows[0].HexByte(2) != 0xff ||
            table.Rows[0].Decimal(3) != -2 ||
            !table.Rows[0].Boolean01(4) ||
            !Mathf.IsEqualApprox(table.Rows[0].FiniteFloat(5), 1.5f) ||
            table.Rows[0].Base64Utf8(6) != "Hi" ||
            table.Rows[0].String(7) != string.Empty ||
            table.Rows[1].Base64Utf8(6) != "World")
        {
            throw new InvalidOperationException(
                "The shared generated-table reader did not preserve rows, empty fields, or typed values.");
        }

        ExpectGeneratedTableFailure(
            () => GeneratedTable.ParseForValidation(
                path,
                "# kind\tkey\thex\tdecimal\tboolean\tfloat\tmessage\toptional\n" +
                "first\tA\t00\t0\t0\t0\t\t\n" +
                "first\tA\t01\t1\t1\t1\t\t\n",
                schema),
            "duplicate unique key",
            "first declared at line 2");
        ExpectGeneratedTableFailure(
            () => GeneratedTable.ParseForValidation(
                path,
                "# kind\tkey\twrong\nfirst\tA\t00\n",
                schema),
            "header is",
            "expected");
        ExpectGeneratedTableFailure(
            () => GeneratedTable.ParseForValidation(
                path,
                "# kind\tkey\thex\tdecimal\tboolean\tfloat\tmessage\toptional\n" +
                "first\tA\t00\n",
                schema),
            "expected 8 columns",
            "got 3");

        GeneratedTable invalidValue = GeneratedTable.ParseForValidation(
            path,
            "# kind\tkey\thex\tdecimal\tboolean\tfloat\tmessage\toptional\n" +
            "first\tA\t100\t0\t0\t0\t\t\n",
            schema);
        ExpectGeneratedTableFailure(
            () => invalidValue.Rows[0].HexByte(2),
            "column 'hex' (3)",
            "hexadecimal byte");

        foreach (GeneratedTableKeySemantics semantics in new[]
        {
            GeneratedTableKeySemantics.Grouped,
            GeneratedTableKeySemantics.Aliased,
            GeneratedTableKeySemantics.Repeated,
            GeneratedTableKeySemantics.Ordered
        })
        {
            string[]? keys = semantics == GeneratedTableKeySemantics.Grouped
                ? ["key"]
                : null;
            var repeatedSchema = new GeneratedTableSchema(
                $"{semantics} validation",
                semantics,
                ["key", "value"],
                keys,
                headerRequired: true);
            GeneratedTable repeated = GeneratedTable.ParseForValidation(
                path,
                "# key\tvalue\nsame\tfirst\nsame\tsecond\n",
                repeatedSchema);
            if (repeated.Rows.Count != 2 ||
                repeated.Rows[0].RequiredString(1) != "first" ||
                repeated.Rows[1].RequiredString(1) != "second")
            {
                throw new InvalidOperationException(
                    $"Generated-table {semantics} semantics did not preserve repeated row order.");
            }
        }

        byte[] manifestPayload = Encoding.UTF8.GetBytes("one\ntwo\n");
        string sha256 = Convert.ToHexString(SHA256.HashData(manifestPayload)).ToLowerInvariant();
        GeneratedTableManifest.ValidateMetadataForValidation(
            path, 1, manifestPayload, 2, 1, 2, sha256);
        ExpectGeneratedTableFailure(
            () => GeneratedTableManifest.ValidateMetadataForValidation(
                path, 2, manifestPayload, 2, 1, 2, sha256),
            "manifest schema version 1",
            "runtime expects 2");
        ExpectGeneratedTableFailure(
            () => GeneratedTableManifest.ValidateMetadataForValidation(
                path, 1, manifestPayload, 2, 1, 2, new string('0', 64)),
            "SHA-256",
            "does not match");
        ExpectGeneratedTableFailure(
            () => GeneratedTableManifest.ValidateMetadataForValidation(
                path, 1, manifestPayload, 1, 1, 2, sha256),
            "parsed 1 records",
            "expects 2");

        int manifestEntries = GeneratedTableManifest.EntryCount;
        if (manifestEntries <= 0)
            throw new InvalidOperationException("The generated-table manifest is empty.");
        GD.Print($"Validated shared generated-table schemas, named diagnostics, unique/grouped/" +
            $"aliased/repeated ordering, and {manifestEntries} manifest versions/counts/SHA-256 hashes.");
    }

    private static void ExpectGeneratedTableFailure(
        Action action,
        params string[] fragments)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            foreach (string fragment in fragments)
            {
                if (!exception.Message.Contains(fragment, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Generated-table diagnostic '{exception.Message}' omitted '{fragment}'.",
                        exception);
                }
            }
            return;
        }
        throw new InvalidOperationException(
            $"Expected generated-table failure containing: {string.Join(", ", fragments)}.");
    }
}

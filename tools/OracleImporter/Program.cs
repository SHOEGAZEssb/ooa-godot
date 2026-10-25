using System.Globalization;
using System.Text;

namespace Oracles.Importer;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 &&
                string.Equals(args[0], "serve", StringComparison.OrdinalIgnoreCase))
            {
                RunServer();
                return 0;
            }
            if (args.Length == 3 &&
                string.Equals(args[0], "manifest", StringComparison.OrdinalIgnoreCase))
            {
                GeneratedAssetManifest.Capture(args[1]).WriteTsv(args[2]);
                return 0;
            }

            Console.Error.WriteLine(
                "Usage: Oracles.Importer serve | manifest <asset-root> <output.tsv>");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void RunServer()
    {
        string root = Environment.GetEnvironmentVariable("OOA_DISASSEMBLY_ROOT")
            ?? throw new InvalidOperationException(
                "OOA_DISASSEMBLY_ROOT was not provided to the importer source host.");
        string[] symbols = (Environment.GetEnvironmentVariable("OOA_ASSEMBLY_SYMBOLS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var repository = new AssemblySourceRepository(root, symbols);
        int labelQueryCount = 0;
        int nodeQueryCount = 0;
        var utf8 = new UTF8Encoding(false, true);
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.WriteLine("READY\t2");
        Console.Out.Flush();

        ImportCache? importCache = null;
        try
        {
            while (Console.ReadLine() is { } request)
            {
                try
                {
                    int separator = request.IndexOf('\t');
                    string command = (separator < 0 ? request : request[..separator])
                        .TrimStart('\uFEFF');
                    string payload = separator < 0
                        ? string.Empty
                        : utf8.GetString(Convert.FromBase64String(request[(separator + 1)..]));
                    switch (command)
                    {
                        case "CACHE_BEGIN":
                        {
                            if (importCache is not null)
                                throw new InvalidDataException("Import cache session already started.");
                            string[] fields = payload.Split('\0');
                            if (fields.Length != 4)
                                throw new InvalidDataException("CACHE_BEGIN requires tools, ROM, output and force flag.");
                            if (fields[3] is not ("0" or "1"))
                                throw new InvalidDataException("CACHE_BEGIN force flag must be 0 or 1.");
                            importCache = new ImportCache(fields[0], root, fields[1], fields[2], string.Join(';', symbols),
                                Environment.GetEnvironmentVariable("OOA_IMPORT_EXECUTION_ENVIRONMENT") ?? string.Empty);
                            bool reused = importCache.TryReuse(fields[3] == "1");
                            WriteSuccess(reused ? $"HIT\t{importCache.AssetCount}" : $"MISS\t{importCache.Reason}", utf8);
                            break;
                        }
                        case "CACHE_COMPLETE":
                            (importCache ?? throw new InvalidDataException("Import cache session not started.")).Complete();
                            WriteSuccess(string.Empty, utf8);
                            break;
                        case "PNG_GRAYSCALE":
                        {
                            string[] fields = payload.Split('\0');
                            if (fields.Length != 4)
                                throw new InvalidDataException("PNG_GRAYSCALE requires path, width, height and pixels.");
                            GeneratedGrayscalePng.Write(fields[0],
                                int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture),
                                int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                                Convert.FromBase64String(fields[3]));
                            WriteSuccess(string.Empty, utf8);
                            break;
                        }
                        case "CUTSCENE_SCALAR":
                        {
                            int delimiter = payload.IndexOf('\0');
                            if (delimiter < 0)
                                throw new InvalidDataException("CUTSCENE_SCALAR requires shape and value.");
                            WriteSuccess(Oracles.CutsceneFieldShape.IsValid(
                                payload[..delimiter], payload[(delimiter + 1)..]) ? "1" : "0", utf8);
                            break;
                        }
                        case "TEXT":
                            WriteSuccess(repository.GetText(payload), utf8);
                            break;
                        case "SOURCE_LINE":
                        case "NODE_RANGE":
                        {
                            string[] fields = payload.Split('\0');
                            int expected = command == "SOURCE_LINE" ? 5 : 3;
                            if (fields.Length != expected)
                                throw new InvalidDataException($"{command} requires {expected} fields.");
                            AssemblySourceFile file = repository.Open(fields[0]);
                            int start = int.Parse(fields[1], CultureInfo.InvariantCulture);
                            int end = int.Parse(fields[2], CultureInfo.InvariantCulture);
                            nodeQueryCount++;
                            WriteSuccess(command == "SOURCE_LINE"
                                ? file.FindSourceLine(start, end, fields[3],
                                    int.Parse(fields[4], CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture)
                                : AssemblySourceQuery.SerializeRange(file, start, end), utf8);
                            break;
                        }
                        case "LABEL":
                        {
                            labelQueryCount++;
                            int delimiter = payload.IndexOf('\0');
                            if (delimiter < 0)
                                throw new InvalidDataException("LABEL requires path and label.");
                            AssemblySourceFile file = repository.Open(payload[..delimiter]);
                            WriteSuccess(file.GetLabelBlockText(payload[(delimiter + 1)..]), utf8);
                            break;
                        }
                        case "NODES":
                        case "LABEL_NODES":
                        case "LABELS":
                        case "DATA_DIRECTIVES":
                        case "MACRO_INVOCATIONS":
                        case "INSTRUCTIONS":
                        case "CONSTANTS":
                        {
                            nodeQueryCount++;
                            string[] fields = payload.Split('\0');
                            if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0]))
                            {
                                throw new InvalidDataException(
                                    $"{command} requires an assembly source path.");
                            }
                            AssemblySourceFile file = repository.Open(fields[0]);
                            string? label = fields.Length > 1 && fields[1].Length != 0
                                ? fields[1]
                                : null;
                            string? name = fields.Length > 2 && fields[2].Length != 0
                                ? fields[2]
                                : null;
                            WriteSuccess(
                                AssemblySourceQuery.Serialize(file, command, label, name),
                                utf8);
                            break;
                        }
                        case "STATS":
                            WriteSuccess(
                                $"{repository.LoadedFiles.Count}\t{repository.PhysicalReadCount}" +
                                $"\t{labelQueryCount}\t{nodeQueryCount}",
                                utf8);
                            break;
                        case "ASSERT":
                            repository.AssertReadOnce();
                            WriteSuccess(
                                $"{repository.LoadedFiles.Count}\t{repository.PhysicalReadCount}" +
                                $"\t{labelQueryCount}\t{nodeQueryCount}",
                                utf8);
                            break;
                        case "QUIT":
                            WriteSuccess(string.Empty, utf8);
                            return;
                        default:
                            throw new InvalidDataException(
                                $"Unknown assembly source host command '{command}'.");
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(
                        $"ERR\t{Convert.ToBase64String(utf8.GetBytes(exception.Message))}");
                    Console.Out.Flush();
                }
            }
        }
        finally
        {
            importCache?.Dispose();
        }
    }

    private static void WriteSuccess(string value, Encoding encoding)
    {
        Console.WriteLine($"OK\t{Convert.ToBase64String(encoding.GetBytes(value))}");
        Console.Out.Flush();
    }
}

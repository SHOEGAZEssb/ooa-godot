using Godot;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace oracleofages;

/// <summary>
/// Development-only bridge from the production executable to the separately
/// compiled validation assembly. No validation scenario code lives here.
/// </summary>
public partial class ValidationSceneHost : Node
{
    private const string ValidationAssemblyName = "oracle-of-ages.validation.dll";
    private const string ValidationRootType = "oracleofages.ValidationRoot";

    public override void _Ready()
    {
        try
        {
            string assemblyPath = FindValidationAssembly();
            AssemblyLoadContext gameLoadContext =
                AssemblyLoadContext.GetLoadContext(typeof(GameRoot).Assembly) ??
                AssemblyLoadContext.Default;
            Assembly assembly = gameLoadContext.LoadFromAssemblyPath(assemblyPath);
            Type rootType = assembly.GetType(ValidationRootType, throwOnError: true) ??
                throw new InvalidOperationException(
                    $"Validation assembly does not contain {ValidationRootType}.");
            if (Activator.CreateInstance(rootType) is not GameRoot validationRoot)
                throw new InvalidOperationException(
                    $"{ValidationRootType} is not a {nameof(GameRoot)}.");

            validationRoot.Name = "ValidationGame";
            var soundEngine = new OracleSoundEngine { Name = "SoundEngine" };
            validationRoot.AddChild(soundEngine);
            soundEngine.Owner = validationRoot;
            AddChild(validationRoot);
        }
        catch (Exception exception)
        {
            GD.PushError(
                "Could not start the validation assembly. Run `dotnet build` " +
                $"before using --validate.\n{exception}");
            GetTree().Quit(1);
        }
    }

    private static string FindValidationAssembly()
    {
        string[] candidates =
        [
            Path.Combine(
                ProjectSettings.GlobalizePath("res://"),
                "validation", "bin", "Debug", "net8.0", ValidationAssemblyName),
            Path.Combine(
                Path.GetDirectoryName(typeof(GameRoot).Assembly.Location) ?? string.Empty,
                ValidationAssemblyName)
        ];
        foreach (string candidate in candidates)
        {
            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        throw new FileNotFoundException(
            $"Could not find {ValidationAssemblyName}.", candidates[0]);
    }
}

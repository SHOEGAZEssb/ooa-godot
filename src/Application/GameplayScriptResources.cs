using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace oracleofages;

// Godot's first managed node constructor loads its CSharpScript and builds
// the native script bridge. Retain those resources during staged boot loading
// without constructing actors or running their initialization/update methods.
internal sealed class GameplayScriptResources
{
    private readonly Dictionary<string, CSharpScript> _scripts = new(StringComparer.Ordinal);

    internal IEnumerable<bool> Prepare()
    {
        foreach (string path in typeof(GameRoot).Assembly.GetTypes()
            .Where(type => typeof(Node).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters)
            .Select(type => type.GetCustomAttribute<ScriptPathAttribute>(inherit: false)?.Path)
            .OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (_scripts.ContainsKey(path)) continue;
            long started = Stopwatch.GetTimestamp();
            CSharpScript script = ResourceLoader.Load<CSharpScript>(path) ??
                throw new InvalidOperationException($"Could not prepare gameplay node script '{path}'.");
            _scripts.Add(path, script);
            if (OracleRoomLoadDiagnostics.IsEnabled)
                GD.Print(FormattableString.Invariant(
                    $"SCRIPT_LOAD path={path} ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}"));
            yield return false;
        }
    }

    internal void Clear() => _scripts.Clear();
}

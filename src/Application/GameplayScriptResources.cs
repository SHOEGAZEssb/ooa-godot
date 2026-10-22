using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace oracleofages;

// Godot's first managed node constructor loads its CSharpScript and builds
// the native script bridge. Retain those resources during staged boot loading.
// Two measured presenter types also need their native constructor path prepared;
// detached instances never enter the tree, factory or original object pools.
internal sealed class GameplayScriptResources
{
    private readonly Dictionary<string, CSharpScript> _scripts = new(StringComparer.Ordinal);
    private readonly HashSet<Type> _preparedConstructors = new();

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
        foreach (bool step in PrepareNativeConstructors()) yield return step;
    }

    internal IEnumerable<bool> PrepareNativeConstructors()
    {
        // Audited constructors contain only local field defaults/collections
        // and Godot's Node2D binding. Do not generalize this to arbitrary actors:
        // constructors may own source state. Initialize*, _Ready, updates,
        // callbacks and drawing must never run during resource preparation.
        foreach (var entry in new (Type Type, Func<Node> Create)[]
        {
            (typeof(LynnaShopItem), static () => new LynnaShopItem()),
            (typeof(TimePortal), static () => new TimePortal())
        })
        {
            if (_preparedConstructors.Contains(entry.Type)) continue;
            long started = Stopwatch.GetTimestamp();
            Node presenter = entry.Create();
            presenter.Free();
            _preparedConstructors.Add(entry.Type);
            if (OracleRoomLoadDiagnostics.IsEnabled)
                GD.Print(FormattableString.Invariant(
                    $"SCRIPT_CONSTRUCTOR type={entry.Type.Name} detached=true ms={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}"));
            yield return false;
        }
    }

    internal void Clear()
    {
        _scripts.Clear();
        _preparedConstructors.Clear();
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

// Retain class metadata, never instances of gameplay owners. Otherwise freeing
// the last node of a type between synchronous resets can unload its C# Script
// and repeat native script registration, property/signal dictionaries and their
// finalization on the next construction. This cache belongs to one worker.
internal sealed class ValidationScriptResources : IDisposable
{
    private readonly List<Script> _scripts = [];

    internal ValidationScriptResources()
    {
        try
        {
            foreach (string path in new[] { typeof(GameRoot).Assembly, typeof(ValidationRoot).Assembly }
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(GodotObject).IsAssignableFrom(type))
                .Select(type => type.GetCustomAttribute<ScriptPathAttribute>(inherit: false)?.Path)
                .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                _scripts.Add(ResourceLoader.Load<Script>(path) ??
                    throw new InvalidOperationException($"Cannot retain validation script resource '{path}'."));
            }
            GD.Print($"VALIDATION_SCRIPT_RESOURCES retained={_scripts.Count}");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (Script script in _scripts) script.Dispose();
        _scripts.Clear();
    }
}

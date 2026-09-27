# Generated-asset mods

Mods are optional overlays on the generated runtime assets. They do not change
the clean-ROM importer, the supported ROM check, or the canonical files below
`assets/oracle/`. A run with any enabled mod is outside the project's vanilla
fidelity guarantee.

## Directory and manifest

The default directory is Godot's per-user `user://mods` directory. During
development or when another launcher owns the installation, select an explicit
directory after Godot's `--` separator:

```powershell
& godot --path . -- --mods-dir='D:\Oracle\ooa-godot-mods'
```

Each immediate child directory is one mod. The repository includes a
synthetic two-mod example at `examples/mods/priority-demo/`:

```text
examples/mods/priority-demo/
├── low-priority/
│   ├── manifest.json
│   └── assets/oracle/menu/inventory_item_slots.tsv
└── high-priority/
    ├── manifest.json
    └── assets/oracle/menu/inventory_item_slots.tsv
```

The example uses fabricated table rows only; it contains no ROM-derived data
or game artwork. Its slot positions are intentionally artificial, so the
example is for demonstrating and testing resolution rather than for a normal
fidelity playthrough.

`manifest.json` has this initial format:

```json
{
  "id": "example-ui",
  "name": "Example UI",
  "version": "1.0.0",
  "priority": 100,
  "enabled": true
}
```

`id` and `version` are required. IDs must be unique and may contain ASCII
letters, digits, `.`, `_`, and `-`. `name`, `priority`, and `enabled` are
optional; their defaults are the ID, `100`, and `true`.

## Resolution and diagnostics

Mods are ordered by ascending numeric priority and then by ordinal ID. Asset
resolution checks that order in reverse, so the greatest priority wins; equal
priorities resolve to the ordinally greatest ID. Disabled mods remain visible
in startup diagnostics but never provide assets.

This first version accepts complete `.tsv` generated-table replacements and
complete `.png` image replacements. The path below `assets/oracle/` must match
the canonical asset exactly. Missing overrides always fall back to the
canonical generated asset.

Overridden tables still pass the production schema, header, key, and typed
value validation used by the vanilla table. Canonical tables continue to pass
the generated manifest's version, record-count, and SHA-256 checks. Mod files
are deliberately not added to or substituted for that clean-import manifest.

Startup output identifies the selected directory, every valid mod and its
priority, and the exact manifest path for malformed JSON, invalid fields, or
duplicate IDs. An invalid manifest is skipped without disabling valid mods.

Use `--no-mods` after Godot's `--` separator for a deterministic vanilla run:

```powershell
& godot --path . -- --no-mods
```

### Run the synthetic priority example

From the Godot project root, point the game at the example directory:

```powershell
$godot = 'C:\path\to\Godot_v4.7.1-stable_mono_win64.exe'
$mods = Join-Path $env:TEMP ("ooa-priority-demo-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $mods | Out-Null
Copy-Item '.\examples\mods\priority-demo\*' -Destination $mods -Recurse
& $godot --path . -- --mods-dir="$mods"
```

At startup, diagnostics list both example mods; when the inventory slot table
loads, `sample-layout-high` wins because its priority is higher. Set
`"enabled": false` in `$mods\high-priority\manifest.json` and relaunch to
select the lower-priority mod. Disable both manifests in that temporary copy
to fall back to the generated vanilla table. Passing `--no-mods` ignores both
regardless of their manifest settings and returns to vanilla for that run.

Run the automated check for priority, disabled fallback, vanilla fallback, and
the no-mods runtime path with:

```powershell
dotnet run --project tools/ModSupport.Tests/ModSupport.Tests.csproj
```

Validation runs disable mods automatically. IPS/BPS patches, runtime scripts,
audio, raw binary assets, and importer changes are intentionally outside this
first overlay contract.

# Compatibility and chart extensions

## Current snapshot

InFalsus Studio+ public version **1.06**, integrated assembly **1.0.77.0**. The original Unity editor core remains 1.0.10. The current plugin preserves every pasted note's SkyArea `groupid` and timinggroup; missing timinggroups are recreated with captured header properties. This intentionally differs from the unmodified core's default paste behavior. Other pre-existing groups are not repurposed.

The universal `output: spc` interface supports syntax-checked SPC rows and append/replace/delete patches, including supported nonofficial statements and opaque function-like statements. It retains unknown text without claiming playback semantics or official-game compatibility. See [EXTERNAL_TOOLS.md](EXTERNAL_TOOLS.md).

The deletion/copy Timing tools now have built-in quick-tool entries; their legacy scripts remain compatible. Bracket tunnel and random scatter are optional add-ons, not extra mandatory defaults. Existing tools are not rewritten just to use the universal interface.

## Runtime target

- Editor plugin: InFalsus Studio Windows Mono 1.0.10, `Assembly-CSharp.dll` SHA-256 `C4C3D6118A19F9BCB1F7D33B29C340DB0F33580655744473B23187E9D3C4E417`.
- Community player target for the chart extension: InFalsus shell `inf-mod6`, version 1.0.6, build 25797904. A public source repository/tag for that shell was not supplied.
- Editor-side integration and source regression records exist for the plugin build; the actual scopes and historical/current distinction are listed in [VALIDATION.md](VALIDATION.md). Loading and playing timinggroup charts in the target game shell, including judgment behavior, have **not been verified** by this commit.

## Extension syntax

The optional plugin recognizes these nonofficial chart statements:

```text
timinggroup(name):
    ... notes and timing events ...
groupend()
hidegroup(time_ms, 0_or_1)
```

`timinggroup(name,noinput):` and `timinggroup(name,noclip):` are also accepted. Names contain 1–64 Unicode letters/digits, `_` or `-`, and cannot begin with a digit. Groups cannot nest or reuse a name; the `chart(...)` header belongs in the base section. Group markers delimit following source lines through `groupend()`.

All groups share absolute song time. Each group has its own event stream. A non-base group inherits base `track` and `bpm` events of each kind until it defines its first local event of that kind; after that, that kind follows the local stream. `track` controls scroll speed; `bpm` controls beat-grid and count estimates. SkyArea `groupid` remains a separate visual/head-connection value and does not encode timinggroup membership.

`noinput` notes remain visible but are excluded from editor preview judgment, autocursor hits, hit sounds, and counts; the editor preview clips them at its judgment line. `noclip` also disables judgment and continues rendering past the line. These describe editor behavior. Their gameplay semantics in a player are not certified here. `hidegroup` applies to the current timinggroup: `1` hides its note visibility and `0` shows it; the latest event at or before the selected absolute time applies. Visibility does not itself suppress judgment, EXACT/BREAK text, hit effects or sound. Build 1.0.76 removes an erroneous visibility filter from judgement text only; noinput/noclip filtering remains unchanged.

Legacy external timing tools operate within the selected timinggroup. Universal SPC patches can explicitly target base/other timinggroups or complete group blocks, with request-snapshot and authoring validation. Defaults remain in `ExternalTools/Default`; optional generators are in `ExternalTools/Optional`, documented in the [tool overview](EXTERNAL_TOOLS.md) and individual tool READMEs.

## Official compatibility and export

The official editor/game format does not implement `timinggroup`, `noinput`, `noclip`, or `hidegroup` semantics. Without the optional plugin, the original editor has no timinggroup editor. With the plugin installed, Save and Save As preserve the extension statements as chart text; this version does not flatten them, remove them, or block saving as an official-format conversion. Do not treat a saved extended chart as playable by an official build. Use a player known to support the exact extension, and validate gameplay separately.

Installation is the explicit opt-in for the editor extension. Once installed, the plugin automatically recognizes the statements when present; it does not silently enable a per-chart official mode or migrate a chart on open. This commit does not add a compatibility toggle or an official-export conversion path.

## Other visual boundary

The plugin's optional out-of-bounds Sky authoring grid and Alt handles are editor features. They are not a guarantee that all modified players support every generated coordinate. Respect the target player's supported ranges and validate gameplay separately.

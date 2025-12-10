# Glimmer

GPU-accelerated shimmer loading placeholders for Unity UI.

## Installation

### Via OpenUPM (Recommended)

Install via [OpenUPM CLI](https://openupm.com/):

```bash
openupm add com.iraklichkuaseli.glimmer
```

Or add the package via OpenUPM scoped registry in `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.iraklichkuaseli"]
    }
  ],
  "dependencies": {
    "com.iraklichkuaseli.glimmer": "1.0.0"
  }
}
```

### Via Git URL

Add this line to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.iraklichkuaseli.glimmer": "https://github.com/irakli/glimmer.git"
  }
}
```

Or install via Package Manager:
1. Open Window → Package Manager
2. Click the `+` button
3. Select "Add package from git URL"
4. Enter: `https://github.com/irakli/glimmer.git`

### Requirements

- Unity 2022.3 or later
- Unity 6000+ for async `ShowAsync`/`HideAsync`
- TextMeshPro

## Features

- **GPU shimmer** - Shader-based animation, zero CPU overhead
- **Explicit targets** - Assign Graphics in Inspector, use "Refresh Targets" to auto-discover
- **Live preview** - See effect in Editor without Play mode
- **Non-destructive** - Original materials restored on Hide()

## Usage

### Basic

```csharp
[SerializeField] private GlimmerGroup glimmer;

public void Load()
{
    glimmer.Show();

    // ... fetch data ...

    glimmer.Hide();
}
```

### Async (Unity 6000+)

```csharp
public async Awaitable LoadAsync()
{
    await glimmer.ShowAsync(0.2f);  // Fade in

    var data = await FetchDataAsync();
    DisplayData(data);

    await glimmer.HideAsync(0.3f);  // Fade out
}
```

### Runtime Configuration

```csharp
// Change colors at runtime
glimmer.SetColors(
    baseColor: new Color(0.9f, 0.9f, 0.9f, 0.5f),
    shimmerColor: new Color(1f, 1f, 1f, 0.5f)
);

// Change animation parameters
glimmer.SetAnimation(
    duration: 1.5f,   // Shimmer cycle duration
    angle: 25f,       // Shimmer angle (-45 to 45)
    width: 0.25f      // Shimmer band width (0.1 to 0.5)
);
```

## Components

| Component | Purpose |
|-----------|---------|
| `GlimmerGroup` | Main controller - add as sibling after text targets |
| `GlimmerElement` | Per-element overrides (ignore, corner radius) |

### GlimmerElement

Add to any target Graphic for per-element control:

| Property | Description |
|----------|-------------|
| `IgnoreGlimmer` | Exclude this element from the shimmer effect |
| `CornerRadius` | Override corner radius (when enabled) |

## Setup

**Create via menu:** GameObject > UI > Glimmer Group

**Important:** Place `GlimmerGroup` **after** any sibling TextMeshPro elements.

```
Container
├── Image
├── TMP_Text      ← text target (index 1)
├── GlimmerGroup  ← must be after text (index 2+)
└── Background    ← other elements can be after
```

**Why?** Unity UI renders siblings in hierarchy order. For TextMeshPro elements, GlimmerGroup renders its own mesh quads as placeholders (the text is made transparent). If GlimmerGroup is before a text target, the placeholder will be obscured.

The Inspector will warn you if GlimmerGroup is incorrectly positioned and offer a "Move After Text Targets" button to fix it.

## API Reference

### GlimmerGroup

| Method | Description |
|--------|-------------|
| `Show()` | Activates glimmer effect |
| `Hide()` | Hides glimmer, restores materials |
| `Toggle()` | Toggles between Show/Hide |
| `ShowAsync(duration, ct)` | Fade-in animation (Unity 6000+) |
| `HideAsync(duration, ct)` | Fade-out animation (Unity 6000+) |
| `SetColors(base, shimmer)` | Change colors at runtime |
| `SetAnimation(duration, angle, width)` | Change animation parameters |
| `Initialize()` | Pre-warm glimmer (call during screen setup) |
| `Refresh()` | Force refresh of all materials |
| `RefreshTargets()` | (Editor) Re-discover Graphics in hierarchy |
| `ClearTargets()` | (Editor) Clear all target Graphics |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsShowing` | `bool` | Whether glimmer is currently visible |
| `TargetGraphics` | `IReadOnlyList<Graphic>` | List of target Graphics |

### Events

| Event | Description |
|-------|-------------|
| `StateChanged` | Fired when `IsShowing` changes (parameter: bool isShowing) |
| `PropertiesChanged` | Fired when colors or animation parameters change |

## Advanced Usage

### Async Cancellation

`ShowAsync` and `HideAsync` support cancellation tokens. If cancelled mid-animation, they throw `OperationCanceledException`:

```csharp
var cts = new CancellationTokenSource();

try
{
    await glimmer.ShowAsync(0.5f, cts.Token);
}
catch (OperationCanceledException)
{
    // Animation was cancelled - call Hide() to reset state
    glimmer.Hide();
}
```

### Material Lifecycle

GlimmerGroup manages materials non-destructively:
- Original materials and colors are saved during `Show()`
- Cached glimmer materials are reused across Show/Hide cycles
- Original materials are restored on `Hide()`
- All cached materials are destroyed on component destruction

### Component Enable/Disable

If GlimmerGroup is disabled while showing, it will automatically resume showing when re-enabled.

### Pre-warming

Call `Initialize()` during screen setup to avoid first-show latency:

```csharp
private void Start()
{
    glimmer.Initialize(); // Pre-load shader, ready for instant Show()
}
```

## Package Structure

```
com.iraklichkuaseli.glimmer/
├── package.json             # UPM package manifest
├── README.md                # This file
├── CHANGELOG.md             # Version history
├── LICENSE.md               # MIT License
├── Runtime/
│   ├── GlimmerGroup.cs      # Main controller component
│   ├── GlimmerElement.cs    # Per-element overrides
│   ├── IrakliChkuaseli.Glimmer.asmdef
│   └── Shaders/
│       └── Glimmer.shader   # GPU shimmer shader
├── Editor/
│   ├── GlimmerGroupEditor.cs    # Custom inspector
│   ├── GlimmerElementEditor.cs  # Element inspector
│   └── IrakliChkuaseli.Glimmer.Editor.asmdef
├── Icons/
└── Tests/
    ├── Editor/
    └── Runtime/
```

## Troubleshooting

### Glimmer not showing on TextMeshPro elements

**Problem:** Text placeholders are invisible or behind other elements.

**Solution:** Ensure `GlimmerGroup` is positioned **after** all `TMP_Text` targets in the hierarchy. The Inspector shows a warning and "Move After Text Targets" button if this is misconfigured.

### Shader not found error

**Problem:** Console shows "Shader 'UI/Glimmer/Shimmer' not found".

**Solution:**
1. Verify the package is fully imported (check `Runtime/Shaders/Glimmer.shader` exists)
2. Try reimporting the package via Package Manager
3. Check for shader compilation errors in the Console

### Materials not restoring after Hide()

**Problem:** Original materials look different after calling `Hide()`.

**Solution:** This can happen if you modify target Graphics while glimmer is showing. Call `Refresh()` after modifying targets, or call `Hide()` before making changes.

### Async methods not available

**Problem:** `ShowAsync` and `HideAsync` don't exist.

**Solution:** These methods require Unity 6000+. On older Unity versions, use synchronous `Show()`/`Hide()` methods.

### Corner radius looks wrong

**Problem:** Rounded corners have artifacts or look cut off.

**Solution:** Corner radius is automatically clamped to half the minimum rect dimension. For very small elements, reduce the corner radius.

## License

MIT

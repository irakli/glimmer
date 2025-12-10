# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-12-10

### Changed
- Minimum Unity version updated to 2022.3 (required for `Undo.SetSiblingIndex`)
- Text material is now cached and reused across Show/Hide cycles (performance improvement)

## [1.0.0] - 2025-12-10

### Added
- GlimmerGroup component for shimmer loading effect
- GlimmerElement component for per-element overrides (ignore, corner radius)
- GPU-based shimmer shader with SDF rounded corners
- Edit-time target discovery with serialized list
- Runtime `SetColors()` and `SetAnimation()` APIs
- Async `ShowAsync()`/`HideAsync()` with fade transitions (Unity 6000+)
- Custom Inspector with live preview toggle
- `StateChanged` event for show/hide state changes
- `PropertiesChanged` event for property change notifications
- `Initialize()` method for pre-warming glimmer system
- `Refresh()` method for forcing material updates

### Known Limitations
- GlimmerGroup must be placed **after** text targets in the hierarchy (siblings render in order)
- Async methods require Unity 6000+ (use synchronous Show/Hide on older versions)
- Corner radius is clamped to half the minimum rect dimension to prevent artifacts
- Sprite atlas support requires sprites to have proper outer UV bounds

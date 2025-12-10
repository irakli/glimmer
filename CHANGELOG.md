# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

# Tikim MockScene

[Back to Repository README](../../README.md) · [한국어 문서](../../docs/README.ko.md)

`org.tikim.mockscene` is a Unity package for managing scene-scoped mock scenarios before entering play mode.

## What is included

- `MockScenePreset`
- `MockSceneSceneSettings`
- `MockSceneInjector`
- `Tools > MockScene Manager`

## Installation note

If you install this package from Git, install `NaughtyAttributes` first:

1. `https://github.com/dbrizov/NaughtyAttributes.git#upm`
2. `https://github.com/xhdtn8070/mock-scene.git?path=/LocalPackages/org.tikim.mockscene#v1.0.0`

Fallback for untagged development:

`https://github.com/xhdtn8070/mock-scene.git?path=/LocalPackages/org.tikim.mockscene#main`

This package is also available on OpenUPM:

- Package page: [openupm.com/packages/org.tikim.mockscene](https://openupm.com/packages/org.tikim.mockscene/)

Install it with:

```bash
openupm add org.tikim.mockscene
```

## Authoring workflow

1. Save the target scene.
2. Open `Tools > MockScene Manager`.
3. Create scene settings and one or more presets.
4. Mark one preset as active.
5. Add `MockSceneInjector` to a scene object.
6. Enter play mode from the manager window.

## Current behavior

The injector currently resolves the active preset in editor play mode and prints a rich `Debug.Log` summary. This is the intended v1 integration point before wiring game-specific data injection.

## Package metadata

- Documentation: [Repository README](../../README.md)
- Korean docs: [docs/README.ko.md](../../docs/README.ko.md)
- Changelog: [CHANGELOG.md](./CHANGELOG.md)
- License: [MIT](../../LICENSE)

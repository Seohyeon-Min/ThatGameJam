# Add the Unity Pipeline package and `image.png`

## Why

Driving the Editor from the Unity CLI needs `com.unity.pipeline`. `image.png` stays on `sam` only.

## What changed

| What | Where |
|------|-------|
| Added `com.unity.pipeline` `0.9.1-exp.1` (`unity pipeline install`) | `Packages/manifest.json`, `Packages/packages-lock.json` |
| Image | `image.png` |

## Verification

| What | Result |
|------|--------|
| `unity status --until-ready` | port 7800, `ready` |
| `unity command editor status` | `"status":"ready"` |

## Blocker

An Editor started with `unity open` exited right after the license check. The cause was not found. Starting `Unity.exe -projectPath <path>` directly worked.

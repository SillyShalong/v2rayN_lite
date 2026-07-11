# v2rayN Lite

`v2rayN_lite` is a trimmed fork of [2dust/v2rayN](https://github.com/2dust/v2rayN) focused on a cleaner desktop distribution.

## Goals

- Provide a Windows self-contained build that is easy to run and redistribute.
- Keep core proxy-client features while reducing unrelated external communication.
- Bundle required cores during packaging so `xray.exe` does not need to be copied manually.

## Kept Features

- Local proxy, system proxy, routing, and other core proxy features.
- Tray integration and state icons.
- Latency test, download speed test, and mixed speed test.
- Windows self-contained packaging script.
- Manual subscription refresh.
- One-click manual Xray core update.
- Manual geodata update.

## Disabled or Trimmed by Default

- Automatic updates for the app itself and non-Xray cores.
- Subscription auto-update timers remain disabled; manual subscription refresh is supported.
- WebDAV backup external calls.
- Geo, SRS, and rule-template external source settings.
- IP lookup style non-core website communication.

To keep speed test support, manual subscription refresh, one-click Xray core update, and manual geodata update, the lite build only allows those explicit update paths plus configured speed-test URLs. Other non-core outbound communication remains blocked by default.

## Build

Run this from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows-selfcontained.ps1
```

The script will:

1. Download the required .NET SDK into `.tools/dotnet` if it is missing.
2. Publish the Windows self-contained app.
3. Download the matching upstream `v2rayN-core-bin` bundle.
4. Copy the bundled cores into `publish\bin` and merge them into the final package.
5. Produce a distributable zip archive.

## Build Outputs

Default outputs are under `artifacts`:

- `artifacts\win-x64\publish\v2rayN.exe`
  This folder is directly runnable and includes bundled cores such as `publish\bin\xray\xray.exe`.
- `artifacts\v2rayN-lite-windows-64-selfcontained.zip`
  This is the final distributable archive.

## Icons

This fork refreshes the application and tray icons with a larger mouse mark and clearer state badges for better readability at small sizes.

## Upstream

- Upstream repository: <https://github.com/2dust/v2rayN>
- This repository is a custom lite distribution branch and is not an official upstream release.
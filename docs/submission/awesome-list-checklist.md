# Awesome PowerToys Run Plugins Submission Checklist

Use this file to keep the evidence needed for the future pull request to
`hlaueriksson/awesome-powertoys-run-plugins`.

## Timing

- Earliest PR date: 2026-07-29
- Reason: upstream requires the plugin to be public for at least 30 days.

## Release

- GitHub release: https://github.com/RosenCodeDev/PowerToys-Run-PromptLibrary/releases/tag/v1.0.0
- x64 asset: https://github.com/RosenCodeDev/PowerToys-Run-PromptLibrary/releases/download/v1.0.0/PromptLibrary-1.0.0-x64.zip
- ARM64 asset: https://github.com/RosenCodeDev/PowerToys-Run-PromptLibrary/releases/download/v1.0.0/PromptLibrary-1.0.0-ARM64.zip

## VirusTotal

Results verified 2026-08-16 (0/67 detections for both published assets):

```text
x64: https://www.virustotal.com/gui/file/655d4ab294741991305229e0dc7ffaa31856d8f37f959a6a9ad05504b37f552f/detection
ARM64: https://www.virustotal.com/gui/file/9b7cca252814737c31509f83e07b9fc81b6f11fbfbacd056d7325bd4987b11b6/detection
```

## Validation

- Logic tests: all 15 passed on 2026-08-16.
- Release builds: x64 and ARM64 succeeded with 0 warnings and 0 errors on 2026-08-16.
- Published x64 SHA-256: `655d4ab294741991305229e0dc7ffaa31856d8f37f959a6a9ad05504b37f552f`
- Published ARM64 SHA-256: `9b7cca252814737c31509f83e07b9fc81b6f11fbfbacd056d7325bd4987b11b6`
- `ptrun-lint` 0.6.0: the only findings are `PTRUN1501` messages requiring
  `net9.0`; PowerToys 0.100.2 host assemblies target .NET 10, so the plugin
  remains on `net10.0-windows10.0.22621.0` and the false positive will be
  reported upstream.

## Upstream Entry

```md
- [PromptLibrary](https://github.com/RosenCodeDev/PowerToys-Run-PromptLibrary) - Search, preview, and copy prompts from a local JSON prompt library.
```

## PR Title

```text
Add PromptLibrary
```


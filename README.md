# PromptLibrary - PowerToys Run Plugin

PromptLibrary is a native PowerToys Run plugin for searching, previewing, and
copying prompts from a local JSON prompt library.

## Demo

### Search

![Search](assets/readme-recording-2.gif)

### Tag Filtering

![Tag Filtering](assets/readme-recording-1.gif)

## Features

- **Weighted Token Search**: Searches prompt names, bodies, tag names, and tag descriptions with relevance scoring.
- **Tag List**: Type `tag` to list available tags; selecting one filters prompts by that tag.
- **Explorer-Style Navigation**: Configurable Back, Forward, and Reset shortcuts for searches and tags.
- **Quick Library Access**: Type `/p +` to edit the active prompt file, edit the tag file, or open the data folder.
- **Zero-Restart Hot-Reload**: Automatically reloads valid JSON changes without restarting PowerToys.
- **Resilient Loading**: Keeps the last valid library after a malformed save and skips malformed individual records.
- **Clipboard Copy**: Copies the selected prompt safely and immediately.
- **PowerToys Settings Integration**: Configure navigation shortcuts, the library files command, result count, auto-reload, icons, and a custom data folder.
- **Multi-Architecture Support**: Native packages for x64 and ARM64.
- **Context Menu Actions**: Copy a prompt with its title, copy only its title, or open the data folder.

## Requirements

- Windows with Microsoft PowerToys installed.
- PowerToys 0.100.0 or newer is recommended.
- .NET 10 runtime support, supplied by current PowerToys builds.

## Installation

### Install PromptLibrary

1. Download the zip for your platform from the latest GitHub Release:
   - **x64**: `PromptLibrary-1.0.0-x64.zip`
   - **ARM64**: `PromptLibrary-1.0.0-ARM64.zip`
2. Quit PowerToys from the system tray.
3. Extract the zip. It contains a `PromptLibrary` folder.
4. Copy that folder to `%LOCALAPPDATA%\PowerToys\RunPlugins\`.
5. Restart PowerToys.

The release includes a small sample prompt library in `Data/` so the plugin works immediately while keeping the download easy to inspect.

For a larger starting collection, this repository also includes a public `sample prompts/` folder. To use it, replace the installed files in `PromptLibrary\Data\` with:

- `sample prompts\user.prompt.json`
- `sample prompts\user.prompt.tag.json`

## Usage

1. Open PowerToys Run with `Alt+Space`.
2. Type `/p` followed by a query.
   - Search example: `/p meeting`
   - Tag list: `/p tag`
   - Library files: `/p +`
3. Press Enter to copy the selected prompt body.
4. Press Tab or right-click a prompt result for additional actions.

The PowerToys action keyword and the `+` library files command can both be changed
in settings. The library files menu always contains:

1. **Edit prompts JSON**
2. **Edit tags JSON**
3. **Open data folder**

JSON files open in the Windows-associated application, with Notepad as a fallback.

### Navigation Shortcuts

- **Back** (`Alt+Left`): Return to the previous tag level or clear a first-level phrase.
- **Forward** (`Alt+Right`): Restore a level after using Back.
- **Reset** (`Alt+Up` or `Alt+Down`): Return to the empty PromptLibrary root and discard navigation history.

Navigation history belongs to the current PromptLibrary session. Copying a prompt,
closing PowerToys Run, or leaving the `/p` query starts the next activation with
fresh history.

## Library Configuration

PromptLibrary reads:

- `user.prompt.json`: prompt titles and bodies.
- `user.prompt.tag.json`: optional tags associated with prompts.

See [PromptLibrary JSON Guide](JSON-GUIDE.md) for minimal examples, optional
metadata, tag IDs, multiple tags, and JSON editing rules.

By default, these files are loaded from the installed plugin's `Data/` folder.
With auto-reload enabled, a valid save is reflected after approximately 500 ms.

### Store Prompts Externally

To protect a personal library from plugin-folder replacement and optionally sync
it with OneDrive, Dropbox, or git:

1. Copy the JSON files to a folder you control.
2. Open **PowerToys Settings** -> **PowerToys Run** -> **Plugins** -> **PromptLibrary**.
3. Set **Custom data folder path** to that folder.

## Development And Building

The project requires the .NET 10 SDK and a local PowerToys installation for
build-time references.

```powershell
$env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"
.\build.ps1
```

The build creates:

- `release\PromptLibrary-1.0.0-x64.zip`
- `release\PromptLibrary-1.0.0-ARM64.zip`
- `release\checksums.txt`

## License

This project is licensed under the [MIT License](LICENSE).

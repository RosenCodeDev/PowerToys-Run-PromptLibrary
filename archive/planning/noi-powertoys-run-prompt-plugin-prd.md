# Noi Prompt Search for PowerToys Run - Product Specification

## Overview

Build a Microsoft PowerToys Run plugin that lets a user search, preview, and copy prompts exported from the Noi app. The plugin should read local JSON prompt files, display matching prompts inside PowerToys Run, and copy the selected prompt when the user presses Enter or clicks the result row.

The goal is to preserve the user's existing Noi prompt library while making it available through a launcher they already use daily.

## User Need

The user no longer uses the Noi app, but still has valuable prompt data stored locally. The important data is:

- Prompt title, stored as `act`
- Prompt body, stored as `prompt`
- Prompt tag/category IDs, stored as `tags`
- Tag/category metadata, stored separately with tag `id`, `name`, `description`, and color

The user wants fast access to these prompts from PowerToys Run without opening Noi or another prompt manager.

## Source Data

The plugin should support two JSON files:

- `user.prompt.json`
- `user.prompt.tag.json`

The current Noi export shape is:

```json
{
  "act": "[Starter] Quality Output",
  "prompt": "A paying client is receiving this output directly...",
  "tags": [
    "035c4fcf086f4452bc1a155d090c5c94",
    "f9e2e02e12ac46088665a856113cc993"
  ],
  "id": "713b0dc9d5364cb7bcce3b6d0d950c7e"
}
```

Tags are stored separately:

```json
{
  "name": "Consulting",
  "description": "",
  "id": "edd9701d0034415d9458182f31a0c802"
}
```

## Product Goals

- Make Noi prompts searchable from PowerToys Run.
- Search across prompt title, prompt body, and tag/category names.
- Copy a prompt with the same interaction style as existing PowerToys Run results.
- Allow the prompt library to be updated by replacing or editing JSON files in the plugin folder.
- Keep all data local; no network access is required.

## Non-Goals

- Do not rebuild the Noi app.
- Do not provide prompt editing inside PowerToys Run.
- Do not sync prompts to cloud services.
- Do not require a fixed, compiled-in prompt database.
- Do not require a custom visual UI beyond standard PowerToys Run result rows and context actions.

## Plugin Location and Data Flexibility

The plugin should be distributed as a normal PowerToys Run plugin folder.

The preferred structure is:

```text
NoiPrompts/
  plugin.json
  NoiPrompts.PowerToysRun.dll
  Images/
    prompt.dark.png
    prompt.light.png
  Data/
    user.prompt.json
    user.prompt.tag.json
```

The plugin should load prompts from the `Data` folder inside its own plugin directory by default.

This makes the prompt library user-editable. If the user wants to change available prompts or tags in the future, they can update:

- `Data/user.prompt.json`
- `Data/user.prompt.tag.json`

After updating the JSON files, the plugin should refresh the data. The simplest acceptable behavior is to reload files when PowerToys Run restarts. A better version can watch file timestamps and reload automatically when the files change.

## Core User Flow

1. User opens PowerToys Run.
2. User types the plugin activation keyword, such as `p:`.
3. User types search terms, such as `p: consulting rewrite`.
4. Plugin displays matching prompts.
5. User clicks a result row or presses Enter.
6. Plugin copies the full prompt body to the clipboard.
7. PowerToys Run closes after the copy action.

## Primary Interaction Design

Each result row should use the standard PowerToys Run primary action.

The primary action should:

- Copy the prompt body to the Windows clipboard.
- Return `true` so PowerToys Run hides/closes after selection.

This should support both expected selection methods:

- Pressing Enter on the selected result.
- Clicking/selecting the result row with the mouse.

This matches the behavior the user already recognizes from existing PowerToys Run features, such as calculator results that can be copied from the result row.

## Result Row Design

Each prompt result should display:

- Title: prompt `act`
- Subtitle: tag names plus a short preview of the prompt body
- Icon: plugin icon
- Tooltip or expanded text: fuller prompt preview if supported cleanly by PowerToys Run

Example:

```text
[Starter] Quality Output
Consulting, Writing - A paying client is receiving this output directly...
```

If a prompt has no matching tags, the subtitle should still show a prompt preview.

## Context Actions

The MVP does not require a separate copy icon because the whole result row should copy the prompt through the primary action.

Optional context actions may include:

- Copy prompt title
- Copy prompt body
- Copy prompt body with title
- Open plugin data folder

These are secondary and should not replace the primary click/Enter copy behavior.

## Search Behavior

The plugin should tokenize the user's query and match against:

- Prompt title, highest priority
- Tag/category names, high priority
- Prompt body, medium priority
- Tag descriptions, low priority if present

Ranking should prefer:

- Exact title match
- Prefix title match
- Exact tag match
- Multiple query terms matched across title and tags
- Body text matches

The search should be case-insensitive.

For the current data size, a simple in-memory search is sufficient. A fuzzy matching library can be added later, but it is not required for the MVP.

## Activation Keyword

Recommended default activation keyword:

```text
p:
```

Alternative:

```text
prompt
```

The plugin should not be global by default, to avoid noisy prompt results appearing in unrelated PowerToys Run searches. The user can enable global results later from the PowerToys Run plugin manager if desired.

The default activation keyword is set in `plugin.json`. Users can override it at any time from the PowerToys Run plugin manager in the PowerToys Settings app — this is standard PowerToys behavior and does not require any special code in the plugin.

## Configuration

Minimum configuration:

- Default data folder: `Data` inside the plugin folder.
- Default activation keyword: `p:`.

Nice-to-have configuration:

- Custom prompt JSON path.
- Custom tag JSON path.
- Toggle for global search.
- Maximum number of results.
- Toggle for automatic reload on file changes.

## Error Handling

If the JSON files are missing, invalid, or unreadable, the plugin should show a clear PowerToys Run result such as:

```text
Noi prompt data not found
Add user.prompt.json and user.prompt.tag.json to the plugin Data folder.
```

If tags are missing or unknown, the plugin should still load prompts and display them without tag names.

If clipboard copy fails, the plugin should return a visible error result or notification if supported.

## Technical Approach

Implement as a C# PowerToys Run plugin using the existing Wox.Plugin interfaces.

Expected components:

- `plugin.json` for plugin metadata, activation keyword, icon paths, and DLL name.
- `Main.cs` implementing the PowerToys Run plugin interface.
- Prompt loader that parses the two JSON files.
- Tag resolver that maps tag IDs to tag names.
- Search/ranking service.
- Clipboard copy action attached to each result row.

The result action should use the standard PowerToys Run result `Action` callback. This keeps Enter and row-click behavior consistent with existing PowerToys Run plugins.

### Known Technical Constraints

These are common points of failure for first-time PowerToys Run plugin builds. Addressing them upfront will prevent debugging time.

**1. Target the correct .NET version.**
The plugin must target the same .NET version that the user's installed PowerToys uses. As of current PowerToys releases this is .NET 9. If the plugin targets a different version, it will silently fail to load in the plugin manager with no error message. Confirm the exact version before creating the project.

**2. Do not copy PowerToys shared DLLs into the plugin output folder.**
The plugin references PowerToys Run interfaces (`Wox.Plugin.dll`, `PowerLauncher.dll`, etc.) at build time, but these DLLs must not be included in the final plugin folder. They are already loaded by the PowerToys host process, and bundling them causes version conflicts that prevent the plugin from loading. Set all PowerToys interface references to `CopyLocal = false` in the project file.

**3. Clipboard access requires an STA thread.**
The Windows clipboard API requires the calling thread to be in Single-Thread Apartment (STA) mode. The PowerToys Run `Action` callback does not guarantee this. The clipboard copy in the result action must be dispatched explicitly on an STA thread (e.g., via `System.Windows.Application.Current.Dispatcher` or a dedicated STA thread). Calling the clipboard API directly from the callback will throw an intermittent exception that is difficult to reproduce.

## Acceptance Criteria

- Plugin appears in PowerToys Run plugin manager after being placed in the PowerToys Run plugin folder and PowerToys is restarted.
- Typing the activation keyword shows prompt search results.
- Query terms match prompt titles.
- Query terms match prompt body text.
- Query terms match tag/category names.
- Result rows display the prompt title and tag/category context.
- Pressing Enter on a result copies the full prompt body to the clipboard.
- Clicking/selecting a result row copies the full prompt body to the clipboard.
- Updating the JSON files changes future search results after reload or restart.
- Missing tag IDs do not prevent prompts from loading.
- Invalid or missing data files show a clear error result instead of crashing the plugin.

## MVP Scope

The first version should include:

- Local JSON loading from plugin `Data` folder.
- Search over title, prompt body, and tag names.
- Ranked result rows.
- Enter and row-click copy behavior.
- Basic missing-file and invalid-JSON handling.

## Future Enhancements

- Automatic file watching and live reload.
- Settings panel for custom JSON file locations.
- More advanced fuzzy search.
- Context action to copy prompt with title.
- Context action to open the data folder.
- Optional import script that copies Noi files into the plugin `Data` folder.

## Implementation Difficulty

This is a moderate PowerToys Run plugin project.

The prompt data model is simple and small, so search and loading are low-risk. The main implementation risk is PowerToys plugin packaging and compatibility with the installed PowerToys version. A build environment with the correct .NET SDK and matching PowerToys Run plugin references will be needed. See the Known Technical Constraints section above for the three most common build-time issues.

Estimated effort:

- MVP: a few hours once the build environment is ready.
- Polished version with settings and auto-reload: about one day.

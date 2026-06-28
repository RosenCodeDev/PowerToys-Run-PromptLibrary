# PromptLibrary JSON Guide

PromptLibrary reads a prompt collection from `user.prompt.json` and, optionally, a
tag collection from `user.prompt.tag.json`. Both files use a top-level JSON array.

The examples below show the smallest useful format first. Additional metadata is
optional and can help when the same library is shared with other prompt-management
tools.

## Prompts

Every prompt needs an `act` title and a `prompt` body:

```json
[
  {
    "act": "Rewrite Clearly",
    "prompt": "Rewrite the following text clearly while preserving its meaning."
  }
]
```

Prompt fields:

| Field | Required | Purpose |
| --- | --- | --- |
| `act` | Yes | Title shown and searched by PromptLibrary. |
| `prompt` | Yes | Text copied when the prompt is selected. |
| `tags` | No | Array of IDs from `user.prompt.tag.json`. Omit it or use `[]` for no tags. |
| `id` | No | Stable identifier useful when sharing or synchronizing the library with other tools. PromptLibrary does not require it. |
| `disabled` | No | Availability metadata understood by some prompt managers. PromptLibrary accepts all prompts regardless of this value. |

Prompt IDs have no required format or length. If another tool does not require
them, you may omit them.

## Tags

The entire `user.prompt.tag.json` file is optional. When tags are used, each tag
needs a unique `id` and a display `name`:

```json
[
  {
    "id": "writing",
    "name": "Writing",
    "description": "Writing, rewriting, and editing prompts"
  },
  {
    "id": "research",
    "name": "Research"
  }
]
```

Tag fields:

| Field | Required | Purpose |
| --- | --- | --- |
| `id` | Yes | Unique key referenced by prompts. It is matched case-insensitively. |
| `name` | Yes | Tag name shown and searched by PromptLibrary. |
| `description` | No | Additional searchable context for the tag. |
| `color` | No | Visual metadata for compatible tools or future colored-tag interfaces. PromptLibrary currently does not display it. |

Tag IDs do not need to be long generated values. Short, readable IDs such as
`writing`, `coding`, or `meeting-notes` are recommended. Keep each ID unique and
use the same value in the prompt file.

## Assigning One Or More Tags

Add the tag IDs to a prompt's `tags` array:

```json
[
  {
    "act": "Compare Sources",
    "prompt": "Compare the following sources and identify agreements, conflicts, and evidence gaps.",
    "tags": [
      "research",
      "writing"
    ]
  }
]
```

To create a new tag:

1. Add it to `user.prompt.tag.json` with a unique `id` and `name`.
2. Add that ID to any prompt's `tags` array.
3. Save both files. With auto-reload enabled, PromptLibrary reloads the library
   shortly after the save.

An unknown tag ID does not prevent a prompt from loading, but that tag cannot be
shown or searched until a matching tag record exists.

## JSON Editing Rules

- Keep the outer square brackets because each file is an array.
- Separate records and array values with commas.
- Escape quotation marks inside text as `\"`.
- Represent a line break inside a JSON string as `\n`.
- Represent a literal backslash as `\\`.
- Save the file as UTF-8.
- Standard JSON is recommended for portability. PromptLibrary also accepts
  comments and trailing commas.
- PromptLibrary ignores unknown fields, allowing richer libraries to remain
  compatible.
- Invalid individual records are skipped while valid records continue loading.
  If the overall JSON document is invalid, PromptLibrary keeps the last valid
  in-memory library until the file is corrected and saved again.

## Where To Store Personal Libraries

The packaged `Data` folder is convenient for trying PromptLibrary, but replacing
the plugin during an update can replace that folder. For a personal library, copy
both JSON files to a separate folder and select it using **Custom data folder
path** in the PromptLibrary settings.

Type `/p +` with the default settings to open the active prompt file, tag file, or
data folder. If you customize `/p` or the library files command, use the configured
values instead.

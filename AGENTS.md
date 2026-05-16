# Repository Instructions

## Environment-Specific Instructions

Tool availability, shell choice, and preferred search commands are environment-specific.

Follow any global, parent, or session instructions for local tool availability. Keep this repository file focused on repository structure and project-specific workflow.

## Initial Discovery Scope

Do not begin a session by recursively scanning the whole workspace.

Use the `README.md` "Directory Structure" section as the baseline map of the repository before doing filesystem discovery. Start with that map and a root directory listing, then inspect only the narrow paths relevant to the task.

For code and tests, prefer targeted discovery under the documented source, test, and helper paths.

Use `.gitignore` as the baseline for generated and local environment paths to avoid during discovery. Avoid scanning those folders unless the task specifically concerns them.

If a broad search becomes necessary, scope it explicitly with exclusions based on `.gitignore` and the generated/local environment folders above.

## GitHub Issue Workflow

When the developer asks to create a GitHub issue, first write a Markdown issue draft under `Artifacts/github-issues`.

The draft should describe the requested feature, bug, chore, or documentation task clearly enough for review. After creating the draft, notify the developer that it is available for review and wait for explicit acceptance before uploading anything to GitHub.

After the developer accepts the issue description, create the issue by calling `Scripts/New-GitHubIssue.ps1` with the issue title and draft file path. Pass labels or assignees only when the developer requested them.

The GitHub token is expected to be configured by the developer for the script. Do not perform token setup or extra GitHub tasks. After the script completes, return the issue link printed by the script.

## Definition of Done

Before considering a change complete, verify the items that apply:

- Documentation is still accurate after every code or project-structure modification.
- The root `README.md` is updated when files or folders are added, removed, renamed, or repurposed, including its "Directory Structure" section.
- Public API additions or behavior changes include XML documentation updates on the affected public API, with useful examples and current `<exception>` entries for every exception the API can throw directly.
- Public API additions or behavior changes update only the affected package docs:
  - `Src/NanoRoute*/Doc/index.md`
  - `Src/NanoRoute*/HISTORY.md`
  - `Src/NanoRoute*/README.md`
- Code changes have relevant tests run, and coverage is not decreased. Use `Scripts/Run-Tests.ps1` for the normal test and coverage measurement flow.
- Native AOT smoke coverage in `Tests/NanoRoute.NativeAot` is updated when behavior changes should be covered there.
- When a change may affect Native AOT compatibility, publish and run the Native AOT smoke project as the PR workflow does. This includes changes involving reflection, JSON serialization, generated metadata, trimming-sensitive code, or public APIs that should remain usable from Native AOT applications.

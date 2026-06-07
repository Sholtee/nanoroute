# Repository Instructions

## Environment and Discovery

Tool availability, shell choice, and preferred search commands are environment-specific. Follow any global, parent, or session instructions for local tool availability.

Do not begin by recursively scanning the whole workspace. Use the root `README.md` "Directory Structure" section and a root directory listing as the repository map, then inspect only paths relevant to the task.

Use `.gitignore` as the baseline for generated and local environment paths to avoid. If a broad search is necessary, scope it explicitly and exclude generated, build, coverage, benchmark-output, IDE, and local environment paths unless directly relevant.

For code and tests, prefer targeted discovery under the documented source, test, and helper paths. Prefer concise command output: use narrow paths, exact patterns, and output limits instead of dumping full files or full recursive search results.

## Serena Workflow

When Serena MCP tools are available for a code-related task, activate the repository root as the Serena project and read Serena's initial instructions before inspecting source symbols.

Use Serena for focused code navigation, symbol overviews, symbol reads, reference lookup, bug diagnosis, and symbol edits when it can answer cleanly. Use shell reads such as `Get-Content` only for narrow patch context, exact diffs, non-code files, or command-oriented verification.

Serena is not required for simple documentation-only edits, direct command-output requests, or tasks clearly confined to non-code files. If Serena is unavailable during a code-related task, continue with the approved environment-specific alternatives and mention the fallback.

## Token and Context Discipline

Keep repository context small and task-focused.

Before inspecting files for a code-related task, state the smallest useful discovery plan: the symbols, files, or directories expected to matter, and the cheapest way to inspect them. Prefer Serena symbol overview, symbol lookup, and reference lookup before opening source files directly.

Do not re-read files that have already been summarized unless the previous summary is insufficient for the next edit or verification step. Maintain a brief working context when useful:

- Relevant files and symbols
- Decisions already made
- Commands already run and important results
- Remaining unknowns

Default inspection budget for a single task:

- Up to 5 source files
- Up to 3 test files
- Up to 2 project/configuration/documentation files

If more context appears necessary, pause and explain why before expanding the search, unless the developer explicitly asked for broad investigation.

Avoid large command outputs. Use focused commands, filters, and output limits. Prefer targeted tests over full test suites until the change is ready for final verification. When command output is long, keep only the relevant error, failure, summary, or final 100-150 lines.

Do not inspect generated, build, coverage, benchmark-output, IDE, or local environment files unless they are directly relevant to the task. Avoid paths such as `bin/`, `obj/`, `TestResults/`, `BenchmarkDotNet.Artifacts/`, `.vs/`, `.idea/`, `.vscode/`, and generated coverage reports. Use `.gitignore` and the repository README directory structure as the first guide for paths to avoid.

When preparing a handoff, context compaction preview, or summary, keep it compact and operational: current goal, changed/inspected files, decisions, commands/results, blockers, and next steps.

## Serena Process Cleanup

When Serena MCP tools are invoked, record the `serena.exe` launcher process ID for the session when practical.

Before finishing, after no further Serena tool calls are needed, terminate and verify only Serena process trees started for the session, including the associated `serena.exe` launcher and Python descendants. A model turn ending is not enough evidence that the Codex conversation/session is concluded; keep the active Serena MCP transport running across turns by default. Terminate it only when the developer explicitly asks to end the conversation, asks for Serena cleanup, or confirms that no more code-related work will continue in the current thread. Do not terminate unrelated or ambiguous Python processes; inspect process ID, command line, executable path, parent process ID, and start time when ownership needs confirmation. Leave pre-existing or ambiguous Serena process trees running and ask before terminating them.

## GitHub Issue Workflow

When asked to create a GitHub issue, first write a Markdown draft under `Artifacts/github-issues` and make it clear enough for review.

Notify the developer that the draft is ready and wait for explicit acceptance before uploading anything to GitHub.

After acceptance, create the issue with `Scripts/New-GitHubIssue.ps1`, passing the issue title and draft file path. Pass labels or assignees only when requested. Do not perform token setup or extra GitHub tasks; return the issue link printed by the script.

## Build, Test, and .NET Process Cleanup

Before running any build, test, or performance workflow, capture the existing `dotnet` process list with process ID, parent process ID, command line, executable path, and start time.

When running repository PowerShell scripts for build, test, or performance workflows, launch the script in a separate process. Record the script process ID and any related child `dotnet` process IDs you can identify.

Watch the process output. If the console output is not updated for 90 seconds (`90000` milliseconds), treat the script as frozen.

When a watched script appears frozen, inspect the process tree, terminate only the session-owned script process and descendants, verify termination, and report the command plus terminated process IDs before retrying or stopping. Ask before terminating any unrelated or ambiguous `dotnet` process.

## Performance Testing Workflow

When asked to performance test, benchmark, compare throughput, or investigate allocations, use the BenchmarkDotNet harness in `Tests/NanoRoute.Perf`.

Place new performance tests in `Tests/NanoRoute.Perf`. Follow the existing benchmark style: keep each benchmark class focused on the behavior being measured, name benchmark classes with the `Benchmarks` suffix, and include baseline/comparison cases when investigating a regression or alternative implementation.

Run performance tests with `Scripts/Run-PerfTests.ps1`. Pass a BenchmarkDotNet filter as the first positional argument when a focused run is enough, for example `.\Scripts\Run-PerfTests.ps1 "*Routing*"`.

The performance script builds `Tests/NanoRoute.Perf/NanoRoute.Perf.csproj` in Release, clears generated `Artifacts` and `BIN` output, runs the generated `NanoRoute.Perf.exe`, and writes BenchmarkDotNet output under `Artifacts/BenchmarkDotNet`.

For performance-sensitive code changes, add or update focused benchmarks and run the narrowest useful filter. Report the filter used, the benchmark summary location, and the important timing/allocation results.

## Context Compaction Preview

When the conversation approaches context compaction, or when asked for a context compaction or compactation preview, provide a brief checkpoint-style summary.

Include the current goal, newest request, active repository instructions and constraints, files inspected or changed, decisions made, commands or tests run with important results, blockers, assumptions, and next steps.

Do not claim to know exactly what would be discarded. For discarded context, describe only broad categories likely safe to omit, such as repeated logs, long command output, dead-end exploration, stale discussion, or details that no longer affect the next action.

## Definition of Done

Before considering a change complete, verify the items that apply:

- Documentation is still accurate after every code or project-structure modification.
- The root `README.md` is updated when files or folders are added, removed, renamed, or repurposed, including its "Directory Structure" section.
- Public API additions or behavior changes include XML documentation updates on the affected public API, with useful examples and current `<exception>` entries for every exception the API can throw directly.
- Public API additions or behavior changes update only the affected package docs:
  - `Src/NanoRoute*/Doc/index.md`
  - `Src/NanoRoute*/HISTORY.md`
  - `Src/NanoRoute*/README.md`
- After code modifications are otherwise complete, run `dotnet format`.
- Code changes have relevant tests run. Use the narrowest useful test command during development. Before final completion, use `Scripts/Run-Tests.ps1` for the normal test and coverage measurement flow when the change is broad enough to affect package behavior, public API behavior, routing behavior, Native AOT behavior, or coverage expectations.
- Native AOT smoke coverage in `Tests/NanoRoute.NativeAot` is updated when behavior changes should be covered there.
- When a change may affect Native AOT compatibility, publish and run the Native AOT smoke project as the PR workflow does. This includes changes involving reflection, JSON serialization, generated metadata, trimming-sensitive code, or public APIs that should remain usable from Native AOT applications.

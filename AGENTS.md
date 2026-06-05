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

## Serena Workflow

When Serena MCP tools are available, use Serena at the start of any session that may require code navigation, code understanding, or code edits. Activate the repository root as the Serena project and read Serena's initial instructions before inspecting source symbols.

When Serena is installed and available, keep using it whenever it makes sense during code-related work, including follow-up investigations, bug diagnosis, symbol inspection, reference lookup, and focused symbol edits. Do not fall back to shell file reads for source-code understanding merely out of habit when Serena can answer the question cleanly.

Prefer Serena's symbol overview, symbol lookup, and reference-finding tools for targeted code exploration. Use them together with the `README.md` directory map so discovery stays focused on the source, test, or helper paths relevant to the task.

Serena is not required for simple documentation-only edits, direct command-output requests, or tasks that are clearly confined to non-code files. If Serena is unavailable during a code-related task, continue with the approved environment-specific alternatives and mention that fallback.

## Context Compaction Preview

When the conversation is approaching context compaction, or when the developer asks for a context compaction or compactation preview, provide a brief checkpoint-style summary of what should be preserved if the conversation is compacted at that moment.

Include the current goal, newest user request, active repository instructions and constraints, files inspected or changed, decisions made, commands or tests run with important results, blockers, assumptions, and next steps.

Do not claim to know the exact content that would be discarded. For discarded context, describe only broad categories that are likely safe to omit, such as repeated logs, long command output, dead-end exploration, stale discussion, or details that no longer affect the next action.

## GitHub Issue Workflow

When the developer asks to create a GitHub issue, first write a Markdown issue draft under `Artifacts/github-issues`.

The draft should describe the requested feature, bug, chore, or documentation task clearly enough for review. After creating the draft, notify the developer that it is available for review and wait for explicit acceptance before uploading anything to GitHub.

After the developer accepts the issue description, create the issue by calling `Scripts/New-GitHubIssue.ps1` with the issue title and draft file path. Pass labels or assignees only when the developer requested them.

The GitHub token is expected to be configured by the developer for the script. Do not perform token setup or extra GitHub tasks. After the script completes, return the issue link printed by the script.

## Performance Testing Workflow

When the developer asks to performance test, benchmark, compare throughput, or investigate allocations, use the BenchmarkDotNet harness in `Tests/NanoRoute.Perf`.

Place new performance tests in the `Tests/NanoRoute.Perf` project. Follow the existing benchmark style: keep each benchmark class focused on the behavior being measured, name benchmark classes with the `Benchmarks` suffix, and include baseline/comparison cases when the request is about a regression or alternative implementation.

Run performance tests with `Scripts/Run-PerfTests.ps1`. Pass a BenchmarkDotNet filter as the first positional argument when a focused run is enough, for example `.\Scripts\Run-PerfTests.ps1 "*Routing*"`.

The performance script builds `Tests/NanoRoute.Perf/NanoRoute.Perf.csproj` in Release, clears generated `Artifacts` and `BIN` output, runs the generated `NanoRoute.Perf.exe`, and writes BenchmarkDotNet output under `Artifacts/BenchmarkDotNet`.

For performance-sensitive code changes, add or update focused benchmarks and run `Scripts/Run-PerfTests.ps1` with the narrowest useful filter. Report the filter used, the benchmark summary location, and the important timing/allocation results.

## Serena Process Cleanup

When Serena MCP tools are invoked during a session, record the process ID of any `serena.exe` launcher started for the session when practical.

Before finishing the session, after no further Serena tool calls are needed, terminate and verify the termination of Serena process trees started for the session. Include the associated `serena.exe` launcher and its Python descendants.

Do not terminate unrelated Python processes. When Serena ownership needs confirmation, inspect the process ID, command line, executable path, parent process ID, and start time. Leave pre-existing or ambiguous Serena process trees running and ask the developer before terminating them.

## .NET Process Cleanup

Before running any build, test, or performance workflow, capture the existing `dotnet` process list with process ID, parent process ID, command line, executable path, and start time.

Never rely solely on a long timeout applied to an entire test or performance script. Enforce separate watchdog timeouts for setup and build-related commands.

Treat all of the following as build-related phases with a hard timeout of 90 seconds (`90000` milliseconds) each:

- `dotnet tool restore`
- `dotnet build-server shutdown`
- `dotnet build`
- Restore or build work started indirectly by `dotnet test`
- Restore or build work started indirectly by repository scripts

If a repository script does not expose these phases separately or enforce its own watchdogs, do not run it as one opaque long-running command. Run equivalent phases separately, or add watchdog handling to the script before continuing.

The 90-second timeout does not apply after test fixture execution or BenchmarkDotNet benchmark execution has clearly started. Those phases may use a longer task-appropriate timeout.

When any watched phase reaches its timeout:

1. Inspect the process tree.
2. Terminate only session-owned processes, including descendants.
3. Verify that they terminated.
4. Report the timed-out command and terminated process IDs before retrying or stopping.

Do not terminate unrelated or ambiguous `dotnet` processes. Ask the developer before terminating a process when ownership cannot be established.

## Definition of Done

Before considering a change complete, verify the items that apply:

- Documentation is still accurate after every code or project-structure modification.
- The root `README.md` is updated when files or folders are added, removed, renamed, or repurposed, including its "Directory Structure" section.
- Public API additions or behavior changes include XML documentation updates on the affected public API, with useful examples and current `<exception>` entries for every exception the API can throw directly.
- Public API additions or behavior changes update only the affected package docs:
  - `Src/NanoRoute*/Doc/index.md`
  - `Src/NanoRoute*/HISTORY.md`
  - `Src/NanoRoute*/README.md`
- After modifications are otherwise complete, run `dotnet format` to keep code style consistent.
- Code changes have relevant tests run, and coverage is not decreased. Use `Scripts/Run-Tests.ps1` for the normal test and coverage measurement flow.
- Native AOT smoke coverage in `Tests/NanoRoute.NativeAot` is updated when behavior changes should be covered there.
- When a change may affect Native AOT compatibility, publish and run the Native AOT smoke project as the PR workflow does. This includes changes involving reflection, JSON serialization, generated metadata, trimming-sensitive code, or public APIs that should remain usable from Native AOT applications.

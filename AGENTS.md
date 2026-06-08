# Project Rules for AI Agents

## File Encoding

All file read/write operations **must** use UTF-8 encoding.

- For `.cs` files in a Unity project: use **UTF-8 with BOM** (`-Encoding UTF8` in PowerShell).
- For `.md`, `.json`, `.xml`, `.yml`, `.shader`, `.asset`, `.meta` files: use **UTF-8 without BOM** unless the file already has a BOM.
- When reading files with `Get-Content`, always specify `-Encoding UTF8`.
- When writing files with `Set-Content`, always specify `-Encoding UTF8` (PowerShell 7) or `-Encoding UTF8BOM` (PowerShell 7) for `.cs` files.

**Rationale**: Unity's C# compiler and the editor expect `.cs` files to be UTF-8 with BOM. Using the wrong encoding corrupts Chinese characters and other non-ASCII text in comments and string literals, producing garbled text that is irreversible.

## General

- Default to ASCII when editing or creating files. Only introduce non-ASCII or Unicode characters when there is a clear reason and the file already uses that character set.

## AI Documentation

All AI-generated documents are stored under `Document/AIDoc/`:

| Subdirectory  | Purpose                                                |
|---------------|--------------------------------------------------------|
| `Analysis/`   | Code analysis, dependency graphs, performance heatmaps |
| `Tasks/`      | Cross-session task state and progress                  |
| `Snapshots/`  | Project snapshots for quick context recovery           |
| `Bugs/`       | Bug records (delete on fix)                            |
| `Plan/`       | Implementation plans (save before execution, archive when done) |

## Memory System

Persistent memory is stored at `C:\Users\TouMing\.claude\projects\d--UnityProject-TGame\memory\`.

Each memory is a single Markdown file with YAML frontmatter (`name`, `description`, `metadata`). Types: `user`, `feedback`, `project`, `reference`. Use `[[link-name]]` cross-reference syntax within memory files.

The index (`MEMORY.md`) is loaded every session — never put full memory content there, only one-line pointers.

## Claude Code Configuration

- **Project config**: `.claude/settings.json` (permissions, hooks)
- **Local overrides**: `.claude/settings.local.json` (machine-specific)
- **Keybindings**: `~/.claude/keybindings.json`
- **Permissions**: Bash commands (`Bash: "allow"`) are pre-approved. File-system and other shell commands are individually allowed via `.claude/settings.local.json` allowlist.

## Skills Reference

Available slash-command skills:

- **code-review** — Review diff for correctness bugs and cleanup opportunities
- **simplify** — Apply reuse/simplification/efficiency cleanups
- **verify** — Run the app to verify a change works
- **run** — Launch the project app
- **deep-research** — Multi-source web research with fact-checking
- **xlsx** — Spreadsheet creation, editing, and analysis
- **codeagent** — Multi-backend AI code task execution
- **harness** — Long-running multi-session autonomous agent tasks
- **browser** — Chrome DevTools Protocol automation
- **loop** — Recurring command execution on an interval
- **init** — Initialize a new CLAUDE.md
- **review** — Review a pull request
- **update-config** — Manage Claude Code settings
- **security-review** — Security review of pending changes

## Agent Workflow Conventions

1. Use dedicated tools (Read, Write, Edit, Glob, Grep) before falling back to Bash
2. Prefer `pipeline()` over `parallel()` for multi-stage agent orchestration (see Workflow tool docs)
3. Only use barrier-style `parallel()` when cross-item context is genuinely needed
4. Scale exploration depth to task scope — use `budget.remaining()` for token-aware loops
5. Document non-obvious design decisions in memory, not in code comments

## Tool Precedence

When searching for code, the following priority should be used:
1. `Grep` — fastest for content search
2. `Glob` — fastest for file name patterns
3. `Read` — read specific files
4. `Bash` — only when dedicated tools don't suffice (e.g., `git log`, `dotnet` commands)

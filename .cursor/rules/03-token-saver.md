# Token Saver Rule (Must Follow)

## Goal
Reduce token usage without losing effectiveness by keeping work narrowly scoped and avoiding repo-wide scanning.

## Hard constraints
- Work on exactly ONE GitHub issue at a time (the one in Status = In Progress).
- Do NOT re-read or re-summarize all docs unless explicitly needed for this ticket.
- Do NOT restate full project context in responses.

## Repo scanning / file access policy
- Do NOT scan the whole repository.
- Only open files you are likely to edit or that are directly required to understand the current bug.
- If you need to locate a symbol, use targeted search (file name / class name) and open at most:
  - 3 source files for reading
  - 3 additional files for editing
  (6 total unless the user explicitly approves expanding scope)

## Output brevity
Every response must be concise and follow this template:
1) Plan (max 4 bullets)
2) Changes (max 8 bullets)
3) Files edited (list)
4) How to test (max 6 steps)
5) Risks/notes (max 4 bullets)

Do not include long explanations, large code dumps, or repeated requirements.

## Implementation style
- Prefer small diffs over refactors.
- Avoid creating new systems unless strictly necessary for the ticket.
- If a change might have cascading effects, stop and ask before proceeding.

## Model usage guidance
- Prefer non-thinking model for UI wiring and small logic changes.
- Use thinking mode only for complex simulation/AI issues, and keep prompts short.

## Definition of Done reminder
- Must compile
- Must run in Godot
- Must satisfy issue acceptance criteria
- Must provide a Review Packet comment on the issue

## File budget (hard)
- Before editing, list the exact files you plan to open/edit (max 6).
- Do not open more files unless the user approves.

## HARD FILE BUDGET (must follow)
- Before opening files, list the exact files you will open/edit.
- Limit: max 3 files to read + max 3 files to edit.
- If you need more, STOP and ask for approval before opening additional files.

## NO DIFF / NO TOOL CHATTER
- Do not print diffs, patch snippets, or command outputs in chat.
- Only report: files edited + what changed at a high level.
- Only print code if the user explicitly asks.

## REVIEW PACKET COMPRESSION
- In chat: provide a short Review Packet (max 8 bullets total).
- Post the full detailed Review Packet only on GitHub.

## NO GITHUB API AUTOMATION
- Do NOT call GitHub APIs (no curl, no listing issues, no auto-commenting, no auto-labeling).
- Provide the Review Packet in chat ONLY (short).
- The user will paste it into GitHub and set labels manually.
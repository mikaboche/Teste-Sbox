<!-- sbox-pro-rules:begin -->
## sbox-pro skill — hard rule (auto-managed by S&Box Pro v1.1.0)

This block is managed by the S&Box Pro MCP installer. Edits inside the markers will be overwritten on the next install. Add your own rules **outside** the markers.

**Before writing any s&box C# / razor / scss, OR invoking any `mcp__sbox-pro__*` tool that mutates code/scenes/components/properties, you MUST:**

1. Read the relevant file under `~/.claude/skills/sbox-pro/references/` first — `core-concepts.md` for components, `ui-razor.md` for UI, `networking.md` for `[Sync]`/RPCs, `input-and-physics.md` for input/raycasts, `api-schema-core.md` for type signatures, `patterns-and-examples.md` for full worked examples.
2. Verify every type, attribute, and method name you plan to use exists in the schema. If it isn't there, do NOT write it — re-check the design.
3. When applying APIs you've never used before in this project, prefer `describe_type` / `docs_get_api_type` to confirm signatures live.

**Why this rule exists:** without consulting the skill first, tool calls and generated code drift toward Unity / older s&box snapshots and produce SB2000 compile errors, broken prefab refs, missing input actions, and engine warnings. Every time the rule was bypassed during development, a new ISSUE landed in `Docs/ISSUES.md` of the sbox-pro repo. Consulting the skill first prevents the entire class of bugs.

**Do not bypass this rule for "simple" tasks.** Muscle memory from Unity is exactly when this drift happens.
<!-- sbox-pro-rules:end -->

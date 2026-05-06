# S&Box Pro MCP Reference

When the S&Box Pro MCP server is connected (`mcp__sbox-pro__*` tools available),
prefer using the live tools over editing files manually for scene/prefab/asset
manipulation. The server runs in-process inside the S&Box editor, so changes
land immediately without restart.

Server endpoint: `http://localhost:8099` (SSE on `/sse`, REST on `/health`,
`/tools`, `/logs`).

## Tool catalog (193 total)

Categories — call `mcp__sbox-pro__list_tools filter=<keyword>` for live names.

### Game starters (composers — one call → full starter)
- `start_multiplayer_fps_project` — PlayerController + NetworkedPlayer + Weapons (Pistol+Knife) + scene + wired prefab
- `start_parkour_game` — same + RagdollDriver. Requires `fish.shrimple_ragdolls`.
- `start_survival_game` — same + Tetris Inventory (UI + hotbar + drag-drop) + Grab + Interact. Requires `conna.inventory`.

### Code piece templates (16)
Player + state: `template_player_controller` (FPS/TPS, sprint, crouch, Shrimple swap section), `template_networked_player`, `template_shrimple_player`, `template_shrimple_pawn`.
Combat: `template_weapon` (abstract base + Pistol + Knife examples — ranged hitscan + melee sweep).
Inventory: `template_inventory` (generates 4 files: backend + Razor UI + hotbar + scss).
Interaction: `template_grab_component`, `template_interact_component`, `template_zoom_component`, `template_trigger_zone`.
Networking primitives: `template_net_cooldown`, `template_net_visibility`.
Cosmetic + AI: `template_dresser`, `template_shrimple_ragdoll`, `template_astar_npc`.

### Scene templates
`template_empty_scene`, `template_multiplayer_basic`.

### Live editor categories
Scene/GameObject/Component CRUD, Asset browsing + creation, Physics, Lighting, Audio, Effects, Mesh editing, Navigation, Networking primitives, Editor/Play mode, Console, Project config, Validation (`validate_scene`, `find_missing_references`).

### Docs (offline-cached wiki + API schema)
`docs_search`, `docs_get_page`, `docs_list_categories`, `docs_search_api`, `docs_get_api_type`, `docs_cache_status`, `docs_refresh_index`, `docs_run_tests`. First call kicks off background indexing (~30s wiki, ~60s API).

## Workflow tips

**"Quero começar um tipo de jogo"** → composer first. After it generates, the scene has a Player Template GameObject with all components wired and `NetworkHelper.PlayerPrefab` already pointing at it. Hit Play directly — no manual prefab building needed.

**"Quero adicionar uma peça num projeto existente"** → individual `template_*` tool, then run `trigger_hotload` on the resulting `.cs` file.

**"Quero entender a cena"** → `get_scene_summary` (aggregate) → `find_game_objects filter=<tag/component>` → `get_game_object_details`.

**"Quero verificar refs quebradas"** → `validate_scene` for full report; `find_missing_references` for actionable subset.

**"Quero modificar muitos GameObjects"** → `batch_transform`, `bulk_set_property`. Avoid manual loops.

**"API question"** → `docs_search_api` first (canonical schema dump, ~1800 types). Fallback to `describe_type` (live TypeLibrary reflection) for project-local types.

## Templates that pair with installed libraries

Some templates reference upstream libraries the user must install separately:

| Template | Library | Repo |
|---|---|---|
| `template_inventory` | `conna.inventory` | kurozael/sbox-inventory |
| `template_shrimple_ragdoll` | `fish.shrimple_ragdolls` | Small-Fish-Dev/Shrimple-Ragdolls |
| `template_shrimple_player` | `fish.scc` | Small-Fish-Dev/shrimple_character_controller |
| `template_shrimple_pawn` | `fish.sp` | Small-Fish-Dev/shrimple-pawns |
| `template_astar_npc` | `fish.grid_and_astar` | Small-Fish-Dev/Grid-and-Astar-NPC |

If a template's `using` directive doesn't resolve, the library isn't installed. Tell the user to install it via Asset Browser first.

## Conventions specific to this MCP

- **GUIDs over names**: any tool taking `name` or `guid` — prefer GUID (rename-safe).
- **Don't auto-call play mode**: `start_play`/`stop_play` mutate scene state; only call when user asks.
- **`set_property` family**: use `set_property` for simple values, `set_resource_property` for Model/Material/Sound refs by path, `set_list_property` for `List<T>` props (accepts JSON arrays).
- **Validate before assuming an API exists**: when user asks for a non-trivial API not in your immediate knowledge, run `docs_search_api` or `describe_type` first. The schema dump is the source of truth.

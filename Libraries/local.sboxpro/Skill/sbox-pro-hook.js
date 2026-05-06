#!/usr/bin/env node
// sbox-pro PreToolUse hook
//
// Fires before any `mcp__sbox-pro__*` tool call. Maps the tool name to the
// most relevant reference file under ~/.claude/skills/sbox-pro/references/
// and injects a system reminder via `additionalContext` pointing Claude at
// the file before the call proceeds.
//
// Never blocks a tool — `permissionDecision: "allow"` is hard-coded. The
// hook only educates; the model still has to actually open the file.
//
// Self-contained, no npm deps. Claude Code ships Node so this runs anywhere.

const fs = require( 'fs' );
const os = require( 'os' );
const path = require( 'path' );

const REF_ROOT = path.join( os.homedir(), '.claude', 'skills', 'sbox-pro', 'references' );

const MAPPING = [
	{
		pattern: /^mcp__sbox-pro__(add_component|set_property|set_resource_property|set_list_property|copy_component|create_game_object|create_script|destroy_game_object|reparent_game_object|set_game_object_(name|tags|enabled|transform)|set_component_(enabled|property)|bulk_set_property|get_component_properties|get_all_properties|describe_type|search_types|list_available_components)\b/,
		ref: 'core-concepts.md',
		topic: 'Component lifecycle (OnAwake/OnStart/OnUpdate/OnFixedUpdate), [Property] / [Sync], scene attachment via Scene.CreateObject, Components.Get<T>',
	},
	{
		pattern: /^mcp__sbox-pro__(create_razor_ui|add_screen_panel|add_world_panel)\b/,
		ref: 'ui-razor.md',
		topic: 'PanelComponent, flexbox layout (NO CSS Grid), BuildHash, paired .razor.scss, [InputAction] for action dropdowns',
	},
	{
		pattern: /^mcp__sbox-pro__(add_collider|add_hull_collider|add_plane_collider|configure_collider|add_physics|add_rigidbody|create_character_controller|add_joint|create_joint|raycast|terrain_raycast|create_model_physics)\b/,
		ref: 'input-and-physics.md',
		topic: 'Collider/Rigidbody, Scene.Trace builder pattern, ICollisionListener.OnCollisionStart(collision), Component.ITriggerListener',
	},
	{
		pattern: /^mcp__sbox-pro__(set_network_data|set_network_flags|add_network_helper|configure_network_helper|create_lobby|set_ownership|network_spawn|network_refresh|disconnect_network|get_network_status)\b/,
		ref: 'networking.md',
		topic: '[Sync] / [Sync(SyncFlags.X)] / [Change], [Rpc.Broadcast/Host/Owner], NetworkMode, IsProxy guard, INetworkListener',
	},
	{
		pattern: /^mcp__sbox-pro__(template_|start_(multiplayer_fps_project|parkour_game|survival_game))/,
		ref: 'patterns-and-examples.md',
		topic: 'Worked examples for player controllers, weapons, HUDs, networked players, NPCs — verify generated code against current API',
	},
	{
		pattern: /^mcp__sbox-pro__(create_camera|configure_camera|create_environment_light|create_light|configure_light|create_ambient_light|configure_post_processing|create_sky_box|set_sky_box|create_fog_volume|create_indirect_light_volume|create_dsp_volume|configure_terrain|create_terrain|get_terrain_info|create_render_entity|configure_render_entity|create_particle_effect|configure_particle_effect|create_beam_effect|configure_beam_effect|create_verlet_rope|create_audio_listener|create_sound_box|create_sound_point|configure_sound|create_soundscape_trigger|play_sound_preview|assign_material|assign_model|assign_sound|create_material|set_material_property|get_material_properties)\b/,
		ref: 'components-builtin.md',
		topic: 'Built-in Sandbox components — CameraComponent, SkinnedModelRenderer, lights, particles, audio, terrain. Verify property names exist before set_property.',
	},
];

let buf = '';
process.stdin.on( 'data', d => { buf += d; } );
process.stdin.on( 'end', () => {
	let toolName = '';
	try {
		const input = JSON.parse( buf || '{}' );
		toolName = input.tool_name || '';
	} catch {
		// Bad input — allow silently
	}

	const match = MAPPING.find( m => m.pattern.test( toolName ) );

	if ( !match ) {
		// No specific reference for this tool — let it through without extra context
		process.stdout.write( JSON.stringify( {
			hookSpecificOutput: { hookEventName: 'PreToolUse', permissionDecision: 'allow' },
		} ) );
		return;
	}

	const refFile = path.join( REF_ROOT, match.ref );
	const fileExists = fs.existsSync( refFile );

	const reminder = [
		`[sbox-pro] About to call \`${toolName}\`.`,
		fileExists
			? `BEFORE issuing this call, READ \`${refFile}\` — covers: ${match.topic}.`
			: `Reference file missing at \`${refFile}\` — install/update the sbox-pro skill via the dock's Settings tab.`,
		`Verify every type, property, attribute, and method name you intend to pass exists in the bundled schema. Do not guess; the schema is ground truth.`,
	].join( ' ' );

	process.stdout.write( JSON.stringify( {
		hookSpecificOutput: {
			hookEventName: 'PreToolUse',
			permissionDecision: 'allow',
			additionalContext: reminder,
		},
	} ) );
} );

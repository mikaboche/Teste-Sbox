using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Sandbox;

namespace SboxPro;

[Dock( "Editor", "S&Box Pro", "construction" )]
public class SboxProDock : Widget
{
	// Single restrained palette — flat, minimal, identity built on
	// typography and spacing rather than colored boxes.
	private const string TextPrimary   = "#e5e7eb";
	private const string TextSecondary = "#9ca3af";
	private const string TextDim       = "#6b7280";
	private const string Accent        = "#60a5fa";
	private const string AccentDim     = "#3b82f6";
	private const string Mono          = "#7dd3fc";
	private const string Success       = "#4ade80";
	private const string Warning       = "#fbbf24";
	private const string Error         = "#f87171";

	// Common style fragments — applied to every Label so the editor's
	// default widget chrome (border/background) never leaks through.
	private const string LblBase = "background-color: transparent; border: none;";

	private TabWidget _tabs;
	private ScrollArea _logScroll;
	private LineEdit _toolsFilter;
	private Widget _toolsList;

	public SboxProDock( Widget parent ) : base( parent )
	{
		WindowTitle = "S&Box Pro";
		MinimumSize = new Vector2( 520, 720 );
		BuildUI();
	}

	private void BuildUI()
	{
		// Belt-and-suspenders against hotload zombie subscribers: SboxProInit also calls
		// these clears, but we don't know the ordering between [EditorEvent.Hotload] and
		// Widget reconstruction. Clearing here, immediately before BuildHeader/BuildTabs
		// re-attach their handlers, makes the dock self-healing regardless of which ran
		// first. The events are private to our assembly and only this dock subscribes,
		// so wiping is safe.
		SboxProLog.ClearSubscribers();
		SboxProServer.ClearSubscribers();

		var root = Layout.Column();
		root.Margin = 0;
		root.Spacing = 0;

		root.Add( BuildHeader() );
		root.Add( BuildTabs(), 1 );
		root.Add( BuildFooter() );

		Layout = root;
	}

	private Widget BuildFooter()
	{
		var bar = new Widget();
		bar.Layout = Layout.Row();
		bar.Layout.Margin = new Sandbox.UI.Margin( 14, 6, 14, 6 );
		bar.Layout.Spacing = 0;

		bar.Layout.AddStretchCell();
		bar.Layout.Add( MakeLabel( "powered by KiKoZl", $"{LblBase} font-size: 10px; color: {TextDim};" ) );

		return bar;
	}

	// ──────────────────────────────────────────────
	//  Header
	// ──────────────────────────────────────────────

	private Widget BuildHeader()
	{
		var bar = new Widget();
		bar.Layout = Layout.Row();
		bar.Layout.Margin = new Sandbox.UI.Margin( 14, 12, 14, 12 );
		bar.Layout.Spacing = 10;

		var brand = MakeLabel( "S&Box Pro", $"font-size: 15px; font-weight: 700; color: {TextPrimary}; letter-spacing: 0.3px;" );
		bar.Layout.Add( brand );

		var version = MakeLabel( $"v{SboxProServer.Version}", $"font-size: 10px; color: {TextDim}; margin-top: 4px;" );
		bar.Layout.Add( version );

		bar.Layout.AddStretchCell();

		var pill = new Label( "" );
		void UpdateStatus()
		{
			if ( SboxProServer.IsRunning )
			{
				pill.Text = $"●  online · :{SboxProServer.Port} · {ToolRegistry.Count} tools";
				pill.SetStyles( $"{LblBase} font-size: 11px; color: {Success};" );
			}
			else
			{
				pill.Text = "●  offline";
				pill.SetStyles( $"{LblBase} font-size: 11px; color: {Error};" );
			}
		}
		SboxProServer.OnStateChanged += UpdateStatus;
		UpdateStatus();
		bar.Layout.Add( pill );

		return bar;
	}

	private Widget BuildTabs()
	{
		_tabs = new TabWidget( this );
		_tabs.AddPage( "Tools",     "build",                CreateToolsTab() );
		_tabs.AddPage( "Templates", "dashboard_customize",  CreateTemplatesTab() );
		_tabs.AddPage( "Docs",      "menu_book",            CreateDocsTab() );
		_tabs.AddPage( "Logs",      "terminal",             CreateLogsTab() );
		_tabs.AddPage( "Settings",  "settings",             CreateSettingsTab() );
		_tabs.StateCookie = "sboxpro.dock";
		return _tabs;
	}

	// ──────────────────────────────────────────────
	//  Tools tab
	// ──────────────────────────────────────────────

	private Widget CreateToolsTab()
	{
		var w = new Widget( null );
		w.Layout = Layout.Column();
		w.Layout.Margin = new Sandbox.UI.Margin( 16, 14, 16, 14 );
		w.Layout.Spacing = 10;

		w.Layout.Add( SectionHeader( "Tool Registry", $"{ToolRegistry.Count} tools available to Claude over MCP." ) );

		_toolsFilter = new LineEdit( null );
		_toolsFilter.PlaceholderText = "Filter by name or description…";
		_toolsFilter.TextChanged += _ => RebuildToolsList();
		w.Layout.Add( _toolsFilter );

		var scroll = new ScrollArea( null );
		_toolsList = new Widget();
		_toolsList.Layout = Layout.Column();
		_toolsList.Layout.Spacing = 0;
		scroll.Canvas = _toolsList;
		w.Layout.Add( scroll, 1 );

		RebuildToolsList();
		return w;
	}

	private void RebuildToolsList()
	{
		if ( _toolsList == null ) return;
		_toolsList.Layout.Clear( true );

		var filter = _toolsFilter?.Text?.Trim() ?? "";
		var groups = ToolRegistry.Tools.Values
			.Where( t => string.IsNullOrEmpty( filter )
				|| t.Name.Contains( filter, StringComparison.OrdinalIgnoreCase )
				|| (t.Description?.Contains( filter, StringComparison.OrdinalIgnoreCase ) ?? false) )
			.GroupBy( t => PrettifyGroup( t.Method?.DeclaringType?.Name ?? "Misc" ) )
			.OrderBy( g => g.Key );

		foreach ( var group in groups )
		{
			_toolsList.Layout.Add( SectionDivider( group.Key ) );

			foreach ( var tool in group.OrderBy( t => t.Name ) )
			{
				var row = new Widget();
				row.Layout = Layout.Column();
				row.Layout.Margin = new Sandbox.UI.Margin( 0, 6, 0, 6 );
				row.Layout.Spacing = 1;

				row.Layout.Add( MakeLabel( tool.Name, $"{LblBase} font-family: monospace; font-size: 11px; color: {Mono};" ) );

				if ( !string.IsNullOrEmpty( tool.Description ) )
					row.Layout.Add( MakeLabel( tool.Description, $"{LblBase} font-size: 10px; color: {TextSecondary};", wordWrap: true ) );

				_toolsList.Layout.Add( row );
			}
		}

		_toolsList.Layout.AddStretchCell();
	}

	private static string PrettifyGroup( string typeName )
	{
		if ( typeName.EndsWith( "Tools" ) ) return typeName.Substring( 0, typeName.Length - 5 );
		return typeName;
	}

	// ──────────────────────────────────────────────
	//  Templates tab
	// ──────────────────────────────────────────────

	private Widget CreateTemplatesTab()
	{
		var w = new Widget( null );
		w.Layout = Layout.Column();
		w.Layout.Margin = new Sandbox.UI.Margin( 16, 14, 16, 14 );
		w.Layout.Spacing = 10;

		w.Layout.Add( SectionHeader( "Templates", "Generate ready-to-use code, components, and scenes. Invoke by tool name in any Claude prompt." ) );

		var scroll = new ScrollArea( null );
		var canvas = new Widget();
		canvas.Layout = Layout.Column();
		canvas.Layout.Spacing = 0;
		scroll.Canvas = canvas;
		w.Layout.Add( scroll, 1 );

		canvas.Layout.Add( SectionDivider( "Game Starters" ) );
		AddTemplate( canvas, "start_multiplayer_fps_project", "Multiplayer FPS",      "Player + networked sync + pistol & knife + ready-to-Play scene." );
		AddTemplate( canvas, "start_parkour_game",            "Parkour / Obby",       "Player + ragdoll on fall + multiplayer scene tuned for movement." );
		AddTemplate( canvas, "start_survival_game",           "Survival + Inventory", "Player + Tetris inventory with hotbar/drag-drop + grab/interact + scene." );

		canvas.Layout.Add( SectionDivider( "Player & Combat" ) );
		AddTemplate( canvas, "template_player_controller", "Player Controller",  "First & third-person controller with sprint, crouch, jump, and camera." );
		AddTemplate( canvas, "template_networked_player",  "Networked Player",   "Synced name & health with broadcast damage and heal RPCs." );
		AddTemplate( canvas, "template_weapon",            "Weapon System",      "Abstract base + ranged hitscan pistol and melee knife. Cooldown, ammo, reload, hit events." );
		AddTemplate( canvas, "template_shrimple_pawn",     "Pawn Architecture",  "Classic Pawn / Client / Game scaffolding for server-authoritative play." );

		canvas.Layout.Add( SectionDivider( "Inventory & Interaction" ) );
		AddTemplate( canvas, "template_inventory",          "Inventory + Hotbar", "Tetris grid, 9-slot hotbar, drag-drop UI, stack counts. Razor-based." );
		AddTemplate( canvas, "template_grab_component",     "Grab",               "Physics pickup with raycast targeting and rotation." );
		AddTemplate( canvas, "template_interact_component", "Interact",           "Raycast interactor with cooldown and tag filter." );
		AddTemplate( canvas, "template_zoom_component",     "Zoom",               "FOV-driven zoom plus first/third-person cycling." );
		AddTemplate( canvas, "template_trigger_zone",       "Trigger Zone",       "Collider OnEnter/OnExit with tag filter and fire-once." );

		canvas.Layout.Add( SectionDivider( "Networking" ) );
		AddTemplate( canvas, "template_net_cooldown",   "Net Cooldown",   "Host-validated, client-replicated cooldowns keyed by string." );
		AddTemplate( canvas, "template_net_visibility", "Net Visibility", "Distance-based and owner-only visibility component." );

		canvas.Layout.Add( SectionDivider( "World & AI" ) );
		AddTemplate( canvas, "template_dresser",          "Citizen Dresser", "Networked clothing, height, age, skin tint, with workshop support." );
		AddTemplate( canvas, "template_shrimple_ragdoll", "Ragdoll Driver",  "Ragdoll wrapper with five modes, hit reactions, and get-up." );
		AddTemplate( canvas, "template_astar_npc",        "A* NPC",          "Pathfinding NPC built on a navigation grid." );

		canvas.Layout.Add( SectionDivider( "Scenes" ) );
		AddTemplate( canvas, "template_empty_scene",       "Empty Scene",       "Camera, sun, ambient, and skybox pre-placed." );
		AddTemplate( canvas, "template_multiplayer_basic", "Multiplayer Basic", "NetworkHelper plus four spawn points around origin." );

		canvas.Layout.AddStretchCell();
		return w;
	}

	private void AddTemplate( Widget canvas, string toolName, string title, string desc )
	{
		var row = new Widget();
		row.Layout = Layout.Column();
		row.Layout.Margin = new Sandbox.UI.Margin( 0, 8, 0, 8 );
		row.Layout.Spacing = 2;

		row.Layout.Add( MakeLabel( title, $"{LblBase} font-size: 12px; font-weight: 600; color: {TextPrimary};" ) );
		row.Layout.Add( MakeLabel( toolName, $"{LblBase} font-family: monospace; font-size: 10px; color: {Mono};" ) );
		row.Layout.Add( MakeLabel( desc, $"{LblBase} font-size: 10px; color: {TextSecondary}; margin-top: 1px;", wordWrap: true ) );

		canvas.Layout.Add( row );
	}

	// ──────────────────────────────────────────────
	//  Docs tab
	// ──────────────────────────────────────────────

	private Widget CreateDocsTab()
	{
		var w = new Widget( null );
		w.Layout = Layout.Column();
		w.Layout.Margin = new Sandbox.UI.Margin( 16, 14, 16, 14 );
		w.Layout.Spacing = 10;

		w.Layout.Add( SectionHeader( "Documentation", "Wiki and API reference indexed natively. Searchable from any Claude prompt — no external MCP." ) );

		var scroll = new ScrollArea( null );
		var canvas = new Widget();
		canvas.Layout = Layout.Column();
		canvas.Layout.Spacing = 0;
		scroll.Canvas = canvas;
		w.Layout.Add( scroll, 1 );

		canvas.Layout.Add( SectionDivider( "Search" ) );
		AddTemplate( canvas, "docs_search",       "Wiki Search",     "Full-text search across the indexed S&Box wiki pages." );
		AddTemplate( canvas, "docs_search_api",   "API Search",      "Search type and member names across the full engine API surface." );
		AddTemplate( canvas, "docs_get_api_type", "API Type Detail", "Read a full type with members, paginated for large classes." );

		canvas.Layout.Add( SectionDivider( "Index" ) );
		AddTemplate( canvas, "docs_cache_status",  "Cache Status",  "Inspect indexer state, page counts, and freshness." );
		AddTemplate( canvas, "docs_refresh_index", "Refresh Index", "Force a re-index of the wiki and API schema." );

		canvas.Layout.Add( SectionDivider( "Runtime Reflection" ) );
		AddTemplate( canvas, "describe_type",             "Describe Type",     "Reflect on any loaded type, including project-local code." );
		AddTemplate( canvas, "search_types",              "Search Types",      "Filter loaded types by namespace, name, or attribute." );
		AddTemplate( canvas, "get_method_signature",      "Method Signature",  "Resolve a single method signature from a type." );
		AddTemplate( canvas, "list_available_components", "List Components",   "Enumerate every Component derived class registered in the editor." );

		canvas.Layout.AddStretchCell();
		return w;
	}

	// ──────────────────────────────────────────────
	//  Logs tab
	// ──────────────────────────────────────────────

	private Widget CreateLogsTab()
	{
		var w = new Widget( null );
		w.Layout = Layout.Column();
		w.Layout.Margin = new Sandbox.UI.Margin( 16, 14, 16, 14 );
		w.Layout.Spacing = 10;

		var headerRow = Layout.Row();
		headerRow.Spacing = 8;

		var col = new Widget();
		col.Layout = Layout.Column();
		col.Layout.Spacing = 1;
		col.Layout.Add( MakeLabel( "Activity Log", $"{LblBase} font-size: 14px; font-weight: 700; color: {TextPrimary};" ) );
		col.Layout.Add( MakeLabel( "Live tail of MCP server, tool calls, and editor events.", $"{LblBase} font-size: 11px; color: {TextSecondary};" ) );
		headerRow.Add( col );
		headerRow.AddStretchCell();

		var clearBtn = new Button( "Clear", "delete_sweep" );
		clearBtn.Clicked += () =>
		{
			SboxProLog.Clear();
			RebuildLogEntries();
		};
		headerRow.Add( clearBtn );
		w.Layout.Add( headerRow );

		_logScroll = new ScrollArea( null );
		_logScroll.MinimumHeight = 220;

		var canvas = new Widget();
		canvas.Layout = Layout.Column();
		canvas.Layout.Spacing = 0;
		_logScroll.Canvas = canvas;

		foreach ( var entry in SboxProLog.Entries )
			AddLogEntry( canvas, entry );

		SboxProLog.OnEntry += entry =>
		{
			var canvasRef = _logScroll?.Canvas;
			if ( canvasRef != null ) AddLogEntry( canvasRef, entry );
		};

		w.Layout.Add( _logScroll, 1 );
		return w;
	}

	private void RebuildLogEntries()
	{
		if ( _logScroll?.Canvas == null ) return;
		_logScroll.Canvas.DestroyChildren();
	}

	private void AddLogEntry( Widget canvas, LogEntry entry )
	{
		var color = entry.Severity switch
		{
			LogSeverity.Error   => Error,
			LogSeverity.Warning => Warning,
			_                   => "#cbd5e1"
		};
		canvas.Layout.Add( MakeLabel( entry.ToString(), $"{LblBase} font-size: 11px; color: {color}; font-family: monospace;", wordWrap: true ) );
	}

	// ──────────────────────────────────────────────
	//  Settings tab
	// ──────────────────────────────────────────────

	private Widget CreateSettingsTab()
	{
		var w = new Widget( null );
		w.Layout = Layout.Column();
		w.Layout.Margin = new Sandbox.UI.Margin( 16, 14, 16, 14 );
		w.Layout.Spacing = 14;

		w.Layout.Add( SectionHeader( "Settings", "Server controls, skill installation, and version info." ) );

		// Server
		w.Layout.Add( SectionDivider( "Server" ) );
		w.Layout.Add( KeyValueRow( "Port",     SboxProServer.Port.ToString() ) );
		w.Layout.Add( KeyValueRow( "Tools",    ToolRegistry.Count.ToString() ) );
		w.Layout.Add( KeyValueRow( "Endpoint", $"http://localhost:{SboxProServer.Port}" ) );

		var btnRow = Layout.Row();
		btnRow.Spacing = 8;
		btnRow.Margin = new Sandbox.UI.Margin( 0, 6, 0, 0 );

		var restartBtn = new Button( "Restart Server", "refresh" );
		restartBtn.Clicked += () =>
		{
			try
			{
				SboxProServer.Stop();
				SboxProServer.Start();
				SboxProLog.Info( "Settings", "Server restarted via dock." );
			}
			catch ( Exception ex )
			{
				SboxProLog.Error( "Settings", $"Restart failed: {ex.Message}" );
			}
		};
		btnRow.Add( restartBtn );

		var hotloadBtn = new Button( "Trigger Hotload", "build" );
		hotloadBtn.Clicked += () =>
		{
			// Was: EditorEvent.Run( "scriptcompiler.compile" ) — that console command
			// doesn't exist in the engine (issue #04, "Unknown Command"). The actual
			// way to recompile from API is Editor.EditorUtility.Projects.Compile(...)
			// against the current open project.
			try
			{
				var project = Sandbox.Project.Current;
				if ( project is null )
				{
					SboxProLog.Warning( "Settings", "No current project — open a project first." );
					return;
				}
				_ = Editor.EditorUtility.Projects.Compile( project, msg => SboxProLog.Info( "Build", msg ) );
				SboxProLog.Info( "Settings", $"Compile started for project '{project.Config.Title}'." );
			}
			catch ( Exception ex )
			{
				SboxProLog.Error( "Settings", $"Hotload failed: {ex.Message}" );
			}
		};
		btnRow.Add( hotloadBtn );
		btnRow.AddStretchCell();
		w.Layout.Add( btnRow );

		// Skill
		w.Layout.Add( SectionDivider( "Claude Skill" ) );

		var skillStatusValue = new Label( "" );
		void RefreshSkillStatus()
		{
			if ( SkillInstaller.IsInstalled() )
			{
				skillStatusValue.Text = $"installed · v{SkillInstaller.GetInstalledVersion()}";
				skillStatusValue.SetStyles( $"{LblBase} font-family: monospace; font-size: 11px; color: {Success};" );
			}
			else
			{
				skillStatusValue.Text = "not installed";
				skillStatusValue.SetStyles( $"{LblBase} font-family: monospace; font-size: 11px; color: {Warning};" );
			}
		}
		var skillStatusRow = Layout.Row();
		skillStatusRow.Spacing = 8;
		var skillStatusLbl = MakeLabel( "Status", $"{LblBase} font-size: 11px; color: {TextSecondary}; min-width: 70px;" );
		skillStatusRow.Add( skillStatusLbl );
		skillStatusRow.Add( skillStatusValue );
		skillStatusRow.AddStretchCell();
		RefreshSkillStatus();
		w.Layout.Add( skillStatusRow );

		var pathRow = Layout.Row();
		pathRow.Spacing = 8;
		pathRow.Add( MakeLabel( "Path", $"{LblBase} font-size: 11px; color: {TextSecondary}; min-width: 70px;" ) );
		pathRow.Add( MakeLabel( SkillInstaller.InstalledRoot, $"{LblBase} font-family: monospace; font-size: 10px; color: {TextDim};", wordWrap: true ), 1 );
		w.Layout.Add( pathRow );

		w.Layout.Add( MakeLabel(
			"Auto-installed at editor open. Activates whenever a Claude conversation mentions s&box, sbox, .sbproj, Sandbox.Component, and similar — points Claude at curated reference instead of training-data guesses.",
			$"{LblBase} font-size: 10px; color: {TextDim}; margin-top: 4px;",
			wordWrap: true
		) );

		var skillBtnRow = Layout.Row();
		skillBtnRow.Spacing = 8;
		skillBtnRow.Margin = new Sandbox.UI.Margin( 0, 6, 0, 0 );
		var installBtn = new Button( "Install / Update", "download" );
		installBtn.Clicked += () =>
		{
			var r = SkillInstaller.Install( force: true );
			if ( r.Installed )
			{
				RefreshSkillStatus();
				SboxProLog.Info( "Skill", $"Installed {r.FilesCopied} files to {r.TargetPath}." );
			}
			else if ( r.AlreadyUpToDate )
			{
				SboxProLog.Info( "Skill", "Already up-to-date." );
			}
			else
			{
				SboxProLog.Error( "Skill", $"Install failed: {r.Error}" );
			}
		};
		skillBtnRow.Add( installBtn );
		skillBtnRow.AddStretchCell();
		w.Layout.Add( skillBtnRow );

		// About
		w.Layout.Add( SectionDivider( "About" ) );
		w.Layout.Add( MakeLabel( $"S&Box Pro v{SboxProServer.Version}", $"{LblBase} font-size: 12px; font-weight: 600; color: {TextPrimary};" ) );
		w.Layout.Add( MakeLabel(
			"Unified MCP and skill bundle for S&Box game development. One server, many tools — scene, components, assets, prefabs, scripts, physics, lighting, audio, FX, mesh, navigation, networking, console, templates, docs.",
			$"{LblBase} font-size: 11px; color: {TextSecondary};",
			wordWrap: true
		) );
		w.Layout.Add( MakeLabel( "Made by KiKoZl", $"{LblBase} font-size: 11px; color: {Accent}; margin-top: 6px;" ) );

		w.Layout.AddStretchCell();
		return w;
	}

	// ──────────────────────────────────────────────
	//  Helpers
	// ──────────────────────────────────────────────

	private Widget SectionHeader( string title, string subtitle )
	{
		var col = new Widget();
		col.Layout = Layout.Column();
		col.Layout.Spacing = 2;

		col.Layout.Add( MakeLabel( title, $"{LblBase} font-size: 14px; font-weight: 700; color: {TextPrimary};" ) );
		if ( !string.IsNullOrEmpty( subtitle ) )
			col.Layout.Add( MakeLabel( subtitle, $"{LblBase} font-size: 11px; color: {TextSecondary};", wordWrap: true ) );

		return col;
	}

	private Widget SectionDivider( string text )
	{
		var col = new Widget();
		col.Layout = Layout.Column();
		col.Layout.Margin = new Sandbox.UI.Margin( 0, 14, 0, 4 );
		col.Layout.Spacing = 0;

		col.Layout.Add( MakeLabel( text.ToUpperInvariant(), $"{LblBase} font-size: 10px; font-weight: 700; color: {Accent}; letter-spacing: 1.4px;" ) );

		return col;
	}

	private Widget KeyValueRow( string key, string value )
	{
		var row = new Widget();
		row.Layout = Layout.Row();
		row.Layout.Spacing = 8;

		row.Layout.Add( MakeLabel( key, $"{LblBase} font-size: 11px; color: {TextSecondary}; min-width: 70px;" ) );
		row.Layout.Add( MakeLabel( value, $"{LblBase} font-family: monospace; font-size: 11px; color: {Mono};" ) );
		row.Layout.AddStretchCell();
		return row;
	}

	private Label MakeLabel( string text, string styles, bool wordWrap = false )
	{
		var lbl = new Label( text );
		lbl.SetStyles( styles );
		if ( wordWrap ) lbl.WordWrap = true;
		return lbl;
	}
}

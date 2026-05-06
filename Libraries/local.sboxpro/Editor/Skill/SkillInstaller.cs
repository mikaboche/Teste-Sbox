using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SboxPro;

/// <summary>
/// Vendors the bundled S&Box Pro skill into the user's global Claude skills
/// directory at ~/.claude/skills/sbox-pro/. Copies all reference markdown
/// files so that any Claude Code conversation (regardless of project) can
/// activate the skill on s&box-related prompts.
///
/// Idempotent: re-runs at every editor init but only overwrites if the
/// bundled version is newer than the installed copy. User-edited installs
/// are detected via a sentinel file (`.sbox-pro.version`) and not clobbered.
/// </summary>
public static class SkillInstaller
{
	private const string SkillName = "sbox-pro";
	private const string SkillVersion = "1.1.0";
	private const string VersionSentinel = ".sbox-pro.version";
	private const string ProjectRulesMarkerBegin = "<!-- sbox-pro-rules:begin -->";
	private const string ProjectRulesMarkerEnd = "<!-- sbox-pro-rules:end -->";

	public static string InstalledRoot
	{
		get
		{
			var home = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
			return Path.Combine( home, ".claude", "skills", SkillName );
		}
	}

	public static string BundledRoot => GetSkillSourceRoot();

	/// <summary>Result of an installation attempt.</summary>
	public sealed class InstallResult
	{
		public bool Installed;
		public bool AlreadyUpToDate;
		public bool BundledMissing;
		public int FilesCopied;
		public string TargetPath;
		public string Error;
	}

	/// <summary>Auto-install at editor init. Silent on success.</summary>
	public static void TryAutoInstall()
	{
		try
		{
			var bundled = BundledRoot;
			if ( !Directory.Exists( bundled ) ) return; // bundled skill not present (dev environment may strip it)

			var installed = InstalledRoot;
			var existingVersion = ReadVersion( installed );
			if ( existingVersion != SkillVersion )
			{
				var result = Install( force: false );
				if ( result.Installed )
					SboxProLog.Info( "SkillInstaller", $"Skill installed at {result.TargetPath} ({result.FilesCopied} files)." );
			}

			// Always try to (idempotently) install the project-level rules block. Cheap,
			// and catches the case where the user deletes/edits CLAUDE.md after install.
			TryInstallProjectRules();

			// Layer 4: register a user-scope PreToolUse hook so every mcp__sbox-pro__*
			// invocation gets a system reminder pointing at the relevant skill reference.
			// Deterministic — runs in the harness, not the model.
			TryInstallHook();
		}
		catch ( Exception ex )
		{
			SboxProLog.Warn( "SkillInstaller", $"Auto-install failed: {ex.Message}" );
		}
	}

	/// <summary>
	/// Idempotently inject a managed rules block into the consumer project's CLAUDE.md.
	/// CLAUDE.md instructions are authoritative for Claude Code, so this is the most
	/// reliable way to force pre-MCP skill consultation across all sessions in the
	/// project. The block is sandwiched between begin/end HTML-comment markers so we
	/// can detect existing installs and update without clobbering user content.
	/// </summary>
	private static void TryInstallProjectRules()
	{
		try
		{
			var projectRoot = Sandbox.Project.Current?.RootDirectory?.FullName;
			if ( string.IsNullOrEmpty( projectRoot ) ) return;

			var claudePath = Path.Combine( projectRoot, "CLAUDE.md" );
			var existing = File.Exists( claudePath ) ? File.ReadAllText( claudePath ) : "";

			var block = BuildProjectRulesBlock();

			if ( existing.Contains( ProjectRulesMarkerBegin ) && existing.Contains( ProjectRulesMarkerEnd ) )
			{
				// Update in place, preserving user content outside the markers
				var startIdx = existing.IndexOf( ProjectRulesMarkerBegin );
				var endIdx = existing.IndexOf( ProjectRulesMarkerEnd ) + ProjectRulesMarkerEnd.Length;
				var before = existing.Substring( 0, startIdx ).TrimEnd();
				var after = existing.Substring( endIdx ).TrimStart();
				var current = existing.Substring( startIdx, endIdx - startIdx );

				if ( current == block ) return; // no-op — already current

				var rebuilt = (before.Length > 0 ? before + "\n\n" : "") + block + (after.Length > 0 ? "\n\n" + after : "") + "\n";
				File.WriteAllText( claudePath, rebuilt );
				SboxProLog.Info( "SkillInstaller", $"Updated sbox-pro rules block in {claudePath}." );
			}
			else
			{
				// Append to end of file, or create CLAUDE.md if absent
				var separator = existing.Length == 0 ? "" : (existing.TrimEnd() + "\n\n");
				File.WriteAllText( claudePath, separator + block + "\n" );
				SboxProLog.Info( "SkillInstaller", $"Installed sbox-pro rules block in {claudePath}." );
			}
		}
		catch ( Exception ex )
		{
			SboxProLog.Warn( "SkillInstaller", $"Project rules install failed: {ex.Message}" );
		}
	}

	private static string BuildProjectRulesBlock()
	{
		return ProjectRulesMarkerBegin + @"
## sbox-pro skill — hard rule (auto-managed by S&Box Pro v" + SkillVersion + @")

This block is managed by the S&Box Pro MCP installer. Edits inside the markers will be overwritten on the next install. Add your own rules **outside** the markers.

**Before writing any s&box C# / razor / scss, OR invoking any `mcp__sbox-pro__*` tool that mutates code/scenes/components/properties, you MUST:**

1. Read the relevant file under `~/.claude/skills/sbox-pro/references/` first — `core-concepts.md` for components, `ui-razor.md` for UI, `networking.md` for `[Sync]`/RPCs, `input-and-physics.md` for input/raycasts, `api-schema-core.md` for type signatures, `patterns-and-examples.md` for full worked examples.
2. Verify every type, attribute, and method name you plan to use exists in the schema. If it isn't there, do NOT write it — re-check the design.
3. When applying APIs you've never used before in this project, prefer `describe_type` / `docs_get_api_type` to confirm signatures live.

**Why this rule exists:** without consulting the skill first, tool calls and generated code drift toward Unity / older s&box snapshots and produce SB2000 compile errors, broken prefab refs, missing input actions, and engine warnings. Every time the rule was bypassed during development, a new ISSUE landed in `Docs/ISSUES.md` of the sbox-pro repo. Consulting the skill first prevents the entire class of bugs.

**Do not bypass this rule for ""simple"" tasks.** Muscle memory from Unity is exactly when this drift happens.
" + ProjectRulesMarkerEnd;
	}

	/// <summary>Manual install (called from dock button). force=true overwrites user edits.</summary>
	public static InstallResult Install( bool force )
	{
		var result = new InstallResult { TargetPath = InstalledRoot };

		try
		{
			var bundled = BundledRoot;
			if ( !Directory.Exists( bundled ) )
			{
				result.BundledMissing = true;
				result.Error = $"Bundled skill not found at {bundled}.";
				return result;
			}

			Directory.CreateDirectory( InstalledRoot );

			var existingVersion = ReadVersion( InstalledRoot );
			if ( !force && existingVersion == SkillVersion )
			{
				result.AlreadyUpToDate = true;
				return result;
			}

			int copied = 0;
			foreach ( var file in Directory.EnumerateFiles( bundled, "*", SearchOption.AllDirectories ) )
			{
				var rel = Path.GetRelativePath( bundled, file );
				var dst = Path.Combine( InstalledRoot, rel );
				Directory.CreateDirectory( Path.GetDirectoryName( dst ) );
				File.Copy( file, dst, overwrite: true );
				copied++;
			}

			WriteVersion( InstalledRoot, SkillVersion );
			result.Installed = true;
			result.FilesCopied = copied;
			return result;
		}
		catch ( Exception ex )
		{
			result.Error = ex.Message;
			return result;
		}
	}

	public static string GetInstalledVersion() => ReadVersion( InstalledRoot );

	public static bool IsInstalled() => File.Exists( Path.Combine( InstalledRoot, VersionSentinel ) );

	private static string ReadVersion( string root )
	{
		var path = Path.Combine( root, VersionSentinel );
		return File.Exists( path ) ? File.ReadAllText( path ).Trim() : null;
	}

	private static void WriteVersion( string root, string version )
	{
		File.WriteAllText( Path.Combine( root, VersionSentinel ), version );
	}

	/// <summary>
	/// Idempotently install a user-scope PreToolUse hook that fires before any
	/// `mcp__sbox-pro__*` tool call and injects a system reminder pointing Claude
	/// at the relevant skill reference. The hook runs in the Claude Code harness
	/// (not the model), so it's deterministic — the model can't choose to skip it.
	/// </summary>
	private static void TryInstallHook()
	{
		try
		{
			// 1. Hook script lives next to the skill references at ~/.claude/skills/sbox-pro/
			//    so the script can resolve its sibling references/ folder reliably.
			var hookScriptDst = Path.Combine( InstalledRoot, "sbox-pro-hook.js" );
			// (script is copied as part of Install() — same directory traversal — but
			// older 1.0.0 installs that pre-date the hook won't have it; if missing,
			// pull it from the bundled source now.)
			if ( !File.Exists( hookScriptDst ) )
			{
				var hookScriptSrc = Path.Combine( BundledRoot, "sbox-pro-hook.js" );
				if ( File.Exists( hookScriptSrc ) )
					File.Copy( hookScriptSrc, hookScriptDst, overwrite: true );
				else return; // bundled hook missing — nothing to install
			}

			// 2. Merge into ~/.claude/settings.json
			var home = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
			var settingsPath = Path.Combine( home, ".claude", "settings.json" );
			Directory.CreateDirectory( Path.GetDirectoryName( settingsPath ) );

			JsonObject root;
			if ( File.Exists( settingsPath ) )
			{
				var raw = File.ReadAllText( settingsPath );
				try { root = (JsonObject)JsonNode.Parse( raw ); }
				catch { root = new JsonObject(); }
				if ( root == null ) root = new JsonObject();
			}
			else
			{
				root = new JsonObject();
			}

			if ( !( root["hooks"] is JsonObject hooks ) )
			{
				hooks = new JsonObject();
				root["hooks"] = hooks;
			}
			if ( !( hooks["PreToolUse"] is JsonArray preToolUse ) )
			{
				preToolUse = new JsonArray();
				hooks["PreToolUse"] = preToolUse;
			}

			// Idempotent: look for an existing entry whose first hook command references our script
			JsonObject targetGroup = null;
			foreach ( var node in preToolUse )
			{
				if ( node is not JsonObject group ) continue;
				if ( group["hooks"] is not JsonArray inner ) continue;
				foreach ( var h in inner )
				{
					if ( h is not JsonObject ho ) continue;
					var cmd = ho["command"]?.GetValue<string>() ?? "";
					if ( cmd.Contains( "sbox-pro-hook.js" ) ) { targetGroup = group; break; }
				}
				if ( targetGroup != null ) break;
			}

			var desiredCommand = $"node \"{hookScriptDst.Replace( '\\', '/' )}\"";
			var desiredGroup = new JsonObject
			{
				["matcher"] = "mcp__sbox-pro__.*",
				["hooks"] = new JsonArray
				{
					new JsonObject
					{
						["type"] = "command",
						["command"] = desiredCommand,
						["timeout"] = 5,
					},
				},
			};

			if ( targetGroup == null )
			{
				preToolUse.Add( desiredGroup );
				SboxProLog.Info( "SkillInstaller", $"Registered PreToolUse hook in {settingsPath}." );
			}
			else
			{
				// Replace the matched entry to keep command path / matcher current
				var idx = preToolUse.IndexOf( targetGroup );
				preToolUse[idx] = desiredGroup;
			}

			var json = root.ToJsonString( new JsonSerializerOptions { WriteIndented = true } );
			File.WriteAllText( settingsPath, json );
		}
		catch ( Exception ex )
		{
			SboxProLog.Warn( "SkillInstaller", $"Hook install failed: {ex.Message}" );
		}
	}

	/// <summary>
	/// Resolves the bundled Skill/ directory at runtime. We can't use
	/// Assembly.Location (sandbox returns null), so derive it from the
	/// CallerFilePath of this very source file: SbX-Pro source lives in
	/// Libraries/local.sboxpro/Editor/Skill/, and the bundled Skill/ is
	/// 4 levels up from there at the project root.
	/// </summary>
	private static string GetSkillSourceRoot( [CallerFilePath] string sourceFile = "" )
	{
		// sourceFile = .../Libraries/local.sboxpro/Editor/Skill/SkillInstaller.cs
		// We want   = .../Libraries/local.sboxpro/Skill (vendored alongside Editor/)
		var libRoot = Path.GetFullPath( Path.Combine( Path.GetDirectoryName( sourceFile ), "..", ".." ) );
		var local = Path.Combine( libRoot, "Skill" );
		if ( Directory.Exists( local ) ) return local;

		// Dev fallback — when running from the source repo, the Skill/ folder
		// lives at the sbox-pro project root, NOT inside Libraries/local.sboxpro/.
		// Walk up until we find a Skill/ sibling to Libraries/.
		var probe = libRoot;
		for ( int i = 0; i < 4 && probe != null; i++ )
		{
			var candidate = Path.Combine( probe, "Skill" );
			if ( Directory.Exists( candidate ) ) return candidate;
			probe = Path.GetDirectoryName( probe );
		}
		return local; // returns the not-found path so caller can detect via Directory.Exists
	}
}

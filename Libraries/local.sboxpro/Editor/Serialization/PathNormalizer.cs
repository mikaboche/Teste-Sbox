using System;
using System.IO;
using Sandbox;

namespace SboxPro;

/// <summary>
/// Normalizes file paths for S&Box project operations.
/// Handles conversion between absolute/relative, forward/back slashes,
/// and project-relative asset paths.
/// </summary>
public static class PathNormalizer
{
	private static string _cachedProjectRoot;

	/// <summary>
	/// Normalize a path: forward slashes, no trailing slash, no double slashes.
	/// </summary>
	public static string Normalize( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		path = path.Replace( '\\', '/' );

		// Remove double slashes (except protocol like http://)
		while ( path.Contains( "//" ) )
		{
			var idx = path.IndexOf( "//" );
			if ( idx > 0 && path[idx - 1] == ':' )
				break; // protocol
			path = path.Remove( idx, 1 );
		}

		// Remove trailing slash
		if ( path.Length > 1 && path.EndsWith( "/" ) )
			path = path.TrimEnd( '/' );

		return path;
	}

	/// <summary>
	/// Get the project root directory (where the .sbproj lives).
	/// Uses Sandbox.Project.Current.GetRootPath() — the engine's authoritative source.
	/// </summary>
	public static string GetProjectRoot()
	{
		if ( _cachedProjectRoot != null )
			return _cachedProjectRoot;

		_cachedProjectRoot = Normalize( Project.Current.GetRootPath() );
		return _cachedProjectRoot;
	}

	/// <summary>
	/// Convert an absolute path to a project-relative path.
	/// Returns the path as-is if it's not under the project root.
	/// </summary>
	public static string ToRelative( string absolutePath )
	{
		if ( string.IsNullOrWhiteSpace( absolutePath ) )
			return string.Empty;

		absolutePath = Normalize( absolutePath );
		var root = GetProjectRoot();

		if ( absolutePath.StartsWith( root, StringComparison.OrdinalIgnoreCase ) )
		{
			var relative = absolutePath.Substring( root.Length );
			if ( relative.StartsWith( "/" ) )
				relative = relative.Substring( 1 );
			return relative;
		}

		return absolutePath;
	}

	/// <summary>
	/// Convert a project-relative path to an absolute path.
	/// </summary>
	public static string ToAbsolute( string relativePath )
	{
		if ( string.IsNullOrWhiteSpace( relativePath ) )
			return string.Empty;

		relativePath = Normalize( relativePath );

		// Already absolute?
		if ( Path.IsPathRooted( relativePath ) )
			return relativePath;

		var root = GetProjectRoot();
		return Normalize( Path.Combine( root, relativePath ) );
	}

	/// <summary>
	/// Get the file extension (lowercase, with dot).
	/// </summary>
	public static string GetExtension( string path )
	{
		return Path.GetExtension( path )?.ToLowerInvariant() ?? string.Empty;
	}

	/// <summary>
	/// Check if a path points to a scene file.
	/// </summary>
	public static bool IsScene( string path )
	{
		return GetExtension( path ) == ".scene";
	}

	/// <summary>
	/// Check if a path points to a prefab file.
	/// </summary>
	public static bool IsPrefab( string path )
	{
		return GetExtension( path ) == ".prefab";
	}

	/// <summary>
	/// Ensure a path is within the project. Returns null if outside.
	/// Prevents path traversal attacks.
	/// </summary>
	public static string SafeResolve( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		var absolute = ToAbsolute( path );
		var root = GetProjectRoot();

		// Resolve to remove any ../ traversal
		var resolved = Normalize( Path.GetFullPath( absolute ) );

		if ( !resolved.StartsWith( root, StringComparison.OrdinalIgnoreCase ) )
			return null;

		return resolved;
	}

	/// <summary>
	/// Top-level project folders that are NOT under Assets/. If the user-supplied path
	/// starts with one of these, NormalizeAssetPath leaves it alone — code lives in
	/// Code/, editor code in Editor/, libraries in Libraries/. Forcing them under
	/// Assets/ silently misroutes generated files (was issue #08: composer-generated
	/// .cs files ended up in Assets/Code/ which the project's csproj doesn't compile).
	///
	/// IMPORTANT: scenes/ and prefabs/ are subdirs of Assets/ — they get the prefix.
	/// Including them here would route .scene/.prefab files to project root, conflicting
	/// with files in libraries that share the same relative path (regression issue #12).
	/// </summary>
	private static readonly string[] _projectRootDirs =
	{
		"Assets/",
		"Code/",
		"Editor/",
		"Libraries/",
		"Localization/",
		"ProjectSettings/",
	};

	private static bool IsProjectRootedPath( string path )
	{
		foreach ( var prefix in _projectRootDirs )
		{
			if ( path.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}
		return false;
	}

	/// <summary>
	/// Normalize a path. Behavior depends on the leading segment:
	/// <list type="bullet">
	///   <item>Path starts with a known project-root dir (Assets/, Code/, Editor/, Libraries/...) → returned unchanged</item>
	///   <item>Path starts with anything else → "Assets/" prefix added (back-compat with scene/prefab tools that pass "scenes/foo.scene")</item>
	/// </list>
	/// </summary>
	public static string NormalizeAssetPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return string.Empty;

		path = Normalize( path );

		if ( path.StartsWith( "/" ) )
			path = path.Substring( 1 );

		if ( IsProjectRootedPath( path ) )
			return path;

		return "Assets/" + path;
	}

	/// <summary>
	/// Convert a user-provided path into the form <see cref="ResourceLibrary"/> /
	/// <see cref="GameObject.GetPrefab"/> expect: project-relative WITHOUT the
	/// <c>Assets/</c> prefix. <see cref="NormalizeAssetPath"/> ADDS the prefix
	/// for filesystem lookups (File.Exists), but the engine's resource registry
	/// indexes assets with the prefix stripped — passing it in returns null.
	///
	/// Use this helper when bridging from a user-supplied path to a
	/// <see cref="ResourceLibrary.Get{T}"/> / <see cref="GameObject.GetPrefab"/>
	/// call. (#32)
	/// </summary>
	public static string ForResourceLibrary( string path )
	{
		var normalized = NormalizeAssetPath( path );
		if ( normalized.StartsWith( "Assets/", StringComparison.OrdinalIgnoreCase ) )
			return normalized.Substring( "Assets/".Length );
		return normalized;
	}

	/// <summary>
	/// Resolve a user-provided path to an absolute filesystem path. See <see cref="NormalizeAssetPath"/>
	/// for routing rules. Returns null if result would escape the project.
	/// </summary>
	public static string ResolveAssetPath( string path )
	{
		var normalized = NormalizeAssetPath( path );
		if ( string.IsNullOrEmpty( normalized ) )
			return null;

		var absolute = Normalize( Path.Combine( GetProjectRoot(), normalized ) );
		return SafeResolve( absolute ) != null ? absolute : null;
	}
}

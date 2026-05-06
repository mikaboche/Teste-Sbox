using System;
using System.Collections.Generic;
using System.Linq;

namespace SboxPro.Docs;

/// <summary>
/// Substring-based fuzzy ranking. Higher score = better match.
/// Used by docs_search and docs_search_api.
/// Scoring (lower is better, like golf):
///   - exact equals (case-insensitive): 0
///   - starts with query: 1
///   - word-boundary contains: 5
///   - mid-string contains: 10
///   - tokenized AND-match (all query words present): 15
///   - no match: -1
/// </summary>
public static class FuzzyMatcher
{
	public static int Score( string haystack, string needle )
	{
		if ( string.IsNullOrEmpty( haystack ) ) return -1;
		if ( string.IsNullOrEmpty( needle ) ) return 0;

		var hl = haystack.ToLowerInvariant();
		var nl = needle.ToLowerInvariant();

		if ( hl == nl ) return 0;
		if ( hl.StartsWith( nl ) ) return 1;

		var idx = hl.IndexOf( nl, StringComparison.Ordinal );
		if ( idx > 0 )
		{
			var prev = hl[idx - 1];
			if ( !char.IsLetterOrDigit( prev ) || char.IsUpper( haystack[idx] ) )
				return 5;
			return 10;
		}

		// Token AND match
		var tokens = nl.Split( new[] { ' ', '.', ',', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries );
		if ( tokens.Length > 1 && tokens.All( t => hl.Contains( t ) ) )
			return 15;

		return -1;
	}

	/// <summary>Combined score: best match across multiple haystacks (e.g. type name + namespace + members).</summary>
	public static int BestScore( string needle, params string[] haystacks )
	{
		if ( string.IsNullOrEmpty( needle ) ) return 0;
		int best = -1;
		foreach ( var h in haystacks )
		{
			var s = Score( h, needle );
			if ( s < 0 ) continue;
			if ( best < 0 || s < best ) best = s;
		}
		return best;
	}

	public static IEnumerable<T> RankAndTake<T>( IEnumerable<T> items, Func<T, string[]> haystackKeys, string query, int limit )
	{
		var scored = new List<(T item, int score)>();
		foreach ( var it in items )
		{
			var keys = haystackKeys( it );
			var s = BestScore( query, keys );
			if ( s < 0 ) continue;
			scored.Add( (it, s) );
		}
		return scored
			.OrderBy( x => x.score )
			.ThenBy( x => x.item is null ? 0 : 1 )
			.Take( limit )
			.Select( x => x.item );
	}
}

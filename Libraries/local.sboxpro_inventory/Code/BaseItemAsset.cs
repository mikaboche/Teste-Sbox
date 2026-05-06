// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

﻿using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// A base item asset class that inherits <see cref="GameResource"/>. Inherit this for any
/// further customization needed for game resource items.
/// </summary>
public abstract class BaseItemAsset : GameResource
{
	[Property] public string DisplayName { get; set; }
	[Property] public string Category { get; set; }
	[Property] public int MaxStackSize { get; set; } = 1;
	[Property] public int Width { get; set; } = 1;
	[Property] public int Height { get; set; } = 1;
}

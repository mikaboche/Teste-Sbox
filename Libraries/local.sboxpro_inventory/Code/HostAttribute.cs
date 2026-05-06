// Vendored from conna.inventory (MIT, by conna).
// Namespace renamed Conna.Inventory → SboxPro.Inventory. Functional code unchanged.
// See LICENSE-NOTICES.md at sbox-pro repo root for full attribution.

﻿using System;
using Sandbox;

namespace SboxPro.Inventory;

/// <summary>
/// Methods on an <see cref="InventoryItem"/> or a <see cref="BaseInventory"/> with this attribute will be invoked on the host.
/// </summary>
[CodeGenerator( CodeGeneratorFlags.Instance | CodeGeneratorFlags.WrapMethod, "SboxPro.Inventory.InventoryItem.InvokeOnHost" )]
[AttributeUsage( AttributeTargets.Method )]
public class HostAttribute : Attribute
{

}

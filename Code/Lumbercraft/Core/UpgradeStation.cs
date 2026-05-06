using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Bancada de upgrades. Adicionar a qualquer prop estático no hub.
/// Quando o jogador chega perto e pressiona "use", gasta madeira e aumenta o Damage da ferramenta.
/// </summary>
[Title( "Upgrade Station" )]
[Category( "Lumbercraft" )]
public sealed class UpgradeStation : Component
{
	/// <summary>Custo em madeira por upgrade de dano</summary>
	[Property, Range( 10f, 5000f )]
	public float DamageUpgradeCost { get; set; } = 200f;

	/// <summary>Quantidade de dano adicionada por upgrade</summary>
	[Property, Range( 5f, 500f )]
	public float DamageUpgradeAmount { get; set; } = 50f;

	/// <summary>Distância para interagir (~2m = 160 unidades s&box)</summary>
	[Property, Range( 50f, 300f )]
	public float InteractRadius { get; set; } = 160f;

	// Cooldown para evitar múltiplos upgrades no mesmo frame
	private TimeSince _timeSinceLastUpgrade;

	protected override void OnUpdate()
	{
		// Só processa o input "use" — Input.Pressed é true apenas no frame do clique
		if ( !Input.Pressed( "use" ) ) return;
		if ( _timeSinceLastUpgrade < 0.5f ) return;

		// Busca todos os PlayerResources na cena e verifica proximidade
		var players = Scene.GetAllComponents<PlayerResources>();

		foreach ( var player in players )
		{
			float dist = Vector3.DistanceBetween( WorldPosition, player.WorldPosition );
			if ( dist > InteractRadius ) continue;

			// Jogador próximo encontrado — tenta o upgrade
			TryUpgrade( player );
			_timeSinceLastUpgrade = 0;
			return; // Só processa um jogador por vez
		}
	}

	/// <summary>
	/// Tenta gastar madeira e aumentar o Damage do ChoppingTool do jogador.
	/// </summary>
	private void TryUpgrade( PlayerResources player )
	{
		// Verifica e gasta a madeira (SpendWood já loga resultado de falha)
		if ( !player.SpendWood( DamageUpgradeCost ) )
		{
			Log.Info( $"[UpgradeStation] Upgrade negado — madeira insuficiente. " +
			          $"Necessário: {DamageUpgradeCost} | Disponível: {player.Wood}" );
			return;
		}

		// Busca o ChoppingTool no mesmo GameObject do jogador (ou em filhos)
		var tool = player.GameObject.GetComponentInChildren<ChoppingTool>( includeDisabled: false );
		if ( tool is null )
		{
			// Se não achou em filhos, tenta direto no pai
			tool = player.GameObject.GetComponentInParent<ChoppingTool>( includeDisabled: false );
		}

		if ( tool is null )
		{
			Log.Warning( "[UpgradeStation] ChoppingTool não encontrado no jogador. " +
			             "Certifique-se de que ChoppingTool está no mesmo GameObject ou filho." );
			return;
		}

		// Aplica o upgrade
		float oldDamage = tool.Damage;
		tool.Damage += DamageUpgradeAmount;

		Log.Info( $"[UpgradeStation] ✅ Upgrade aplicado! " +
		          $"Damage: {oldDamage} → {tool.Damage} " +
		          $"(+{DamageUpgradeAmount}) | Madeira restante: {player.Wood}" );
	}

	/// <summary>
	/// Visualiza o raio de interação no editor.
	/// </summary>
	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.25f );
		Gizmo.Draw.LineSphere( Vector3.Zero, InteractRadius );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.WorldText(
			$"Upgrade: -{DamageUpgradeCost} 🪵 / +{DamageUpgradeAmount} DMG",
			new Transform( Vector3.Up * 60f )
		);
	}
}

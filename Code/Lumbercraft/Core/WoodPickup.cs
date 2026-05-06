using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Objeto físico de madeira spawnado ao derrubar uma árvore.
/// Cai com gravidade e é coletado automaticamente quando o jogador se aproxima.
/// </summary>
[Title( "Wood Pickup" )]
[Category( "Lumbercraft" )]
public sealed class WoodPickup : Component
{
	/// <summary>Quantidade de madeira que este pickup concede ao jogador</summary>
	[Property]
	public float WoodAmount { get; set; } = 50f;

	/// <summary>Distância para coleta automática (em unidades do s&box)</summary>
	[Property, Range( 20f, 200f )]
	public float PickupRadius { get; set; } = 60f;

	/// <summary>Tempo de vida máximo antes de se autodestruir (em segundos)</summary>
	[Property, Range( 5f, 120f )]
	public float Lifetime { get; set; } = 30f;

	private float _aliveTime = 0f;
	private bool _collected = false;

	protected override void OnStart()
	{
		// Garante que o Rigidbody está ativo para cair com gravidade
		var rb = GameObject.GetComponent<Rigidbody>();
		if ( rb is not null )
		{
			rb.Enabled = true;
		}

		Log.Info( $"[WoodPickup] Pickup spawnado com {WoodAmount} de madeira." );
	}

	protected override void OnUpdate()
	{
		if ( _collected ) return;

		// Controla o tempo de vida
		_aliveTime += Time.Delta;
		if ( _aliveTime >= Lifetime )
		{
			Log.Info( "[WoodPickup] Pickup expirado, destruindo." );
			GameObject.Destroy();
			return;
		}

		// Verifica proximidade com o jogador a cada frame
		CheckForNearbyPlayer();
	}

	/// <summary>
	/// Procura um jogador dentro do raio de coleta.
	/// Se encontrar, adiciona madeira ao PlayerResources e destrói este objeto.
	/// </summary>
	private void CheckForNearbyPlayer()
	{
		// Busca todos os GameObjects com PlayerResources na cena
		var players = Scene.GetAllComponents<PlayerResources>();

		foreach ( var player in players )
		{
			var dist = Vector3.DistanceBetween( Transform.Position, player.Transform.Position );
			if ( dist <= PickupRadius )
			{
				Collect( player );
				return;
			}
		}
	}

	/// <summary>
	/// Efetua a coleta: adiciona madeira ao jogador e destrói o pickup.
	/// </summary>
	private void Collect( PlayerResources player )
	{
		if ( _collected ) return;
		_collected = true;

		player.AddWood( WoodAmount );
		Log.Info( $"[WoodPickup] {WoodAmount} de madeira coletada pelo jogador!" );

		GameObject.Destroy();
	}
}

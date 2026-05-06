using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Componente colocado em qualquer prop de árvore na cena.
/// Gerencia HP, recebe dano, dropa madeira e ativa física de ragdoll ao morrer.
/// </summary>
[Title( "Tree Component" )]
[Category( "Lumbercraft" )]
public sealed class TreeComponent : Component
{
	/// <summary>HP máximo da árvore</summary>
	[Property, Range( 100f, 50000f )]
	public float MaxHP { get; set; } = 5000f;

	/// <summary>HP atual — começa igual ao máximo</summary>
	[Property]
	public float CurrentHP { get; set; } = 5000f;

	/// <summary>Quantidade de madeira dropada ao morrer</summary>
	[Property, Range( 1f, 500f )]
	public float WoodDrop { get; set; } = 50f;

	/// <summary>Prefab do WoodPickup que será spawnado ao morrer</summary>
	[Property]
	public GameObject WoodPickupPrefab { get; set; }

	// Guarda a posição original para o efeito de shake
	private Vector3 _originalPosition;
	private bool _isDead = false;

	protected override void OnStart()
	{
		CurrentHP = MaxHP;
		_originalPosition = Transform.LocalPosition;
	}

	/// <summary>
	/// Aplica dano à árvore. Se HP chegar a zero, mata a árvore.
	/// </summary>
	public void TakeDamage( float amount )
	{
		if ( _isDead ) return;

		CurrentHP -= amount;
		Log.Info( $"[Árvore] Dano recebido: {amount} | HP restante: {CurrentHP}/{MaxHP}" );

		// Efeito visual de shake ao receber dano
		_ = ShakeEffect();

		if ( CurrentHP <= 0f )
		{
			Die();
		}
	}

	/// <summary>
	/// Mata a árvore: ativa ragdoll, spawna madeira e desativa este componente.
	/// </summary>
	private void Die()
	{
		_isDead = true;
		Log.Info( $"[Árvore] Árvore derrubada! Dropando {WoodDrop} de madeira." );

		// Ativa física de ragdoll no ModelPhysics do GameObject
		var physics = GameObject.GetComponent<ModelPhysics>();
		if ( physics is not null )
		{
			physics.Enabled = true;
		}
		else
		{
			// Fallback: ativa o Rigidbody padrão se não houver ModelPhysics
			var rb = GameObject.GetComponent<Rigidbody>();
			if ( rb is not null ) rb.Enabled = true;
		}

		// Spawna o pickup de madeira na posição da árvore
		if ( WoodPickupPrefab is not null )
		{
			var pickup = WoodPickupPrefab.Clone( Transform.Position + Vector3.Up * 40f );
			var woodComp = pickup.GetComponent<WoodPickup>();
			if ( woodComp is not null )
			{
				woodComp.WoodAmount = WoodDrop;
			}
		}

		// Desativa este componente (árvore morta não recebe mais dano)
		Enabled = false;
	}

	/// <summary>
	/// Efeito de shake leve: desloca levemente o transform e volta à posição original.
	/// Guard IsValid garante que não acessa transform de objeto já destruído.
	/// </summary>
	private async Task ShakeEffect()
	{
		const int steps = 6;
		const float magnitude = 3f;
		const float stepDelay = 0.03f;

		for ( int i = 0; i < steps; i++ )
		{
			// Para a animação se o componente foi destruído durante o shake
			if ( !IsValid ) return;

			var offset = new Vector3(
				Game.Random.Float( -magnitude, magnitude ),
				Game.Random.Float( -magnitude, magnitude ),
				0f
			);
			Transform.LocalPosition = _originalPosition + offset;
			await Task.DelaySeconds( stepDelay );
		}

		// Retorna à posição original (se ainda existir)
		if ( IsValid )
			Transform.LocalPosition = _originalPosition;
	}
}

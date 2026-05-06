using Sandbox;
using System.Linq;

namespace Lumbercraft;

/// <summary>
/// Feedback visual da árvore. Adicionar no mesmo GameObject que TreeComponent.
/// Responsável por: piscar vermelho ao receber dano e spawnar fragmentos ao morrer.
/// </summary>
[Title( "Tree Visual" )]
[Category( "Lumbercraft" )]
public sealed class TreeVisual : Component
{
	/// <summary>Quantidade de cubinhos de fragmento ao morrer</summary>
	[Property, Range( 1, 15 )]
	public int FragmentCount { get; set; } = 5;

	/// <summary>Força do impulso inicial dos fragmentos</summary>
	[Property, Range( 100f, 1000f )]
	public float FragmentImpulse { get; set; } = 400f;

	/// <summary>Tempo de vida de cada fragmento (segundos)</summary>
	[Property, Range( 0.5f, 10f )]
	public float FragmentLifetime { get; set; } = 2f;

	private bool _flashing = false;

	protected override void OnStart()
	{
		// Busca o TreeComponent no mesmo GameObject e se inscreve nos eventos
		var tree = GetComponent<TreeComponent>();
		if ( tree is null )
		{
			Log.Warning( "[TreeVisual] TreeComponent não encontrado. Verifique se estão no mesmo GameObject." );
			return;
		}

		tree.OnDamageTaken += HandleDamageTaken;
		tree.OnDied        += HandleDied;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  CALLBACKS
	// ──────────────────────────────────────────────────────────────────────────

	private void HandleDamageTaken()
	{
		if ( !_flashing )
			_ = FlashRed();
	}

	private void HandleDied()
	{
		SpawnFragments();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  FLASH VERMELHO
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Troca o Tint de todos os ModelRenderers filhos para vermelho por 0.1s,
	/// depois restaura as cores originais.
	/// </summary>
	private async Task FlashRed()
	{
		_flashing = true;

		// Coleta todos os renderers filhos (tronco + copa)
		var renderers = GetComponentsInChildren<ModelRenderer>( includeDisabled: false ).ToList();
		if ( renderers.Count == 0 )
		{
			_flashing = false;
			return;
		}

		// Salva cores originais
		var originalColors = renderers.Select( r => r.Tint ).ToList();

		// Aplica vermelho
		foreach ( var r in renderers )
			r.Tint = Color.Red;

		await Task.DelaySeconds( 0.1f );

		// Restaura cores — verifica validade pois a árvore pode ter morrido
		if ( IsValid )
		{
			for ( int i = 0; i < renderers.Count; i++ )
			{
				if ( renderers[i].IsValid() )
					renderers[i].Tint = originalColors[i];
			}
		}

		_flashing = false;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  FRAGMENTOS DE MADEIRA
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Spawna cubinhos físicos voando em direções aleatórias ao redor da árvore.
	/// Cada fragmento some após FragmentLifetime segundos.
	/// </summary>
	private void SpawnFragments()
	{
		for ( int i = 0; i < FragmentCount; i++ )
		{
			var frag = new GameObject( true, "WoodFragment" );
			frag.SetParent( Scene );

			// Posição ligeiramente aleatória ao redor do centro da árvore
			frag.WorldPosition = WorldPosition + new Vector3(
				Game.Random.Float( -20f, 20f ),
				Game.Random.Float( -20f, 20f ),
				Game.Random.Float( 30f,  80f )   // começam um pouco acima do chão
			);

			// Visual: cubo pequeno cor de madeira
			frag.LocalScale = Vector3.One * 20f;
			var renderer = frag.AddComponent<ModelRenderer>();
			renderer.Model = Model.Load( "models/dev/box.vmdl" );
			renderer.Tint  = new Color(
				Game.Random.Float( 0.45f, 0.65f ),
				Game.Random.Float( 0.25f, 0.40f ),
				Game.Random.Float( 0.05f, 0.15f )
			); // variação de marrom

			// Física: collider + rigidbody com impulso aleatório
			frag.AddComponent<BoxCollider>();
			var rb = frag.AddComponent<Rigidbody>();

			// Impulso para fora e para cima
			var impulse = new Vector3(
				Game.Random.Float( -FragmentImpulse, FragmentImpulse ),
				Game.Random.Float( -FragmentImpulse, FragmentImpulse ),
				Game.Random.Float( FragmentImpulse * 0.5f, FragmentImpulse * 1.5f ) // sempre para cima
			);
			rb.ApplyImpulse( impulse );

			// Torque aleatório para girar no ar
			var torque = new Vector3(
				Game.Random.Float( -300f, 300f ),
				Game.Random.Float( -300f, 300f ),
				Game.Random.Float( -300f, 300f )
			);
			rb.ApplyTorque( torque );

			// Auto-destruição após lifetime
			_ = DestroyAfterSeconds( frag, FragmentLifetime );
		}
	}

	/// <summary>
	/// Destrói um GameObject após N segundos (fire-and-forget).
	/// </summary>
	private static async Task DestroyAfterSeconds( GameObject go, float seconds )
	{
		await Task.DelaySeconds( seconds );
		if ( go.IsValid() )
			go.Destroy();
	}
}

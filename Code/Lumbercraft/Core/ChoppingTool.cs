using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Ferramenta de corte. Adicionar ao GameObject do jogador (ou filho "Hand").
/// Detecta attack1, faz raycast para árvores no alcance e aplica dano com chance de crítico.
/// </summary>
[Title( "Chopping Tool" )]
[Category( "Lumbercraft" )]
public sealed class ChoppingTool : Component
{
	/// <summary>Dano base por golpe</summary>
	[Property, Range( 10f, 2000f )]
	public float Damage { get; set; } = 200f;

	/// <summary>Chance de crítico (0 = 0%, 1 = 100%)</summary>
	[Property, Range( 0f, 1f )]
	public float CritChance { get; set; } = 0.1f;

	/// <summary>Multiplicador de dano no crítico</summary>
	[Property, Range( 1f, 5f )]
	public float CritMultiplier { get; set; } = 2.0f;

	/// <summary>Alcance do golpe em unidades do s&box (~160u ≈ 2m)</summary>
	[Property, Range( 50f, 500f )]
	public float Range { get; set; } = 160f;

	/// <summary>Cooldown mínimo entre golpes (segundos)</summary>
	[Property, Range( 0.1f, 2f )]
	public float AttackCooldown { get; set; } = 0.4f;

	// Usando TimeSince para gerenciar cooldown sem estado manual
	private TimeSince _timeSinceLastHit;

	protected override void OnUpdate()
	{
		// Verifica input de ataque — Input.Pressed só é true no frame exato do clique
		if ( !Input.Pressed( "attack1" ) ) return;

		// Respeita cooldown para não dar dano a 60fps
		if ( _timeSinceLastHit < AttackCooldown ) return;

		_timeSinceLastHit = 0;
		TryChop();
	}

	/// <summary>
	/// Faz o raycast a partir da câmera (mira central da tela) e aplica dano
	/// se atingir um GameObject com TreeComponent dentro do alcance.
	/// </summary>
	private void TryChop()
	{
		// Usa a câmera ativa para obter o raio da mira central — padrão FPS
		var camera = Scene.Camera;
		if ( camera is null )
		{
			Log.Warning( "[ChoppingTool] Nenhuma câmera ativa na cena." );
			return;
		}

		// Raio da mira do centro da tela, com alcance limitado
		var ray = camera.ScreenPixelToRay( Screen.Size / 2f );

		// SceneTrace a partir da posição do jogador, ignorando o próprio collider
		var tr = Scene.Trace
			.Ray( ray, Range )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit )
		{
			Log.Info( "[ChoppingTool] Golpe no ar — nenhum alvo atingido." );
			return;
		}

		// Verifica se o objeto atingido tem um TreeComponent
		var tree = tr.GameObject.GetComponent<TreeComponent>( includeDisabled: false );
		if ( tree is null )
		{
			Log.Info( $"[ChoppingTool] Atingiu '{tr.GameObject.Name}', mas não é uma árvore." );
			return;
		}

		// Calcula crítico: Game.Random é auto-semeado por tick, seguro para usar aqui
		bool isCrit = Game.Random.Float( 0f, 1f ) < CritChance;
		float finalDamage = isCrit ? Damage * CritMultiplier : Damage;

		if ( isCrit )
			Log.Info( $"[ChoppingTool] CRITICAL HIT! x{CritMultiplier} → {finalDamage} de dano!" );

		// Aplica dano à árvore
		tree.TakeDamage( finalDamage );
	}

	/// <summary>
	/// Desenha o range de corte no editor para facilitar ajuste visual.
	/// </summary>
	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Green.WithAlpha( 0.3f );
		Gizmo.Draw.LineSphere( Vector3.Zero, Range );
	}
}

using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Visual de debug do jogador — cria corpo (cápsula box) + machado na mão.
/// Adicionar no mesmo GameObject que ChoppingTool e PlayerResources.
/// O machado gira -30° ao atacar e volta suavemente em 0.2s.
/// </summary>
[Title( "Player Visual" )]
[Category( "Lumbercraft" )]
public sealed class PlayerVisual : Component
{
	// ── Configuração ──────────────────────────────────────────────────────────

	/// <summary>Cor do corpo do jogador</summary>
	[Property]
	public Color BodyColor { get; set; } = new Color( 0.2f, 0.4f, 0.9f ); // azul

	/// <summary>Cor do machado</summary>
	[Property]
	public Color AxeColor { get; set; } = new Color( 0.3f, 0.3f, 0.3f ); // cinza escuro

	/// <summary>Duração da animação de swing do machado (segundos)</summary>
	[Property, Range( 0.05f, 0.5f )]
	public float SwingDuration { get; set; } = 0.2f;

	// ── Estado interno ────────────────────────────────────────────────────────

	private GameObject _bodyGo;
	private GameObject _axeGo;
	private TimeSince  _timeSinceSwing;
	private bool       _swinging = false;

	// Rotação de descanso e de swing do machado (em graus, eixo X local)
	private const float AxeRestAngle  = 0f;
	private const float AxeSwingAngle = -30f;

	// ──────────────────────────────────────────────────────────────────────────
	//  SETUP
	// ──────────────────────────────────────────────────────────────────────────

	protected override void OnStart()
	{
		BuildBody();
		BuildAxe();
	}

	/// <summary>
	/// Cria a representação visual do corpo: caixa azul simulando uma cápsula.
	/// Escala: 50 x 50 unidades de largura, 100 de altura.
	/// </summary>
	private void BuildBody()
	{
		_bodyGo = new GameObject( true, "PlayerBody" );
		_bodyGo.SetParent( GameObject );
		// Centro da hitbox: sobe metade da altura para ficar sobre o chão
		_bodyGo.LocalPosition = new Vector3( 0f, 0f, 50f );
		_bodyGo.LocalScale    = new Vector3( 50f, 50f, 100f ); // Z = altura

		var renderer = _bodyGo.AddComponent<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint  = BodyColor;
	}

	/// <summary>
	/// Cria o machado: caixa cinza longa na mão direita do jogador.
	/// Posicionado à direita (+Y no s&box), na frente (+X) e na altura da mão (~Z40).
	/// </summary>
	private void BuildAxe()
	{
		_axeGo = new GameObject( true, "Axe" );
		_axeGo.SetParent( GameObject );

		// Offset: mão direita do jogador (Y negativo = direita em s&box Y-left)
		_axeGo.LocalPosition = new Vector3( 40f, -25f, 40f );
		_axeGo.LocalScale    = new Vector3( 15f, 10f, 60f ); // comprido no eixo Z

		var renderer = _axeGo.AddComponent<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint  = AxeColor;
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  UPDATE — animação de golpe
	// ──────────────────────────────────────────────────────────────────────────

	protected override void OnUpdate()
	{
		if ( !_axeGo.IsValid() ) return;

		// Detecta o input de ataque para iniciar o swing
		if ( Input.Pressed( "attack1" ) )
		{
			_swinging = true;
			_timeSinceSwing = 0;
		}

		AnimateAxe();
	}

	/// <summary>
	/// Interpola a rotação do machado entre o ângulo de swing (-30°) e o descanso (0°).
	/// Usa uma curva suave: rápido para baixo, lento voltando.
	/// </summary>
	private void AnimateAxe()
	{
		if ( !_swinging )
		{
			// Sem swing ativo — mantém posição de descanso
			_axeGo.LocalRotation = Rotation.FromAxis( Vector3.Right, AxeRestAngle );
			return;
		}

		float progress = _timeSinceSwing / SwingDuration; // 0 → 1

		if ( progress >= 1f )
		{
			// Animação completa — volta ao descanso
			_axeGo.LocalRotation = Rotation.FromAxis( Vector3.Right, AxeRestAngle );
			_swinging = false;
			return;
		}

		// Fase de ida (0→0.3): swing para -30°
		// Fase de volta (0.3→1): retorna suavemente a 0°
		float angle;
		if ( progress < 0.3f )
		{
			// Entrada rápida
			float t = progress / 0.3f;
			angle = MathX.Lerp( AxeRestAngle, AxeSwingAngle, MathX.EaseTo( t ) );
		}
		else
		{
			// Retorno lento
			float t = ( progress - 0.3f ) / 0.7f;
			angle = MathX.Lerp( AxeSwingAngle, AxeRestAngle, MathX.EaseTo( t ) );
		}

		_axeGo.LocalRotation = Rotation.FromAxis( Vector3.Right, angle );
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  CLEANUP
	// ──────────────────────────────────────────────────────────────────────────

	protected override void OnDestroy()
	{
		if ( _bodyGo.IsValid() ) _bodyGo.Destroy();
		if ( _axeGo.IsValid()  ) _axeGo.Destroy();
	}
}

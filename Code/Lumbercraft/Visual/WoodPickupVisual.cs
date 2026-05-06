using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Visual do WoodPickup. Adicionar no mesmo GameObject que WoodPickup.
/// Cria um cubo amarelo girando com leve bobbing vertical para chamar atenção.
/// </summary>
[Title( "Wood Pickup Visual" )]
[Category( "Lumbercraft" )]
public sealed class WoodPickupVisual : Component
{
	/// <summary>Tamanho do cubo visual (unidades)</summary>
	[Property, Range( 5f, 100f )]
	public float CubeSize { get; set; } = 30f;

	/// <summary>Velocidade de rotação em graus/segundo</summary>
	[Property, Range( 30f, 360f )]
	public float RotationSpeed { get; set; } = 90f;

	/// <summary>Amplitude do bobbing vertical (unidades)</summary>
	[Property, Range( 0f, 30f )]
	public float BobAmplitude { get; set; } = 6f;

	/// <summary>Frequência do bobbing (ciclos/segundo)</summary>
	[Property, Range( 0.5f, 5f )]
	public float BobFrequency { get; set; } = 2.5f;

	private GameObject _cubeGo;
	private Vector3    _baseLocalPosition;
	// Offset de fase para evitar que todos os pickups bobinam em sincronia
	private float      _phaseOffset;

	protected override void OnStart()
	{
		// Fase aleatória para dessincronizar pickups na cena
		_phaseOffset = Game.Random.Float( 0f, MathF.PI * 2f );

		// Cria o cubo amarelo como filho deste GameObject
		_cubeGo = new GameObject( true, "PickupVisual" );
		_cubeGo.SetParent( GameObject );
		_cubeGo.LocalScale    = Vector3.One * CubeSize;
		_cubeGo.LocalPosition = Vector3.Zero;
		_baseLocalPosition    = Vector3.Zero;

		var renderer = _cubeGo.AddComponent<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint  = new Color( 1f, 0.85f, 0.05f ); // amarelo dourado

		Log.Info( "[WoodPickupVisual] Visual criado." );
	}

	protected override void OnUpdate()
	{
		// Para se o visual foi destruído (pickup coletado)
		if ( !_cubeGo.IsValid() ) return;

		// ── Rotação contínua em torno do eixo Z (up) ──────────────────────────
		_cubeGo.LocalRotation = Rotation.FromAxis(
			Vector3.Up,
			Time.Now * RotationSpeed
		);

		// ── Bobbing senoidal suave para cima e para baixo ─────────────────────
		float bob = MathF.Sin( Time.Now * BobFrequency * MathF.PI * 2f + _phaseOffset ) * BobAmplitude;
		_cubeGo.LocalPosition = _baseLocalPosition + Vector3.Up * bob;
	}

	protected override void OnDestroy()
	{
		// Garante limpeza do visual se o pickup for destruído por outro código
		if ( _cubeGo.IsValid() )
			_cubeGo.Destroy();
	}
}

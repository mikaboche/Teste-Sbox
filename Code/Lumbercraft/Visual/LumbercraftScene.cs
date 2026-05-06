using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Spawner central do Lumbercraft MVP.
/// Coloque em um GameObject vazio na cena — ao rodar, gera chão, luzes,
/// 10 árvores e o hub com bancadas de upgrade, tudo via código sem assets externos.
/// </summary>
[Title( "Lumbercraft Scene Builder" )]
[Category( "Lumbercraft" )]
public sealed class LumbercraftScene : Component
{
	[Property, Range( 1, 30 )]
	public int TreeCount { get; set; } = 10;

	[Property, Range( 100f, 1200f )]
	public float ForestRadius { get; set; } = 800f;

	protected override void OnStart()
	{
		BuildScene();
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  ORQUESTRAÇÃO PRINCIPAL
	// ══════════════════════════════════════════════════════════════════════════

	private void BuildScene()
	{
		Log.Info( "[LumbercraftScene] Construindo cena..." );
		SpawnLights();
		SpawnGround();
		SpawnTrees();
		SpawnHub();
		Log.Info( "[LumbercraftScene] Cena pronta! ✓" );
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  ILUMINAÇÃO
	// ══════════════════════════════════════════════════════════════════════════

	private void SpawnLights()
	{
		// Sol direcional — ângulo de 45° simulando fim de tarde
		var sunGo = new GameObject( true, "Sun" );
		sunGo.SetParent( Scene );
		sunGo.WorldRotation = Rotation.From( 45f, 45f, 0f );
		var sun = sunGo.AddComponent<DirectionalLight>();
		sun.LightColor = new Color( 1f, 0.92f, 0.75f ) * 2.5f; // amarelo suave
		sun.Shadows = true;

		// Luz ambiente com tom de floresta (verde escuro)
		var ambGo = new GameObject( true, "AmbientLight" );
		ambGo.SetParent( Scene );
		var amb = ambGo.AddComponent<AmbientLight>();
		amb.Color = new Color( 0.04f, 0.12f, 0.04f ); // verde muito escuro
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  CHÃO
	// ══════════════════════════════════════════════════════════════════════════

	private void SpawnGround()
	{
		// Cubo achatado enorme como chão — cinza escuro
		// Escala: 2000 x 2000 unidades de superfície, 10 de altura
		var ground = CreateBox(
			name:     "Ground",
			color:    new Color( 0.28f, 0.28f, 0.28f ),
			position: new Vector3( 0f, 0f, -5f ),       // ligeiramente abaixo da origem Z=0
			scale:    new Vector3( 2000f, 2000f, 10f )
		);

		// Collider estático para o jogador andar
		ground.AddComponent<BoxCollider>();
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  ÁRVORES
	// ══════════════════════════════════════════════════════════════════════════

	private void SpawnTrees()
	{
		for ( int i = 0; i < TreeCount; i++ )
		{
			// Posição aleatória em anel ao redor do centro (evita spawnar no hub)
			float angle = Game.Random.Float( 0f, 360f );
			float dist  = Game.Random.Float( ForestRadius * 0.3f, ForestRadius );
			float rad   = angle * MathF.PI / 180f;

			var pos = new Vector3(
				MathF.Cos( rad ) * dist,
				MathF.Sin( rad ) * dist,
				0f
			);

			// Escala aleatória para variedade visual
			float scale = Game.Random.Float( 0.8f, 1.4f );

			SpawnTree( pos, scale );
		}
	}

	/// <summary>
	/// Cria uma árvore completa com tronco (cilindro) + copa (esfera),
	/// TreeComponent, TreeVisual e física de queda.
	/// </summary>
	private void SpawnTree( Vector3 position, float scale )
	{
		// Raiz da árvore — contém todos os componentes
		var treeRoot = new GameObject( true, "Tree" );
		treeRoot.SetParent( Scene );
		treeRoot.WorldPosition = position;
		treeRoot.LocalScale = Vector3.One * scale; // escala uniforme aleatória

		// ── Tronco ────────────────────────────────────────────────────────────
		// Cilindro verde escuro — estreito em X/Y, alto em Z (eixo up do s&box)
		var trunk = new GameObject( true, "Trunk" );
		trunk.SetParent( treeRoot );
		trunk.LocalScale = new Vector3( 30f, 30f, 200f ); // ≈ 37cm wide, 2.5m tall
		var trunkR = trunk.AddComponent<ModelRenderer>();
		trunkR.Model = Model.Load( "models/dev/box.vmdl" );      // fallback box se cylinder não existir
		trunkR.Tint  = new Color( 0.35f, 0.20f, 0.07f );         // marrom madeira

		// ── Copa ──────────────────────────────────────────────────────────────
		// Esfera verde no topo do tronco
		var crown = new GameObject( true, "Crown" );
		crown.SetParent( treeRoot );
		crown.LocalPosition = new Vector3( 0f, 0f, 190f ); // Z-up: topo do tronco
		crown.LocalScale    = Vector3.One * 160f;           // ≈ 2m de diâmetro
		var crownR = crown.AddComponent<ModelRenderer>();
		crownR.Model = Model.Load( "models/dev/box.vmdl" );  // fallback; idealmente sphere
		crownR.Tint  = new Color( 0.12f, 0.55f, 0.12f );    // verde floresta

		// ── Física (desabilitada — TreeComponent ativa ao morrer) ─────────────
		var col = treeRoot.AddComponent<BoxCollider>();
		col.Scale = new Vector3( 30f, 30f, 200f ); // mesmo volume do tronco

		var rb = treeRoot.AddComponent<Rigidbody>();
		rb.Enabled = false; // TreeComponent.Die() vai habilitar

		// ── Gameplay + Visual ─────────────────────────────────────────────────
		var tree = treeRoot.AddComponent<TreeComponent>();
		tree.MaxHP    = 500f;
		tree.WoodDrop = 30f;
		// WoodPickupPrefab = null → TreeComponent usa fallback por código

		treeRoot.AddComponent<TreeVisual>();
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  HUB CENTRAL
	// ══════════════════════════════════════════════════════════════════════════

	private void SpawnHub()
	{
		// 3 bancadas de upgrade em semicírculo à frente
		float[] angles = { -60f, 0f, 60f };
		var stationColor = new Color( 0.45f, 0.27f, 0.10f ); // marrom madeira

		for ( int i = 0; i < 3; i++ )
		{
			float rad = angles[i] * MathF.PI / 180f;
			var pos = new Vector3(
				MathF.Cos( rad ) * 220f,
				MathF.Sin( rad ) * 220f,
				40f  // elevar meia-altura (80/2) acima do chão
			);

			var stationGo = CreateBox( $"UpgradeStation_{i}", stationColor, pos, Vector3.One * 80f );
			stationGo.AddComponent<BoxCollider>();

			var upgrade = stationGo.AddComponent<UpgradeStation>();
			upgrade.DamageUpgradeCost    = 50f;
			upgrade.DamageUpgradeAmount  = 100f;
			upgrade.InteractRadius       = 160f;
		}

		// Portal decorativo — cubo alto azul brilhante (placeholder de teleporte)
		CreateBox(
			name:     "Portal",
			color:    new Color( 0.15f, 0.45f, 1.0f ),  // azul brilhante
			position: new Vector3( 0f, -350f, 100f ),
			scale:    new Vector3( 100f, 20f, 200f )
		);
	}

	// ══════════════════════════════════════════════════════════════════════════
	//  HELPER FACTORY
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Cria um GameObject com ModelRenderer usando box.vmdl e a cor especificada.
	/// Retorna o GameObject para quem quiser adicionar mais componentes.
	/// </summary>
	private GameObject CreateBox( string name, Color color, Vector3 position, Vector3 scale )
	{
		var go = new GameObject( true, name );
		go.SetParent( Scene );
		go.WorldPosition = position;
		go.LocalScale    = scale;

		var renderer = go.AddComponent<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint  = color;

		return go;
	}
}

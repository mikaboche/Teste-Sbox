using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Spawna o jogador ao iniciar a cena, configurando todos os componentes
/// necessários para o loop do Lumbercraft.
///
/// Como usar: adicionar num GameObject vazio (junto com LumbercraftScene).
/// O spawn acontece automaticamente no OnStart.
/// </summary>
[Title( "Player Spawner" )]
[Category( "Lumbercraft" )]
public sealed class PlayerSpawner : Component
{
	/// <summary>
	/// Posição de spawn. Z=200 garante que o jogador cai suavemente sobre o chão.
	/// (Chão está em Z≈0; gravidade = -800 u/s²)
	/// </summary>
	[Property]
	public Vector3 SpawnPosition { get; set; } = new Vector3( 0f, 0f, 200f );

	protected override void OnStart()
	{
		DisableStaticCamera();
		SpawnPlayer();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  CÂMERA ESTÁTICA
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Desabilita a câmera estática que vem na cena padrão (minimal.scene).
	/// O PlayerController cria e gerencia a própria câmera com prioridade maior.
	/// </summary>
	private void DisableStaticCamera()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( cam.IsValid() )
			{
				cam.Enabled = false;
				Log.Info( $"[PlayerSpawner] Câmera estática '{cam.GameObject.Name}' desabilitada." );
			}
		}
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  PLAYER
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Cria o jogador com:
	/// - PlayerController (movimento FPS + câmera + física — built-in s&box)
	/// - PlayerResources (madeira coletada)
	/// - ChoppingTool (raycast + dano nas árvores)
	/// - PlayerVisual (corpo azul + machado cinza)
	/// </summary>
	private void SpawnPlayer()
	{
		var playerGo = new GameObject( true, "Player" );
		playerGo.SetParent( Scene );
		playerGo.WorldPosition = SpawnPosition;

		// PlayerController é sealed e self-contained:
		// gerencia Rigidbody interno, câmera FPS/TPS e input de movimento
		playerGo.AddComponent<PlayerController>();

		// Componentes do Lumbercraft
		playerGo.AddComponent<PlayerResources>();
		playerGo.AddComponent<ChoppingTool>();
		playerGo.AddComponent<PlayerVisual>();

		Log.Info( $"[PlayerSpawner] ✅ Jogador spawnado em {SpawnPosition}" );

		// Spawna o HUD junto com o jogador
		SpawnHUD();
	}

	// ──────────────────────────────────────────────────────────────────────────
	//  HUD
	// ──────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Cria o HUD de madeira (WoodCounter Razor) num GameObject separado com ScreenPanel.
	/// </summary>
	private void SpawnHUD()
	{
		var hudGo = new GameObject( true, "HUD" );
		hudGo.SetParent( Scene );

		// ScreenPanel é obrigatório para PanelComponents funcionarem
		hudGo.AddComponent<ScreenPanel>();

		// WoodCounter é nosso PanelComponent Razor — mostra "🪵 Wood: X"
		hudGo.AddComponent<WoodCounter>();

		Log.Info( "[PlayerSpawner] HUD criado." );
	}
}

using Sandbox;

namespace Lumbercraft;

/// <summary>
/// Componente no jogador que armazena e gerencia os recursos coletados.
/// Deve ser adicionado ao GameObject do jogador.
/// </summary>
[Title( "Player Resources" )]
[Category( "Lumbercraft" )]
public sealed class PlayerResources : Component
{
	/// <summary>Quantidade atual de madeira do jogador</summary>
	[Property]
	public float Wood { get; private set; } = 0f;

	protected override void OnStart()
	{
		Log.Info( "[PlayerResources] Sistema de recursos iniciado. Wood: 0" );
	}

	/// <summary>
	/// Adiciona madeira ao inventário do jogador.
	/// </summary>
	public void AddWood( float amount )
	{
		if ( amount <= 0f ) return;

		Wood += amount;
		Log.Info( $"[PlayerResources] +{amount} madeira adicionada. Total: {Wood}" );
	}

	/// <summary>
	/// Tenta gastar madeira. Retorna true se havia recursos suficientes.
	/// </summary>
	public bool SpendWood( float amount )
	{
		if ( amount <= 0f ) return true;

		if ( Wood < amount )
		{
			Log.Info( $"[PlayerResources] Madeira insuficiente! Necessário: {amount} | Disponível: {Wood}" );
			return false;
		}

		Wood -= amount;
		Log.Info( $"[PlayerResources] -{amount} madeira gasta. Total restante: {Wood}" );
		return true;
	}
}

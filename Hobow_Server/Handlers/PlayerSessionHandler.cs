using System;
using System.Threading.Tasks;
using Hobow_Server.Models;
using Microsoft.AspNetCore.SignalR;
using Hobow_Server.Hubs;

namespace Hobow_Server.Handlers
{
	public interface IPlayerSessionHandler
	{
		Task HandleLogoutAsync(int heroId, string actorId);
	}

	public class PlayerSessionHandler : IPlayerSessionHandler
	{
		private readonly GameState _gameState;
		private readonly IHubContext<HeroHub> _hubContext;

		public PlayerSessionHandler(GameState gameState, IHubContext<HeroHub> hubContext)
		{
			_gameState = gameState;
			_hubContext = hubContext;
		}

		public async Task HandleLogoutAsync(int heroId, string actorId)
		{
			_gameState.RemoveHero(heroId);
			Console.WriteLine($"[PlayerSessionHandler] {actorId} removed hero {heroId}");
			await _hubContext.Clients.All.SendAsync("PlayerLoggedOut", heroId);
		}
	}
}

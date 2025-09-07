using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Hobow_Server.Handlers;
using Hobow_Server.Models;

namespace Hobow_Server.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class EnemyController : ControllerBase
{
    private readonly IEnemyHandler _enemyHandler;
    private readonly GameState _gameState;

    public EnemyController(IEnemyHandler enemyHandler, GameState gameState)
    {
        _enemyHandler = enemyHandler;
        _gameState = gameState;
    }
}

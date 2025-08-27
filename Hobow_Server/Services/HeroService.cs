namespace Hobow_Server.Services;

public interface IHeroService
{
    void DoSomething();
}

public class HeroService : IHeroService
{
    public void DoSomething()
    {
        Console.WriteLine("hey!");
    }
}

public class MockHeroService : IHeroService
{
    public void DoSomething()
    {
        Console.WriteLine("hey from MockPlayerService!");
    }
}
namespace RefactorScore.Domain.SeedWork;

public abstract class Entity
{
    public string Id { get; protected set; } = Guid.NewGuid().ToString();
    
    protected Entity() {}
    
    protected Entity(string id) => Id = id;
}
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Marker interface for composite skills.
/// </summary>
public interface ICompositeSkill
{
    List<string> ComponentSkills { get; }
}
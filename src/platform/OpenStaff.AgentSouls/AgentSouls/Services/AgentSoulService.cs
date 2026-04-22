using OpenStaff;

namespace OpenStaff.AgentSouls.Services;

public interface IAgentSoulService
{
    IAgentSoulHttpService CommunicationStyles { get; }
    IAgentSoulHttpService PersonalityTraits { get; }
    IAgentSoulHttpService WorkAttitudes { get; }
}

internal class AgentSoulService : ServiceBase, IAgentSoulService
{
    // Personality traits(personality_traits.json)
    // Work attitudes(work_attitudes.json)
    // Communication styles(communication_styles.json)
    public IAgentSoulHttpService PersonalityTraits => GetRequiredService<PersonalityTraitsHttpService>();
    public IAgentSoulHttpService WorkAttitudes => GetRequiredService<WorkAttitudesHttpService>();
    public IAgentSoulHttpService CommunicationStyles => GetRequiredService<CommunicationStylesHttpService>();

    public AgentSoulService(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }
}

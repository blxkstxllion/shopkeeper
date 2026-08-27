namespace ShopKeeper.Application.Advisor.Queries;

using MediatR;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Common.Interfaces;

/// <summary>No plan-gating - this is just a UI feature flag (whether to show the free-text input
/// at all), safe for any authenticated user to read regardless of their plan tier.</summary>
public record GetAdvisorCapabilitiesQuery : IRequest<AdvisorCapabilitiesDto>;

public class GetAdvisorCapabilitiesQueryHandler(IAdvisorConversationClient claude)
    : IRequestHandler<GetAdvisorCapabilitiesQuery, AdvisorCapabilitiesDto>
{
    public Task<AdvisorCapabilitiesDto> Handle(GetAdvisorCapabilitiesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new AdvisorCapabilitiesDto(claude.IsConfigured));
}

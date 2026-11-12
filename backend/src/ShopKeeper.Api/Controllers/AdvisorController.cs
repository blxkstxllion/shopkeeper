namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShopKeeper.Application.Advisor;
using ShopKeeper.Application.Advisor.Commands;
using ShopKeeper.Application.Advisor.Dtos;
using ShopKeeper.Application.Advisor.Queries;

[Authorize]
[ApiController]
[Route("api/advisor")]
public class AdvisorController(ISender mediator) : ControllerBase
{
    [HttpGet("questions")]
    public async Task<ActionResult<IReadOnlyList<AdvisorQuestionDto>>> GetQuestions(CancellationToken ct) =>
        Ok(await mediator.Send(new GetAdvisorQuestionsQuery(), ct));

    [HttpGet("answer")]
    public async Task<ActionResult<AdvisorAnswerDto>> GetAnswer(
        [FromQuery] AdvisorQuestionId questionId, [FromQuery] Guid? branchId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAdvisorAnswerQuery(questionId, branchId), ct));

    [HttpGet("capabilities")]
    public async Task<ActionResult<AdvisorCapabilitiesDto>> GetCapabilities(CancellationToken ct) =>
        Ok(await mediator.Send(new GetAdvisorCapabilitiesQuery(), ct));

    // Calls the real Anthropic API when configured (see ClaudeAdvisorConversationClient) -
    // every request here has a real dollar cost, unlike the rest of this controller.
    [EnableRateLimiting("expensive")]
    [HttpPost("ask")]
    public async Task<ActionResult<AdvisorAnswerDto>> Ask([FromBody] AskAdvisorCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));
}

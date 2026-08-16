namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Advisor;
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
}

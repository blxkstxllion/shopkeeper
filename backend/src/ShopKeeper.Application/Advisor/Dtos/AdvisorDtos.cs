namespace ShopKeeper.Application.Advisor.Dtos;

using ShopKeeper.Application.Advisor;

public record AdvisorQuestionDto(AdvisorQuestionId Id, string Label);

public record AdvisorAnswerDto(string Answer, DateTimeOffset GeneratedAt);

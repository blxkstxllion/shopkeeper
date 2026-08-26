namespace ShopKeeper.Api.Tests.Onboarding;

using System.Text.Json;
using System.Text.Json.Serialization;
using ShopKeeper.Api.Controllers;

/// <summary>
/// Regression test for a real bug found during live testing: OnboardingController's own
/// CompleteOnboardingRequest DTO didn't declare ColorTheme, so ASP.NET's System.Text.Json model
/// binder silently dropped it from the request body before CompleteOnboardingCommand ever saw
/// it - every handler-level test (which calls CompleteOnboardingCommandHandler directly) was
/// blind to this, since it bypasses the controller/DTO/model-binding layer entirely. Deserializing
/// the exact JSON shape the frontend sends, through the same System.Text.Json path ASP.NET's
/// [FromBody] binding uses, is what actually exercises this boundary.
/// </summary>
public class OnboardingControllerRequestBindingTests
{
    // Mirrors Program.cs's real controller JSON config (AddJsonOptions -> JsonStringEnumConverter)
    // so this deserializes exactly the way ASP.NET's [FromBody] model binder actually does.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void CompleteOnboardingRequest_DeserializesColorThemeFromRequestBody()
    {
        const string json = """
            {"businessName":"Ama's Pharmacy","businessType":"Pharmacy","country":"Ghana","currencyCode":"GHS",
             "logoUrl":null,"taxEnabled":false,"taxRatePercent":0,"taxInclusivePricing":true,
             "goals":["IncreaseProfit"],"firstBranchName":"Main Store","firstBranchAddress":null,
             "firstBranchCity":null,"colorTheme":"red"}
            """;

        var request = JsonSerializer.Deserialize<OnboardingController.CompleteOnboardingRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("red", request!.ColorTheme);
    }

    [Fact]
    public void CompleteOnboardingRequest_DefaultsColorThemeToGreenWhenOmittedFromRequestBody()
    {
        const string json = """
            {"businessName":"Ama's Shop","businessType":"Retail","country":"Ghana","currencyCode":"GHS",
             "logoUrl":null,"taxEnabled":false,"taxRatePercent":0,"taxInclusivePricing":true,
             "goals":["IncreaseProfit"],"firstBranchName":"Main Store","firstBranchAddress":null,
             "firstBranchCity":null}
            """;

        var request = JsonSerializer.Deserialize<OnboardingController.CompleteOnboardingRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal("green", request!.ColorTheme);
    }
}

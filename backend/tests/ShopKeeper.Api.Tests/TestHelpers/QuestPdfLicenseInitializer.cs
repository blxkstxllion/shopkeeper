namespace ShopKeeper.Api.Tests.TestHelpers;

using System.Runtime.CompilerServices;

/// <summary>Sets QuestPDF's required one-time license declaration for the test process - normally
/// done once in Program.cs at app startup, which tests never run through.</summary>
internal static class QuestPdfLicenseInitializer
{
    [ModuleInitializer]
    public static void Initialize() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
}

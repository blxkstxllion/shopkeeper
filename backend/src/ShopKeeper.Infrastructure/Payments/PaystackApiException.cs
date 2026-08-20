namespace ShopKeeper.Infrastructure.Payments;

/// <summary>Thrown when Paystack returns a non-success response. Deliberately not added to
/// ExceptionHandlingMiddleware's switch - falls into the existing generic 500 branch (logged
/// server-side with full detail, generic message to the client), consistent with how the app
/// already treats unexpected provider failures.</summary>
public class PaystackApiException(string message) : Exception(message);

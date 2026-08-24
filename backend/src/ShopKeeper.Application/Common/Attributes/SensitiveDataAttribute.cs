namespace ShopKeeper.Application.Common.Attributes;

/// <summary>Marks a command property carrying customer/supplier personal data that
/// AuditLoggingBehavior redacts even though the property name alone isn't a universally
/// unambiguous PII signal the way "password"/"token"/"secret"/"hash" are - e.g.
/// CreateCustomerCommand.Name is a person's name, CreateProductCommand.Name is a product
/// name, so redaction here is opted into per-property rather than matched by name.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute;

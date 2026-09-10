using InsuranceAgent.DomainTypes;

namespace InsuranceAgent.Contracts;

/// <summary>Immutable progress snapshot emitted by the insurance conversation.</summary>
public sealed record InsuranceProgressNotification(
    string Stage,
    int Progress,
    string Message,
    string? NextStep = null,
    QualifiedCarriers? Payload = null);

/// <summary>Immutable request for the host to display the customer console.</summary>
public sealed record InsuranceCustomerConsoleNotification(string Message);

/// <summary>Immutable navigation request raised by an insurance topic.</summary>
public sealed record InsuranceNavigationNotification(string Url);

/// <summary>Immutable qualification completion snapshot.</summary>
public sealed record InsuranceQualificationNotification(
    string Stage,
    int Progress,
    string Message,
    QualifiedCarriers? Payload = null);

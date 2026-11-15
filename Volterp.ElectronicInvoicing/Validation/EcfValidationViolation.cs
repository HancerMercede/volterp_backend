namespace Volterp.ElectronicInvoicing.Validation;

/// <summary>
/// A breach of the rules of a type of document or of the arithmetic of the
/// document.
/// </summary>
/// <param name="Rule">The rule that was broken.</param>
/// <param name="Message">What the document does instead of meeting it.</param>
public sealed record EcfValidationViolation(string Rule, string Message);

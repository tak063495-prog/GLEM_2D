namespace GLEM.Core.Validation;

public sealed record ValidationIssue(string Code, string FieldName, bool IsWarning, string Message)
{
    public static ValidationIssue Error(string code, string field, string message) => new(code, field, false, message);

    public static ValidationIssue Warning(string code, string field, string message) => new(code, field, true, message);
}

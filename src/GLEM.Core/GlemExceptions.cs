namespace GLEM.Core;

public class GlemException : Exception
{
    public string Code { get; }

    protected GlemException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public sealed class InputValidationException : GlemException
{
    public string FieldName { get; }

    public InputValidationException(string code, string fieldName, string message)
        : base(code, message)
    {
        FieldName = fieldName;
    }
}

public sealed class EngineException : GlemException
{
    public EngineException(string code, string message) : base(code, message)
    {
    }
}

public sealed class ProjectFileException : GlemException
{
    public ProjectFileException(string code, string message) : base(code, message)
    {
    }
}

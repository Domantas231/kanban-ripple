namespace Kanban.Api.Exceptions;

public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string message)
        : this(message, "CONFLICT")
    {
    }

    public ConflictException(string message, string code)
        : base(message)
    {
        Code = code;
    }
}

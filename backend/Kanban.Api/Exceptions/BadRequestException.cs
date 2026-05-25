namespace Kanban.Api.Exceptions;

public sealed class BadRequestException : Exception
{
    public string? Code { get; }

    public BadRequestException(string message) : base(message)
    {
    }

    public BadRequestException(string message, string code) : base(message)
    {
        Code = code;
    }
}

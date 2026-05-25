namespace Kanban.Api.Services.Auth;

public interface IAccessTokenBlocklist
{
    void Block(string accessToken);
    bool IsBlocked(string accessToken);
}

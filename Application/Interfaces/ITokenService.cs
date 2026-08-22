namespace GamePrice.Api.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(Guid userId, string email, string name);
    }
}

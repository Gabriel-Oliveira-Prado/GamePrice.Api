using GamePrice.Api.Domain.Models;

namespace GamePrice.Api.Application.Interfaces
{
    public interface IUserRepository
    {
        UserModel? Authenticate(string email, string password);
        bool Register(string name, string email, string password);
        bool EmailExists(string email);
    }
}

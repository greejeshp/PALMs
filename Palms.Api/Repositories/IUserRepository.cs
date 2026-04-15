using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByIdAsync(int id);
        Task UpdateLastLoginAsync(int userId, string ipAddress);
        Task IncrementFailedLoginAsync(int userId);
        Task ResetFailedLoginAsync(int userId);
    }
}

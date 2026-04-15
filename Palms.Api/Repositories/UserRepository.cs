using Dapper;
using Palms.Api.Data;
using Palms.Api.Models.Entities;

namespace Palms.Api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var connection = _connectionFactory.CreateConnection();
            string query = "SELECT * FROM Users WHERE Username = @Username";
            return await connection.QuerySingleOrDefaultAsync<User>(query, new { Username = username });
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            using var connection = _connectionFactory.CreateConnection();
            string query = "SELECT * FROM Users WHERE Id = @Id";
            return await connection.QuerySingleOrDefaultAsync<User>(query, new { Id = id });
        }

        public async Task UpdateLastLoginAsync(int userId, string ipAddress)
        {
            using var connection = _connectionFactory.CreateConnection();
            string query = @"
                UPDATE Users 
                SET LastLoginAt = GETUTCDATE(), LastLoginIp = @IpAddress, FailedLoginCount = 0 
                WHERE Id = @Id";
            await connection.ExecuteAsync(query, new { Id = userId, IpAddress = ipAddress });
        }

        public async Task IncrementFailedLoginAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            string query = @"
                UPDATE Users 
                SET FailedLoginCount = FailedLoginCount + 1, 
                    IsLocked = CASE WHEN FailedLoginCount >= 4 THEN 1 ELSE 0 END
                WHERE Id = @Id";
            await connection.ExecuteAsync(query, new { Id = userId });
        }

        public async Task ResetFailedLoginAsync(int userId)
        {
            using var connection = _connectionFactory.CreateConnection();
            string query = "UPDATE Users SET FailedLoginCount = 0, IsLocked = 0 WHERE Id = @Id";
            await connection.ExecuteAsync(query, new { Id = userId });
        }
    }
}

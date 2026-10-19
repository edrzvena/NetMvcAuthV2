using NetMvcAuthV2.Models;

namespace NetMvcAuthV2.Repositories.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByResetTokenHashAsync(string tokenHash);
    Task<bool> EmailExistsAsync(string email);
}

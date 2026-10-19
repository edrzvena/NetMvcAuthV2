using Microsoft.EntityFrameworkCore;
using NetMvcAuthV2.Data;
using NetMvcAuthV2.Models;
using NetMvcAuthV2.Repositories.Interfaces;

namespace NetMvcAuthV2.Repositories.Implementations;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email) =>
        Set.FirstOrDefaultAsync(u => u.Email == email);

    public Task<User?> GetByResetTokenHashAsync(string tokenHash) =>
        Set.FirstOrDefaultAsync(u => u.ResetPasswordTokenHash == tokenHash);

    public Task<bool> EmailExistsAsync(string email) =>
        Set.AnyAsync(u => u.Email == email);
}

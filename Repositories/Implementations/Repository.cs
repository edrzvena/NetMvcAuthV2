using Microsoft.EntityFrameworkCore;
using NetMvcAuthV2.Data;
using NetMvcAuthV2.Repositories.Interfaces;

namespace NetMvcAuthV2.Repositories.Implementations;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected readonly AppDbContext Context = context;
    protected DbSet<T> Set => Context.Set<T>();

    public async Task<T?> GetByIdAsync(int id) => await Set.FindAsync(id);

    public async Task<List<T>> GetAllAsync() => await Set.ToListAsync();

    public async Task AddAsync(T entity) => await Set.AddAsync(entity);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    public async Task SaveChangesAsync() => await Context.SaveChangesAsync();
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetMvcAuthV2.Common.Settings;
using NetMvcAuthV2.Data;
using NetMvcAuthV2.Models;
using NetMvcAuthV2.Repositories.Implementations;
using NetMvcAuthV2.Repositories.Interfaces;
using NetMvcAuthV2.Services.Implementations;
using NetMvcAuthV2.Services.Interfaces;

var builder =WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
if (builder.Environment.IsDevelopment())
    builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

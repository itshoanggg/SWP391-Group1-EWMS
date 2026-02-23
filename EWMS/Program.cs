using EWMS.Models;
using EWMS.Repositories;
using EWMS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace EWMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllersWithViews();

            // 1. Register Authentication & Authorization Services
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                });

            builder.Services.AddAuthorization();

            // Database Context
            builder.Services.AddDbContext<EWMSDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DBContext")));

            // Register Repositories
            builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
            builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
            builder.Services.AddScoped<IProductRepository, ProductRepository>();

            // Register Services
            builder.Services.AddScoped<IInventoryCheckService, InventoryCheckService>();
            builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
            builder.Services.AddScoped<IUserService, UserService>();
            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // 2. Middleware Order: Authentication must come BEFORE Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Public}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
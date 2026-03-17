using EWMS.Models;
using EWMS.Repositories;
using EWMS.Repositories.Interfaces;
using EWMS.Services;
using EWMS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Identity;
using EWMS.Hubs;
using EWMS.BackgroundServices;

namespace EWMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container and require authentication globally.
            builder.Services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            });

            // Add HttpContextAccessor for reading claims in services
            builder.Services.AddHttpContextAccessor();

            // Register Password Hasher for User model
            builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

            // Cookie authentication
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                });

            // Configure DbContext (use EWMSDbContext)
            builder.Services.AddDbContext<EWMSDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DBContext")
                ));

            // Register Master Repositories/Services (Sales/StockOut/InventoryCheck)
            builder.Services.AddScoped<EWMS.Repositories.IInventoryRepository, InventoryRepository>();
            builder.Services.AddScoped<EWMS.Repositories.Interfaces.IInventoryRepository, InventoryRepository>();
            builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
            builder.Services.AddScoped<EWMS.Repositories.IProductRepository, ProductRepository>();
            builder.Services.AddScoped<EWMS.Repositories.Interfaces.IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IStockOutReceiptRepository, StockOutReceiptRepository>();
            builder.Services.AddScoped<EWMS.Repositories.ILocationRepository, LocationRepository>();
            builder.Services.AddScoped<EWMS.Repositories.Interfaces.ILocationRepository, LocationRepository>();
            builder.Services.AddScoped<EWMS.Repositories.Interfaces.IWarehouseRepository, WarehouseRepository>();
            builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
            builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
            builder.Services.AddScoped<IInventoryCheckService, InventoryCheckService>();
            builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
            builder.Services.AddScoped<IStockOutReceiptService, StockOutReceiptService>();
            builder.Services.AddScoped<IWarehouseService, WarehouseService>();

            // Register Unit of Work and legacy-style services that depend on it
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
            builder.Services.AddScoped<IStockInService, StockInService>();
            builder.Services.AddScoped<IStockService, StockService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<ISupplierService, SupplierService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();

            // Register TransferService so TransferController can be resolved
            builder.Services.AddScoped<TransferService>();

            // RabbitMQ Service - Singleton vì dùng chung connection
            builder.Services.AddSingleton<IRabbitMQService, RabbitMQService>();

            // SignalR
            builder.Services.AddSignalR();

            // Background Worker để xử lý Sales Order từ RabbitMQ Queue
            builder.Services.AddHostedService<SalesOrderWorker>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // IMPORTANT: enable authentication before authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // Map SignalR Hub
            app.MapHub<SalesOrderHub>("/salesOrderHub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}

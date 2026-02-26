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
            builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
            builder.Services.AddScoped<EWMS.Repositories.IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IStockOutReceiptRepository, StockOutReceiptRepository>();
            builder.Services.AddScoped<EWMS.Repositories.ILocationRepository, LocationRepository>();
            builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
            builder.Services.AddScoped<IInventoryCheckService, InventoryCheckService>();
            builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
            builder.Services.AddScoped<IStockOutReceiptService, StockOutReceiptService>();

            // Register Unit of Work and legacy-style services that depend on it
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
            builder.Services.AddScoped<IStockInService, StockInService>();
            builder.Services.AddScoped<IStockService, StockService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<ISupplierService, SupplierService>();
            builder.Services.AddScoped<IUserService, UserService>();

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

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}

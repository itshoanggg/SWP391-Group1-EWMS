using EWMS.Models;
using EWMS.Repositories;
using EWMS.Repositories.Interfaces;
using EWMS.Services;
using EWMS.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EWMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

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

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=StockIn}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

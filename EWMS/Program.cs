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

            // Configure DbContext
            builder.Services.AddDbContext<EWMSContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DBContext")
                ));

            // Register Unit of Work
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Register Services
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
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

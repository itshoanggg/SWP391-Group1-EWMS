using System;
using System.Threading.Tasks;
using EWMS.ViewModels;

namespace EWMS.Services.Interfaces
{
    public interface IInventoryReportService
    {
        Task<NXTReportViewModel> GetNXTReportAsync(int warehouseId, DateTime? from, DateTime? to);
    }
}

using EWMS.Models;
using EWMS.Repositories.Interfaces;
using EWMS.Services.Interfaces;

namespace EWMS.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SupplierService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
        {
            return await _unitOfWork.Suppliers.GetAllOrderedByNameAsync();
        }

        public async Task<Supplier?> GetSupplierByIdAsync(int id)
        {
            return await _unitOfWork.Suppliers.GetByIdAsync(id);
        }
    }
}

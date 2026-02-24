using EWMS.Models;
using EWMS.Repositories.Interfaces;

namespace EWMS.Repositories
{
    public class WarehouseRepository : GenericRepository<Warehouse>, IWarehouseRepository
    {
        public WarehouseRepository(EWMSContext context) : base(context)
        {
        }
    }
}

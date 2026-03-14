using Microsoft.AspNetCore.SignalR;

namespace EWMS.Hubs
{
    public class SalesOrderHub : Hub
    {
        // Phương thức để client join vào group theo UserId
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }

        // Phương thức để client leave group
        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
    }
}
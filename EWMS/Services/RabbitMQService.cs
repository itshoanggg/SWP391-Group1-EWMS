using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace EWMS.Services
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMQService(IConfiguration configuration)
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:UserName"] ?? "admin",
                Password = configuration["RabbitMQ:Password"] ?? "admin123"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare queue (idempotent - nếu đã tồn tại thì bỏ qua)
            _channel.QueueDeclare(
                queue: "sales-order-queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
        }

        public void PublishMessage<T>(string queueName, T message)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true; // Message sẽ được lưu vào disk

            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: body
            );
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}

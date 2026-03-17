namespace EWMS.Services
{
    public interface IRabbitMQService
    {
        void PublishMessage<T>(string queueName, T message);
    }
}

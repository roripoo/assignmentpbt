using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Chat
{
    private static string msg_queue;
    
    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run <username> <tcp_port>"); //usage to connect to rabbitmq
            return;
        }

        string username = args[0];
        string tcpPort = args[1];
        msg_queue = $"{username}_queue"; //queue for the specified user so we can identify messages

        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            UserName = "guest", //writing guest/guest so there are no authentication issues
            Password = "guest"
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.ExchangeDeclare(exchange: "room", type: "topic");

            channel.QueueDeclare(queue: msg_queue,
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.QueueBind(queue: msg_queue,
                              exchange: "room",
                              routingKey: "user.*"); //bind the queue to room

            Console.WriteLine("Enter your messages. Type 'exit' to quit."); //gives instructions for the user

            var sendMessagesTask = SendMessages(channel, username);
            var receiveMessagesTask = ReceiveMessages(channel);

            await Task.WhenAny(sendMessagesTask, receiveMessagesTask);
        }
    }

    private static async Task SendMessages(IModel channel, string username)
    {
        string message;
        while ((message = Console.ReadLine()) != "exit") //as long as the user hasnt typed exit, do the following
        {
            var messageBytes = Encoding.UTF8.GetBytes($"[{username}]: {message}");
            channel.BasicPublish(exchange: "room",
                                 routingKey: "user.*",
                                 basicProperties: null,
                                 body: messageBytes);
        }

        Console.WriteLine("Exiting chat.");
    }

    private static async Task ReceiveMessages(IModel channel) //receiving messages and sending them to the queue
    {
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine(message);
        };

        channel.BasicConsume(queue: msg_queue,
                             autoAck: true,
                             consumer: consumer);

        await Task.Delay(-1); //waiting for messages so we don't timeout
    }
}
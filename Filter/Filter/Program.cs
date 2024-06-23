using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using NATS.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FilterService
{
    class Program
    {
        private static IMqttClient _mqttClient;
        private static IConnection _natsConnection;

        private static readonly Dictionary<string, Queue<double>> _buffers = new Dictionary<string, Queue<double>>()
        {
            { "hive temp", new Queue<double>() },
            { "hive humidity", new Queue<double>() },
            { "hive pressure", new Queue<double>() },
            { "weather temp", new Queue<double>() },
            { "weather humidity", new Queue<double>() },
            { "weather pressure", new Queue<double>() },
            { "wind speed", new Queue<double>() },
            { "cloud coverage", new Queue<double>() },
            { "rain", new Queue<double>() },
            { "lat", new Queue<double>() },
            { "long", new Queue<double>() }
        };

        private static readonly int _bufferSize = 10;

        static async Task Main(string[] args)
        {
            string mqttBroker = "host.docker.internal";
            int mqttPort = 1883;
            string mqttTopic = "sensor-data";
            string natsUrl = "nats://host.docker.internal:4222";
            string natsTopic = "sensor/averages";

            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            var mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId("FilterService")
                .WithTcpServer(mqttBroker, mqttPort)
                .Build();

            _natsConnection = new ConnectionFactory().CreateConnection(natsUrl);

            _mqttClient.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected to MQTT broker.");
                await _mqttClient.SubscribeAsync(mqttTopic);
            };

            _mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array, e.ApplicationMessage.PayloadSegment.Offset, e.ApplicationMessage.PayloadSegment.Count);
                ProcessMessage(payload, natsTopic);
                return Task.CompletedTask;
            };

            await _mqttClient.ConnectAsync(mqttOptions);
           
            while (true)
            {
                await Task.Delay(1000);
            }
        }

        static void ProcessMessage(string payload, string natsTopic)
        {
            JObject data = JObject.Parse(payload);

            var averages = new Dictionary<string, double>();

            string device = data["device"].Value<string>();
            string hiveNumber = data["hive number"].Value<string>();

            foreach (var key in _buffers.Keys)
            {
                if (data.ContainsKey(key))
                {
                    double value = data[key].Value<double>();
                    averages[key] = ComputeAverage(_buffers[key], value);
                }
            }

            var message = new
            {
                measurement = "sensor_data",
                device = device,
                hive_number = hiveNumber,
                fields = averages
            };

            Console.WriteLine($"Publishing to NATS topic '{natsTopic}': {JsonConvert.SerializeObject(message)}");

            SendToNats(natsTopic, message);
        }

        static double ComputeAverage(Queue<double> buffer, double newValue)
        {
            if (buffer.Count >= _bufferSize)
            {
                buffer.Dequeue();
            }
            buffer.Enqueue(newValue);

            double sum = 0;
            foreach (var value in buffer)
            {
                sum += value;
            }
            return sum / buffer.Count;
        }

        static void SendToNats(string topic, object data)
        {
            string jsonData = JsonConvert.SerializeObject(data);
            byte[] payload = Encoding.UTF8.GetBytes(jsonData);
            _natsConnection.Publish(topic, payload);
        }
    }
}

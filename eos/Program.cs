using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace eos
{
    class Program
    {
        private static ILoggerFactory LoggerFactory;
        private static readonly ILogger<Program> logger;
        
        private static readonly TimeSpan TIMEOUT_KAFKA_OPS = TimeSpan.FromSeconds(10);
        private static readonly int MAX_PER_TRANSACTION = 3;

        static Program()
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create((builder) =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddLog4Net();
            });
            logger = LoggerFactory.CreateLogger<Program>();
        }

        static void Main(string[] args)
        {
            string bootstrapServer = GetValueOrDefault("KAFKA_BOOTSTRAP_SERVER", "localhost:9092");
            logger.LogDebug($"Connecting to broker: {bootstrapServer}");
            string sourceTopic = GetValueOrDefault("KAFKA_TOPIC", "topic-test");
            logger.LogDebug($"Consuming from: {sourceTopic}");
            string destinationTopic = sourceTopic + "-final";
            logger.LogDebug($"Producing to: {destinationTopic}");
            string applicationId = GetValueOrDefault("KAFKA_GROUP_ID", "transaction-group1");
            logger.LogDebug($"App id is: {applicationId}");
            int sequence = 0;
            
            var consumerBuilder = CreateConsumerBuilder(bootstrapServer, applicationId);
            var producerBuilder = CreateProducerBuilder(bootstrapServer, applicationId);
            
            new Thread(() => 
            {
                Thread.CurrentThread.IsBackground = true;
                logger.LogInformation($"Launching echo!");
                RunEchoProducer(sourceTopic, bootstrapServer);
            }).Start();

            using (var consumer = consumerBuilder.Build())
            using (var producer = producerBuilder.Build())
            {
                Console.CancelKeyPress += (o, e) =>
                {
                    consumer.Unsubscribe();
                    consumer.Close();
                    consumer.Dispose();
                    logger.LogInformation($"Consumer closed");
                    producer.CommitTransaction();
                    logger.LogInformation($"Producer closed");
                };

                logger.LogInformation($"Consumer subscribe topic {sourceTopic}");
                consumer.Subscribe(sourceTopic);


                producer.InitTransactions(TIMEOUT_KAFKA_OPS);
                logger.LogInformation($"Producer initialized transaction mode");
               
                while (true)
                {
                    try
                    {
                        var record = consumer.Consume(TimeSpan.FromMilliseconds(100));
                        if (record != null)
                        {
                            if (sequence == 0)
                            {
                                logger.LogDebug("Beginning a new transaction");
                                producer.BeginTransaction();
                            }
                            var messageKey = record.Message.Key;
                            var messageValue = record.Message.Value;
                            logger.LogDebug($"Message : {messageKey} - {messageValue}" +
                                            $" | Metadata : {record.TopicPartition} - {record.Offset}");

                            var msg = GenerateMessage(sequence, messageKey, messageValue);
                            producer.Produce(destinationTopic, msg, report =>
                            {
                                logger.LogInformation($"Delivery : {report.Error.Code.ToString()} " +
                                                      $"- {report.Error.Reason} | Message : {report.Message.Key} - {report.Message.Value}");
                            });
                            sequence++;
                            if (sequence >= MAX_PER_TRANSACTION)
                            {
                                logger.LogInformation(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Finishing transaction");
                                // Thread.Sleep(10000);
                                // https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#store-offsets
                                // https://docs.confluent.io/kafka-clients/dotnet/current/overview.html#synchronous-commits
                                // use the example https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/examples/ExactlyOnce/Program.cs#L444-L450
                                producer.SendOffsetsToTransaction(
                                    // Note: committed offsets reflect the next message to consume, not last
                                    // message consumed. consumer.Position returns the last consumed offset
                                    // values + 1, as required.
                                    consumer.Assignment.Select(topicPartition => new TopicPartitionOffset(topicPartition, consumer.Position(topicPartition))),
                                    consumer.ConsumerGroupMetadata,
                                    TIMEOUT_KAFKA_OPS);
                                producer.CommitTransaction(TimeSpan.FromSeconds(2));
                                sequence = 0;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Ops, I did it again - {e.GetType()} { e.Message}");
                    }
                }
            }
        }

        private static void RunEchoProducer(string topic, string bootstrapServers)
        {
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                EnableIdempotence = true,
                Acks = Acks.All,
                MaxInFlight = 5,
            };


            Int32 key = 0;
            Int32 blockSize = 0;
            using var echoProducer = new ProducerBuilder<string, string>(config).Build();
            while (true)
            {
                Message<string, string> echoMessage = new Message<string, string>();
                echoMessage.Key = key.ToString();
                echoMessage.Value = "generated at " + DateTime.Now.ToString(CultureInfo.InvariantCulture);
                echoProducer.Produce(topic, echoMessage, report =>
                {
                    logger.LogInformation($"Echo'ed message : {report.Message.Key} - {report.Message.Value}");
                });
                Thread.Sleep(1000);
                blockSize = (blockSize + 1 ) % 3;
                key = (blockSize == 0) ? (key + 1) % 3 : key;
            }
        }

        private static Message<string, string> GenerateMessage(int sequence, string messageKey, string messageValue)
        {
            Message<string, string> msg = new Message<string, string>();
            msg.Key = messageKey;
            msg.Value = messageValue.ToUpper() + $" (changed at ${DateTime.Now.ToString(CultureInfo.InvariantCulture)})";
            return msg;
        }

        private static ProducerBuilder<string, string> CreateProducerBuilder(string bootstrapServer, string transactionId)
        {
            ProducerConfig producerConfig = new ProducerConfig();
            producerConfig.BootstrapServers = bootstrapServer;
            producerConfig.TransactionalId = transactionId;
            producerConfig.EnableIdempotence = true;
            producerConfig.Acks = Acks.All;
            producerConfig.MaxInFlight = 5;
            producerConfig.Debug = "all";

            ProducerBuilder<string, string> producerBuilder = new ProducerBuilder<string, string>(producerConfig);
            producerBuilder.SetErrorHandler((p, e) => logger.LogError($"{e.Code}-{e.Reason}"));
            producerBuilder.SetLogHandler((p, e) => logger.LogInformation($"{e.Name}-{e.Message}"));
            return producerBuilder;
        }

        private static ConsumerBuilder<string, string> CreateConsumerBuilder(string bootstrapServer, string applicationId)
        {
            ConsumerConfig consumerConfig = new ConsumerConfig();
            consumerConfig.BootstrapServers = bootstrapServer;
            consumerConfig.GroupId = applicationId;
            // force transaction 
            consumerConfig.AutoOffsetReset = AutoOffsetReset.Earliest;
            consumerConfig.EnableAutoCommit = false;
            consumerConfig.IsolationLevel = IsolationLevel.ReadCommitted;
            ConsumerBuilder<string, string> consumerBuilder = new ConsumerBuilder<string, string>(consumerConfig);
            consumerBuilder.SetErrorHandler((p, e) => logger.LogError($"{e.Code}-{e.Reason}"));
            consumerBuilder.SetLogHandler((p, e) => logger.LogInformation($"{e.Name}-{e.Message}"));
            return consumerBuilder;
        }

        static string GetValueOrDefault(string envVar, string @default)
        {
            return Environment.GetEnvironmentVariable(envVar) ?? @default;
        }
    }
}
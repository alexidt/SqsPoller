using System;
using System.Linq;
using System.Reflection;
using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SqsPoller
{
    public static class SqsPollerConfiguration
    {
        public static IServiceCollection AddSqsPoller(
            this IServiceCollection services, SqsPollerConfig config, Assembly[] assembliesWithConsumers)
        {
            var types = assembliesWithConsumers
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && typeof(IConsumer).IsAssignableFrom(type))
                .ToArray();
            
            return AddSqsPoller(services, config, types);
        }

        public static IServiceCollection AddSqsPoller(
            this IServiceCollection services, SqsPollerConfig config, Type[] types)
        {
            foreach (var type in types)
            {
                services.AddSingleton(type);
            }
            
            services.AddTransient<IHostedService>(provider =>
            {
                AmazonSQSClient sqsClient;
                if (!string.IsNullOrEmpty(config.AccessKey) &&
                    !string.IsNullOrEmpty(config.SecretKey))
                {
                    sqsClient = new AmazonSQSClient(
                        config.AccessKey, config.SecretKey, CreateSqsConfig(config));
                }
                else
                {
                    sqsClient = new AmazonSQSClient(CreateSqsConfig(config));
                }

                return new SqsPollerHostedService(
                    sqsClient,
                    config,
                    new ConsumerResolver(types.Select(provider.GetRequiredService).Cast<IConsumer>().ToArray()),
                    provider.GetRequiredService<ILogger<SqsPollerHostedService>>());
            });

            return services;
        }

        private static AmazonSQSConfig CreateSqsConfig(SqsPollerConfig config)
        {
            var amazonSqsConfig = new AmazonSQSConfig
            {
                ServiceURL = config.ServiceUrl
            };

            if (!string.IsNullOrEmpty(config.Region))
                amazonSqsConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(config.Region);
            
            return amazonSqsConfig;
        }
    }
}
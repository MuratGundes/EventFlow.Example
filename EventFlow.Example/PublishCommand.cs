﻿using Autofac;
using EventFlow;
using EventFlow.Aggregates.ExecutionResults;
using EventFlow.Autofac.Extensions;
using EventFlow.Configuration;
using EventFlow.EventStores;
using EventFlow.Extensions;
using EventFlow.MongoDB.Extensions;
using EventFlow.RabbitMQ;
using EventFlow.RabbitMQ.Extensions;
using EventFlow.Snapshots.Strategies;
using EventFlowExample.Aggregates.CommandHandlers;
using EventFlowExample.Aggregates.Commands;
using EventFlowExample.Aggregates.Events;
using EventFlowExample.Aggregates.Snapshots;
using EventFlowExample.Jobs;
using MongoDB.Driver;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlowExample
{
    public class PublishCommand
    {
        #region Variables
        ICommandBus CommandBus { get; set; }
        private const string SNAPSHOT_CONTAINER_NAME = "snapshots";
        #endregion

        public PublishCommand() 
        {
        }

        public static ExampleId GetStreamName(string tenantName, string eventName, Guid? aggregateId = null) =>
            new ExampleId($"{tenantName.ToLowerInvariant()}_{eventName.ToLowerInvariant()}-{(aggregateId.HasValue ? aggregateId.ToString() : Guid.NewGuid().ToString())}");

        void DecorateCommandBus(IServiceRegistration sr)
        {
            sr.Decorate<ICommandBus>((r, cb) => new LogCommandBus(cb));
        }

        public async Task PublishCommandAsync()
        {
            var client = new MongoClient("mongodb://localhost:27017");

            using (var resolver = EventFlowOptions.New
                                                  .UseAutofacContainerBuilder(new ContainerBuilder()) // Must be the first line!
                                                  .Configure(c => c.ThrowSubscriberExceptions = true)
                                                  .AddEvents(typeof(ExampleEvent))
                                                  .AddEvents(typeof(ResetEvent))
                                                  .AddCommands(typeof(ExampleCommand))
                                                  .AddCommands(typeof(ResetCommand))
                                                  .AddCommandHandlers(typeof(ExampleCommandHandler))
                                                  .AddCommandHandlers(typeof(ResetCommandHandler))
                                                  .ConfigureEventStore()
                                                  .ConfigureMongoDb(client, SNAPSHOT_CONTAINER_NAME)
                                                  .AddSnapshots(typeof(ExampleSnaphost))
                                                  .UseMongoDbSnapshotStore()
                                                  .RegisterServices(sr => sr.Register(i => SnapshotEveryFewVersionsStrategy.Default))
                                                  .RegisterServices(DecorateCommandBus)
                                                  .PublishToRabbitMq(RabbitMqConfiguration.With(new Uri(@"amqp://test:test@localhost:5672"), true, 4, "eventflow"))
                                                  //.ConfigureSagas()
                                                  //.UseNullLog()
                                                  //.UseInMemoryReadStoreFor<Aggregates.ReadModels.ExampleReadModel>()
                                                  .Configure(c => c.IsAsynchronousSubscribersEnabled = true)
                                                  //.AddAsynchronousSubscriber<ExampleAggregate, Aggregates.Events.ExampleId, ExampleEvent, RabbitMqConsumePersistanceService>()
                                                  //.RegisterServices(s => {
                                                  //    s.Register<IHostedService, RabbitConsumePersistenceService>(Lifetime.Singleton);
                                                  //    s.Register<IHostedService, RabbitMqConsumePersistanceService>(Lifetime.Singleton);
                                                  //})
                                                  //.AddSubscribers(new Type[] { typeof(ExampleSyncSubscriber) })
                                                 // .UseHangfireJobScheduler()
                                                  .AddJobs(typeof(ExampleJob))
                                                  .CreateResolver())
            {
                Int32 magicNumber = 2;
                CommandBus = resolver.Resolve<ICommandBus>();

                var clock = new Stopwatch();
                clock.Start();

                ExampleId exampleId = GetStreamName("Tenant", "EXAMPLE");

                for (int i = 0; i < 1; i++)
                {

                    IExecutionResult result = await CommandBus.PublishAsync(
                        new ExampleCommand(exampleId, magicNumber), CancellationToken.None)
                        .ConfigureAwait(false);
                    #region Comments
                    //result.IsSuccess.Should().BeTrue();

                    //IAggregateStore aggregateStore = resolver.Resolve<IAggregateStore>();
                    //var @object = aggregateStore.LoadAsync<ExampleAggregate, ExampleId>(exampleId, CancellationToken.None);

                    ////Obsolete
                    ////IEventStore eventStore = resolver.Resolve<IEventStore>();
                    ////var aggregate = await eventStore.LoadAggregateAsync<ExampleAggregate, ExampleId>(exampleId, CancellationToken.None);

                    ////state of our aggregate root
                    //var queryProcessor = resolver.Resolve<IQueryProcessor>();
                    //var result = await queryProcessor.ProcessAsync(
                    //     new ReadModelByIdQuery<ExampleReadModel>(exampleId),
                    //     CancellationToken.None)
                    //     .ConfigureAwait(false);

                    //// Verify that the read model has the expected magic number
                    //exampleReadModel.MagicNumber.Should().Be(42);
                    #endregion
                }

                clock.Stop();

                Console.WriteLine("Duration: " + clock.ElapsedMilliseconds + "ms");
            }

            Console.ReadLine();
        }
    }
}

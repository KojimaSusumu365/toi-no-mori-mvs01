namespace ToiNoMori.Api.Persistence;

public sealed class PersistenceInitializerHostedService(IQuestionStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => store.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

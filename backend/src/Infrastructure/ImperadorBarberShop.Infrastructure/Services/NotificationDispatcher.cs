using System.Threading.Channels;
using ImperadorBarberShop.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImperadorBarberShop.Infrastructure.Services;

/// <summary>
/// Fila em memória de notificações pendentes. Singleton: os handlers escrevem,
/// o <see cref="NotificationDispatcher"/> consome.
/// </summary>
public sealed class NotificationQueue : INotificationQueue
{
    // ponytail: fila em memória, sem persistência. Notificação pendente se perde
    // se o processo cair. Trocar por tabela/broker se a entrega virar requisito.
    private readonly Channel<Func<INotificationService, CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<INotificationService, CancellationToken, Task>>(
            new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Func<INotificationService, CancellationToken, Task> job) =>
        _channel.Writer.TryWrite(job);

    internal IAsyncEnumerable<Func<INotificationService, CancellationToken, Task>> ReadAllAsync(
        CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Consome a fila fora da requisição. Cada job roda no seu próprio escopo de DI,
/// porque o escopo da requisição que enfileirou já foi descartado.
/// </summary>
public sealed class NotificationDispatcher : BackgroundService
{
    private readonly NotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        NotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatcher> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await job(notifications, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Best-effort: uma notificação que falha não pode derrubar o
                // dispatcher e parar todas as seguintes.
                _logger.LogError(ex, "Falha ao enviar notificação enfileirada");
            }
        }
    }
}

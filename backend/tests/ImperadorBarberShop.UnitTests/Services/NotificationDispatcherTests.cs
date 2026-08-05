using FluentAssertions;
using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImperadorBarberShop.UnitTests.Services;

public class NotificationDispatcherTests
{
    private static (NotificationQueue queue, NotificationDispatcher dispatcher) Build()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Substitute.For<INotificationService>());
        var provider = services.BuildServiceProvider();

        var queue = new NotificationQueue();
        var dispatcher = new NotificationDispatcher(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationDispatcher>.Instance);

        return (queue, dispatcher);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
    }

    [Fact]
    public async Task Dispatcher_RunsEnqueuedJob()
    {
        var (queue, dispatcher) = Build();
        var ran = false;

        queue.Enqueue((_, _) => { ran = true; return Task.CompletedTask; });

        await dispatcher.StartAsync(CancellationToken.None);
        await WaitForAsync(() => ran);
        await dispatcher.StopAsync(CancellationToken.None);

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task Dispatcher_KeepsRunningAfterAJobThrows()
    {
        var (queue, dispatcher) = Build();
        var secondRan = false;

        // Uma notificação que estoura (SMTP fora do ar, por exemplo) não pode
        // matar o dispatcher e travar todas as seguintes.
        queue.Enqueue((_, _) => throw new InvalidOperationException("SMTP fora do ar"));
        queue.Enqueue((_, _) => { secondRan = true; return Task.CompletedTask; });

        await dispatcher.StartAsync(CancellationToken.None);
        await WaitForAsync(() => secondRan);
        await dispatcher.StopAsync(CancellationToken.None);

        secondRan.Should().BeTrue();
    }
}

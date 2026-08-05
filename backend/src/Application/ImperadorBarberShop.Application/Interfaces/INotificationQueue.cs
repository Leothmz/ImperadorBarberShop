namespace ImperadorBarberShop.Application.Interfaces;

/// <summary>
/// Enfileira notificações para serem enviadas fora do ciclo da requisição.
/// Handlers não devem aguardar SMTP/WhatsApp: um provedor lento seguraria a
/// resposta HTTP do cliente até o timeout da rede.
/// </summary>
public interface INotificationQueue
{
    /// <param name="job">
    /// Recebe um <see cref="INotificationService"/> resolvido num escopo novo e o
    /// token de parada da aplicação — nunca o CancellationToken da requisição,
    /// que já foi cancelado quando o job roda.
    /// </param>
    void Enqueue(Func<INotificationService, CancellationToken, Task> job);
}

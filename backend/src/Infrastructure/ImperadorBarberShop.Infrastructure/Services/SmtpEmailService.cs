using ImperadorBarberShop.Application.Interfaces;
using ImperadorBarberShop.Infrastructure.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ImperadorBarberShop.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public SmtpEmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAppointmentCreatedAsync(
        string barberEmail, string barberName, string clientName, DateTime scheduledAt,
        CancellationToken cancellationToken = default)
    {
        var subject = "Novo agendamento recebido";
        var body = $"Olá {barberName},\n\n" +
                   $"O cliente {clientName} agendou um atendimento para {scheduledAt:dd/MM/yyyy HH:mm}.\n\n" +
                   "Acesse o sistema para aceitar ou rejeitar o agendamento.\n\n" +
                   "O Imperador Barber Shop";

        await SendAsync(barberEmail, subject, body, cancellationToken);
    }

    public async Task SendAppointmentAcceptedAsync(
        string clientEmail, string clientName, DateTime scheduledAt,
        CancellationToken cancellationToken = default)
    {
        var subject = "Seu agendamento foi confirmado!";
        var body = $"Olá {clientName},\n\n" +
                   $"Seu agendamento para {scheduledAt:dd/MM/yyyy HH:mm} foi confirmado.\n\n" +
                   "Esperamos por você!\n\n" +
                   "O Imperador Barber Shop";

        await SendAsync(clientEmail, subject, body, cancellationToken);
    }

    public async Task SendAppointmentRejectedAsync(
        string clientEmail, string clientName, DateTime scheduledAt,
        CancellationToken cancellationToken = default)
    {
        var subject = "Agendamento não disponível";
        var body = $"Olá {clientName},\n\n" +
                   $"Infelizmente seu agendamento para {scheduledAt:dd/MM/yyyy HH:mm} não pôde ser confirmado.\n\n" +
                   "Por favor, tente outro horário.\n\n" +
                   "O Imperador Barber Shop";

        await SendAsync(clientEmail, subject, body, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

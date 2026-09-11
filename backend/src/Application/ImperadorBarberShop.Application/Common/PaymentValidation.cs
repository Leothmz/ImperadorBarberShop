using FluentValidation;
using FluentValidation.Results;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.ValueObjects;

namespace ImperadorBarberShop.Application.Common;

/// <summary>
/// As regras de <see cref="AppointmentPayment"/> como falhas de validação: uma combinação
/// inválida volta como 400, por campo, antes de tocar no agendamento.
/// </summary>
internal static class PaymentValidation
{
    /// <param name="method">Nulo: concluir sem registrar pagamento.</param>
    public static List<ValidationFailure> Failures(
        PaymentMethod? method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount)
    {
        if (method is null)
        {
            // Dados de plano sem a forma Plano seriam descartados em silêncio
            return planKind is null && planTender is null && chargedAmount is null
                ? []
                : [new(PaymentError.PaymentMethodField, "Escolha a forma de pagamento Plano para informar os dados do plano.")];
        }

        return AppointmentPayment.Validate(method.Value, planKind, planTender, chargedAmount)
            .Select(error => new ValidationFailure(error.Field, error.Message))
            .ToList();
    }

    // ToPayment e ToOptionalPayment conferem a combinação no próprio handler: o ValidationBehavior
    // só cobre IRequest<T>, e os comandos de pagamento não devolvem nada (IRequest).

    /// <summary>O pagamento de um atendimento concluído, ou <see cref="ValidationException"/> (400).</summary>
    public static AppointmentPayment ToPayment(
        PaymentMethod method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount)
    {
        ThrowIfInvalid(method, planKind, planTender, chargedAmount);
        return AppointmentPayment.Create(method, planKind, planTender, chargedAmount);
    }

    /// <summary>O pagamento escolhido ao concluir (nulo conclui sem registrar), ou <see cref="ValidationException"/> (400).</summary>
    public static AppointmentPayment? ToOptionalPayment(
        PaymentMethod? method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount)
    {
        ThrowIfInvalid(method, planKind, planTender, chargedAmount);
        return method is { } m ? AppointmentPayment.Create(m, planKind, planTender, chargedAmount) : null;
    }

    private static void ThrowIfInvalid(
        PaymentMethod? method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount)
    {
        var failures = Failures(method, planKind, planTender, chargedAmount);
        if (failures.Count > 0)
            throw new ValidationException(failures);
    }
}

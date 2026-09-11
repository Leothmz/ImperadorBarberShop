using ImperadorBarberShop.Domain.Enums;

namespace ImperadorBarberShop.Domain.ValueObjects;

/// <summary>
/// Como um atendimento concluído foi pago. Fora do plano é só o método. No plano, o tipo decide
/// o resto: <see cref="Enums.PlanKind.Pagamento"/> cobra um valor digitado em Pix, Dinheiro ou
/// Cartão; <see cref="Enums.PlanKind.Recorrencia"/> não cobra nada nesta visita. É o único lugar
/// com essas combinações: os validadores dos comandos devolvem os mesmos erros como 400.
/// </summary>
public sealed record AppointmentPayment
{
    private const string NotAPlanMessage = "Os dados do plano só valem para a forma de pagamento Plano.";

    public PaymentMethod Method { get; }

    /// <summary>Só no plano.</summary>
    public PlanKind? PlanKind { get; }

    /// <summary>Como o plano foi pago (Pix, Dinheiro ou Cartão). Só em <see cref="Enums.PlanKind.Pagamento"/>.</summary>
    public PaymentMethod? PlanTender { get; }

    /// <summary>Valor cobrado, só no plano (0 na recorrência). Nulo: vale a soma dos serviços.</summary>
    public decimal? ChargedAmount { get; }

    private AppointmentPayment(PaymentMethod method, PlanKind? planKind, PaymentMethod? planTender, decimal? chargedAmount)
    {
        Method = method;
        PlanKind = planKind;
        PlanTender = planTender;
        ChargedAmount = chargedAmount;
    }

    public static AppointmentPayment Normal(PaymentMethod method) => Create(method);

    public static AppointmentPayment PlanPayment(decimal chargedAmount, PaymentMethod tender)
        => Create(PaymentMethod.Plano, Enums.PlanKind.Pagamento, tender, chargedAmount);

    public static AppointmentPayment PlanRecurrence() => Create(PaymentMethod.Plano, Enums.PlanKind.Recorrencia);

    /// <summary>Monta o pagamento a partir do que chegou na requisição, recusando combinações inválidas.</summary>
    /// <exception cref="ArgumentException">A combinação não passa em <see cref="Validate"/>.</exception>
    public static AppointmentPayment Create(
        PaymentMethod method,
        PlanKind? planKind = null,
        PaymentMethod? planTender = null,
        decimal? chargedAmount = null)
    {
        var errors = Validate(method, planKind, planTender, chargedAmount);
        if (errors.Count > 0)
            throw new ArgumentException(errors[0].Message);

        return planKind switch
        {
            null => new(method, null, null, null),
            Enums.PlanKind.Pagamento => new(method, planKind, planTender, chargedAmount),
            // A recorrência sempre grava R$ 0: é o que entra no faturamento desta visita
            _ => new(method, planKind, null, 0m),
        };
    }

    /// <summary>Os erros da combinação, por campo; vazio quando ela é válida.</summary>
    public static IReadOnlyList<PaymentError> Validate(
        PaymentMethod method,
        PlanKind? planKind,
        PaymentMethod? planTender,
        decimal? chargedAmount)
    {
        var errors = new List<PaymentError>();

        if (!Enum.IsDefined(method))
        {
            errors.Add(new(PaymentError.PaymentMethodField, "Forma de pagamento inválida."));
            return errors;
        }

        if (method != PaymentMethod.Plano)
        {
            if (planKind is not null) errors.Add(new(PaymentError.PlanKindField, NotAPlanMessage));
            if (planTender is not null) errors.Add(new(PaymentError.PlanTenderField, NotAPlanMessage));
            if (chargedAmount is not null) errors.Add(new(PaymentError.ChargedAmountField, NotAPlanMessage));
            return errors;
        }

        switch (planKind)
        {
            case null:
                errors.Add(new(PaymentError.PlanKindField, "Informe se o plano é Pagamento ou Recorrência."));
                break;

            case Enums.PlanKind.Pagamento:
                if (planTender is null)
                    errors.Add(new(PaymentError.PlanTenderField, "Informe como o plano foi pago: Pix, Dinheiro ou Cartão."));
                else if (planTender is not (PaymentMethod.Dinheiro or PaymentMethod.Cartão or PaymentMethod.Pix))
                    errors.Add(new(PaymentError.PlanTenderField, "O plano é pago em Pix, Dinheiro ou Cartão."));

                if (chargedAmount is null)
                    errors.Add(new(PaymentError.ChargedAmountField, "Informe o valor cobrado pelo plano."));
                else if (chargedAmount < 0)
                    errors.Add(new(PaymentError.ChargedAmountField, "O valor cobrado não pode ser negativo."));
                else if (decimal.Round(chargedAmount.Value, 2) != chargedAmount.Value)
                    errors.Add(new(PaymentError.ChargedAmountField, "O valor cobrado aceita no máximo duas casas decimais."));
                break;

            case Enums.PlanKind.Recorrencia:
                if (planTender is not null)
                    errors.Add(new(PaymentError.PlanTenderField, "Recorrência não tem forma de pagamento: nada é cobrado nesta visita."));
                if (chargedAmount is not null and not 0m)
                    errors.Add(new(PaymentError.ChargedAmountField, "Recorrência não cobra valor nesta visita."));
                break;

            default:
                errors.Add(new(PaymentError.PlanKindField, "Tipo de plano inválido."));
                break;
        }

        return errors;
    }
}

/// <param name="Field">Nome do campo na requisição, como os validadores o reportam.</param>
public readonly record struct PaymentError(string Field, string Message)
{
    public const string PaymentMethodField = "PaymentMethod";
    public const string PlanKindField = "PlanKind";
    public const string PlanTenderField = "PlanTender";
    public const string ChargedAmountField = "ChargedAmount";
}

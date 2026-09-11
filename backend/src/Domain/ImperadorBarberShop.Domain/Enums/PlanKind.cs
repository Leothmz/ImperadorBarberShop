namespace ImperadorBarberShop.Domain.Enums;

/// <summary>O que um atendimento de plano (<see cref="PaymentMethod.Plano"/>) representa no caixa.</summary>
public enum PlanKind
{
    /// <summary>O cliente paga o plano nesta visita: um valor digitado, em Pix, Dinheiro ou Cartão.</summary>
    Pagamento   = 0,
    /// <summary>Visita coberta por um plano já pago: nada é cobrado agora.</summary>
    Recorrencia = 1,
}

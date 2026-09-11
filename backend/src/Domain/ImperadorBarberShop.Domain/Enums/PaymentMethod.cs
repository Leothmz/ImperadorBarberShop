namespace ImperadorBarberShop.Domain.Enums;

public enum PaymentMethod
{
    Dinheiro = 0,
    Cartão   = 1,
    Pix      = 2,
    /// <summary>Atendimento de plano: o detalhe fica em Appointment.PlanKind (ver AppointmentPayment).</summary>
    Plano    = 3,
}

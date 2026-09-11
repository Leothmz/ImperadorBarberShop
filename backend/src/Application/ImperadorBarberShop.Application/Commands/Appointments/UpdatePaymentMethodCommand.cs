using FluentValidation;
using ImperadorBarberShop.Application.Common;
using ImperadorBarberShop.Domain.Enums;
using ImperadorBarberShop.Domain.Exceptions;
using ImperadorBarberShop.Domain.Interfaces;
using ImperadorBarberShop.Domain.ValueObjects;
using MediatR;

namespace ImperadorBarberShop.Application.Commands.Appointments;

/// <summary>Registra ou troca o pagamento de um atendimento concluído, com os dados do plano quando houver.</summary>
/// <param name="PlanKind">Só com <see cref="Domain.Enums.PaymentMethod.Plano"/>; ver <see cref="AppointmentPayment"/>.</param>
public record UpdatePaymentMethodCommand(
    Guid AppointmentId,
    PaymentMethod PaymentMethod,
    Guid? RequesterBarberId,   // null = admin, bypasses IDOR
    PlanKind? PlanKind = null,
    PaymentMethod? PlanTender = null,
    decimal? ChargedAmount = null)
    : IRequest;

public class UpdatePaymentMethodCommandValidator : AbstractValidator<UpdatePaymentMethodCommand>
{
    public UpdatePaymentMethodCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x).Custom((command, context) => PaymentValidation
            .Failures(command.PaymentMethod, command.PlanKind, command.PlanTender, command.ChargedAmount)
            .ForEach(context.AddFailure));
    }
}

public class UpdatePaymentMethodCommandHandler : IRequestHandler<UpdatePaymentMethodCommand>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePaymentMethodCommandHandler(IAppointmentRepository appointmentRepository, IUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var payment = PaymentValidation.ToPayment(
            request.PaymentMethod, request.PlanKind, request.PlanTender, request.ChargedAmount);

        var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
            throw new KeyNotFoundException($"Appointment '{request.AppointmentId}' not found.");

        if (request.RequesterBarberId.HasValue && appointment.BarberId != request.RequesterBarberId.Value)
            throw new ForbiddenException("You are not authorized to update this appointment.");

        appointment.SetPayment(payment);
        await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

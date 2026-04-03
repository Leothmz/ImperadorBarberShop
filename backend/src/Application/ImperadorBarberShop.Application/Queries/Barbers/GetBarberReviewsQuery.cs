using AutoMapper;
using ImperadorBarberShop.Application.DTOs;
using ImperadorBarberShop.Domain.Interfaces;
using MediatR;

namespace ImperadorBarberShop.Application.Queries.Barbers;

public record GetBarberReviewsQuery(Guid BarberId) : IRequest<List<ReviewDto>>;

public class GetBarberReviewsQueryHandler : IRequestHandler<GetBarberReviewsQuery, List<ReviewDto>>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IMapper _mapper;

    public GetBarberReviewsQueryHandler(IReviewRepository reviewRepository, IMapper mapper)
    {
        _reviewRepository = reviewRepository;
        _mapper = mapper;
    }

    public async Task<List<ReviewDto>> Handle(GetBarberReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await _reviewRepository.GetByBarberIdAsync(request.BarberId, cancellationToken);
        return _mapper.Map<List<ReviewDto>>(reviews);
    }
}

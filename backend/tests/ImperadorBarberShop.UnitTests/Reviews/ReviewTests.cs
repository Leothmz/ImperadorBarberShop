using FluentAssertions;
using ImperadorBarberShop.Domain.Entities;

namespace ImperadorBarberShop.UnitTests.Reviews;

public class ReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_RatingOutOfRange_Throws(int rating)
    {
        var act = () => Review.Create(Guid.NewGuid(), Guid.NewGuid(), rating, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ValidRating_SetsFields()
    {
        var appointmentId = Guid.NewGuid();
        var barberId = Guid.NewGuid();

        var review = Review.Create(appointmentId, barberId, 5, "Top!");

        review.AppointmentId.Should().Be(appointmentId);
        review.BarberId.Should().Be(barberId);
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Top!");
    }
}

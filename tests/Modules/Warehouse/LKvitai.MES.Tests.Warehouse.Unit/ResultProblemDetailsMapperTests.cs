using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ResultProblemDetailsMapperTests
{
    [Theory]
    [InlineData(DomainErrorCodes.ConcurrencyConflict, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrorCodes.IdempotencyInProgress, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrorCodes.IdempotencyAlreadyProcessed, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrorCodes.HardLockConflict, StatusCodes.Status409Conflict)]
    [InlineData(DomainErrorCodes.InsufficientBalance, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(DomainErrorCodes.ReservationNotAllocated, StatusCodes.Status400BadRequest)]
    [InlineData(DomainErrorCodes.InvalidProjectionName, StatusCodes.Status400BadRequest)]
    [InlineData(DomainErrorCodes.ValidationError, StatusCodes.Status400BadRequest)]
    [InlineData(DomainErrorCodes.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(DomainErrorCodes.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(DomainErrorCodes.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(DomainErrorCodes.InternalError, StatusCodes.Status500InternalServerError)]
    public void ToProblemDetails_FailedResult_MapsStatusAndAddsErrorCodeAndTraceId(
        string errorCode,
        int expectedStatus)
    {
        // Arrange
        const string traceId = "trace-123";
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceId
        };
        var result = Result.Fail(errorCode, $"detail for {errorCode}");

        // Act
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, httpContext);

        // Assert
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions.Should().ContainKey("traceId");
        problemDetails.Extensions["errorCode"].Should().Be(errorCode);
        problemDetails.Extensions["traceId"].Should().Be(traceId);
    }
}

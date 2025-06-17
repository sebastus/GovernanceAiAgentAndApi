using Xunit;
using govapi.Controllers;
using govapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace govapi.Tests
{
    public class TimeControllerTests
    {
        [Fact]
        public void GetCurrentTime_ReturnsOkResult_WithIso8601String()
        {
            // Arrange
            var mockTimeService = new MockTimeService();
            var controller = new TimeController(mockTimeService);

            // Act
            var result = controller.GetCurrentTime();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var value = Assert.IsType<string>(okResult.Value);
            Assert.True(DateTime.TryParse(value, out _));
        }

        private class MockTimeService : ITimeService
        {
            public DateTime GetUtcNow() => new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        }
    }
}

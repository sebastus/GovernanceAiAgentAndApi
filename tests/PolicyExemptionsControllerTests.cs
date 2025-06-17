using Xunit;
using Moq;
using govapi.Controllers;
using govapi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace govapi.Tests
{
    public class PolicyExemptionsControllerTests
    {
        [Fact]
        public async Task GetPolicyExemptions_ReturnsJsonResult()
        {
            // Arrange
            var mockService = new Mock<IPolicyExemptionsService>();
            var mockLogger = new Mock<ILogger<PolicyExemptionsController>>();
            var fakeResult = new Dictionary<string, List<Dictionary<string, object?>>>
            {
                { "assignment1", new List<Dictionary<string, object?>> { new Dictionary<string, object?> { { "name", "ex1" } } } }
            };
            mockService.Setup(s => s.GetPolicyExemptionsAsync(
                It.IsAny<string>(), It.IsAny<HttpContext>(), false, false, null))
                .ReturnsAsync(fakeResult);

            var controller = new PolicyExemptionsController(mockService.Object, mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            // Act
            var result = await controller.GetPolicyExemptions("subid");

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Equal(fakeResult, jsonResult.Value);
        }

        [Fact]
        public async Task GetPolicyExemptionByName_ReturnsNotFound_WhenNull()
        {
            var mockService = new Mock<IPolicyExemptionsService>();
            var mockLogger = new Mock<ILogger<PolicyExemptionsController>>();
            mockService.Setup(s => s.GetPolicyExemptionDetailsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync((JsonElement?)null);

            var controller = new PolicyExemptionsController(mockService.Object, mockLogger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var result = await controller.GetPolicyExemptionByName("subid", "ex1");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdatePolicyExemptionExpiresOn_ReturnsBadRequest_WhenExpiresOnMissing()
        {
            var mockService = new Mock<IPolicyExemptionsService>();
            var mockLogger = new Mock<ILogger<PolicyExemptionsController>>();
            var controller = new PolicyExemptionsController(mockService.Object, mockLogger.Object);

            var result = await controller.UpdatePolicyExemptionExpiresOn("subid", "ex1", "");
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}

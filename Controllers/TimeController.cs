using Microsoft.AspNetCore.Mvc;
using govapi.Services;

namespace govapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TimeController : ControllerBase
    {
        private readonly ITimeService _timeService;

        public TimeController(ITimeService timeService)
        {
            _timeService = timeService;
        }

        [HttpGet]
        public IActionResult GetCurrentTime()
        {
            // Use the injected time service to get the current time
            var now = _timeService.GetUtcNow();
            return Ok(now.ToString("o")); // ISO 8601 format
        }
    }
}

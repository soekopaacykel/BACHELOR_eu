using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CVAPI.Services;

namespace CVAPI.Controllers
{
    [Authorize(Roles = "manager")]
    [ApiController]
    [Route("api/[controller]")]
    public class OneTimeLinkController : ControllerBase
    {
        private readonly OneTimeLinkService _oneTimeLinkService;

        public OneTimeLinkController(OneTimeLinkService oneTimeLinkService)
        {
            _oneTimeLinkService = oneTimeLinkService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateOneTimeLink([FromBody] OneTimeLinkRequest request)
        {
            try
            {
                await _oneTimeLinkService.GenerateAndSendOneTimeLink(request.Email);
                return Ok(new { Message = "One-time link sent to email" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Could not generate link", Error = ex.Message });
            }
        }
    }

    [AllowAnonymous]
    [ApiController]
    [Route("[controller]")]
    public class OneTimeLinkValidationController : ControllerBase
    {
        private readonly OneTimeLinkService _oneTimeLinkService;

        public OneTimeLinkValidationController(OneTimeLinkService oneTimeLinkService)
        {
            _oneTimeLinkService = oneTimeLinkService;
        }

        [HttpGet("validate/{token}")]
        public async Task<IActionResult> ValidateOneTimeLink(string token)
        {
            bool isValid = await _oneTimeLinkService.ValidateOneTimeLink(token);
            return isValid
                ? Ok("Link is valid. Proceed to create profile.")
                : BadRequest("Invalid or expired link");
        }
    }

    public class OneTimeLinkRequest
    {
        public string Email { get; set; }
    }
}
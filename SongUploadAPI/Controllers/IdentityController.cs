﻿using System.Threading.Tasks;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SongUploadAPI.Contracts.Requests;
using SongUploadAPI.Contracts.Responses;
using SongUploadAPI.Domain;
using SongUploadAPI.Services;

namespace SongUploadAPI.Controllers
{
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    [ApiController]
    public class IdentityController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        public IdentityController(IIdentityService identityService) => _identityService = identityService;

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthSuccessResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> Register([FromBody] UserRegistrationRequest request) => 
            GenerateAuthResponse(await _identityService.RegisterAsync(request.Email, request.Password));

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthSuccessResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest request) => 
            GenerateAuthResponse(await _identityService.LoginAsync(request.Email, request.Password));

        private IActionResult GenerateAuthResponse(Result<Token> authResponse) =>
            authResponse.Match<IActionResult>(
                token => Ok(new AuthSuccessResponse(token)),
                error => BadRequest(error.Adapt<ErrorResponse>()));
    }
}

﻿using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SongUploadAPI.Domain;
using SongUploadAPI.Options;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SongUploadAPI.Models;

namespace SongUploadAPI.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtSettings _jwtSettings;

        public IdentityService(UserManager<ApplicationUser> userManager, JwtSettings jwtSettings)
        {
            _userManager = userManager;
            _jwtSettings = jwtSettings;
        }

        public async Task<AuthenticationResult> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null) return new AuthenticationResult
            {

                ErrorMessages = new[] { "User does not exist" },
                Succeeded = false
            };

            var userHasValidPassword = await _userManager.CheckPasswordAsync(user, password);

            if (userHasValidPassword == false) return new AuthenticationResult
            {
                ErrorMessages = new[] { "user and/or password is incorrect" }
            };

            return new AuthenticationResult
            {
                Succeeded = true,
                Token = GenerateAndWriteTokenForUser(user)
            };
        }

        public async Task<AuthenticationResult> RegisterAsync(string email, string password)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                return new AuthenticationResult
                {
                    ErrorMessages = new[] { "User with this email address already exists" },
                    Succeeded = false
                };
            }

            var newUser = new ApplicationUser()
            {
                Email = email,
                UserName = email,
                Songs = new List<Song>()
            };

            var createdUser = await _userManager.CreateAsync(newUser, password);

            if (createdUser.Succeeded == false)
            {
                return new AuthenticationResult
                {
                    ErrorMessages = createdUser.Errors.Select(err => err.Description),
                    Succeeded = false
                };
            }

            return new AuthenticationResult
            {
                Succeeded = true,
                Token = GenerateAndWriteTokenForUser(newUser)
            };
        }

        private string GenerateAndWriteTokenForUser(ApplicationUser newUser)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims: new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, newUser.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, newUser.Email),
                    new Claim(_jwtSettings.UserIdClaimName, newUser.Id),
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}

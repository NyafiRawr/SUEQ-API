﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SUEQ_API.Models;
// Хэширование пароля
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
// Использование токена
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
// Проверка почты
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SUEQ_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly SUEQContext _context;

        public UsersController(SUEQContext context)
        {
            _context = context;
        }

        [Route("me")]
        public IActionResult Test()
        {
            return Ok($"Вы: {HttpContext.User.Identity.Name}. ID: {HttpContext.User.FindFirst("UserId").Value}");
        }

        private byte[] GetSalt()
        {
            // Генерация 128-битной соли с использованием генератора псевдослучайных чисел
            byte[] salt = new byte[128 / 8];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private string ToHash(string password, byte[] salt)
        {
            // Извлечение 256-битного подключа (используется HMACSHA1 с 10,000 итераций)
            string hashed = Convert.ToBase64String(
                KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA1,
                    iterationCount: 10000,
                    numBytesRequested: 256 / 8
                )
            );
            return hashed;
        }

        public class LoginModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class LoginResult
        {
            public bool Validation { get; set; }
            public string Error { get; set; }
            public string Token { get; set; }
        }

        [AllowAnonymous]
        [HttpGet("login")]
        public async Task<ActionResult<LoginResult>> Login([FromBody] LoginModel login)
        {
            var findUser = await _context.Users.SingleOrDefaultAsync(user => user.Email == login.Email);
            if (findUser == null)
            {
                return BadRequest(new LoginResult { Validation = false, Error = "User not found." });
            }
            
            string PasswordHash = ToHash(login.Password, findUser.PasswordSalt);

            if (findUser.PasswordHash != PasswordHash)
            {
                return BadRequest(new LoginResult { Validation = false, Error = "Password are invalid." });
            }

            var now = DateTime.UtcNow;
            // identity not init. claim other
            var claims = new[] {
                new Claim(JwtRegisteredClaimNames.Sub, $"{findUser.SurName} {findUser.FirstName} {findUser.LastName}"),
                new Claim(JwtRegisteredClaimNames.Email, login.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, findUser.UserId.ToString()),
                new Claim("UserId", findUser.UserId.ToString())
            };

            // Создаем JWT
            var jwt = new JwtSecurityToken(
                issuer: TokenOptions.ISSUER,
                audience: TokenOptions.AUDIENCE,
                notBefore: now,
                claims: claims,
                expires: now.Add(TimeSpan.FromMinutes(TokenOptions.LIFETIME)),
                signingCredentials: new SigningCredentials(TokenOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256)
            );

            return Ok(new LoginResult { Validation = true, Token = new JwtSecurityTokenHandler().WriteToken(jwt) });
        }

        public class RegistrationModel
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string FirstName { get; set; }
            public string SurName { get; set; }
            public string LastName { get; set; }
        }

        public bool CheckSizeFIO(string first = null, string sur = null, string last = null)
        {
            if (last == null && sur == null && first == null)
            {
                return true;
            }

            if (first != null)
            {
                if (first.Length < 2)
                {
                    return false;
                }
            }

            if (sur != null)
            {
                if (sur.Length < 3)
                {
                    return false;
                }
            }

            if (last != null)
            {
                if (last.Length < 3)
                {
                    return false;
                }
            }

            return true;
        }

        public bool CheckEmail(string email)
        {
            if (new EmailAddressAttribute().IsValid(email))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool CheckPassword(string password)
        {
            if (password.Length >= 3)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        [AllowAnonymous]
        [HttpPost("registration")]
        public async Task<ActionResult> Registration([FromBody] RegistrationModel registration)
        {
            var findEmail = await _context.Users.SingleOrDefaultAsync(user => user.Email == registration.Email);
            if (findEmail != null)
            {
                return BadRequest("Email already exists.");
            }

            var newUser = new User();
            if (!CheckEmail(registration.Email))
            {
                return BadRequest("Incorrect email!");
            }
            newUser.Email = registration.Email;
            if (
                registration.FirstName == null ||
                registration.SurName == null || 
                registration.LastName == null ||
                !CheckSizeFIO(registration.FirstName, registration.SurName, registration.LastName)
            )
            {
                return BadRequest("Bad size fields FIO!");
            }
            newUser.FirstName = registration.FirstName;
            newUser.SurName = registration.SurName;
            newUser.LastName = registration.LastName;
            if (!CheckPassword(registration.Password))
            {
                return BadRequest("Small password! Min size: 3 symbols");
            }
            newUser.PasswordSalt = GetSalt();
            newUser.PasswordHash = ToHash(registration.Password, newUser.PasswordSalt);

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
                
            return Ok($"Account created. ID: {newUser.UserId}");
        }

        [HttpGet("info/{email}")]
        public async Task<ActionResult<User>> GetUser(string email)
        {
            var user = await _context.Users.SingleOrDefaultAsync(user => user.Email == email);

            if (user == null)
            {
                return NotFound();
            }

            user.PasswordHash = null;
            user.PasswordSalt = null;

            return user;
        }

        [HttpPut("update/{id}")] // TODO: нет проверки, что это клиент обновляет себя самого! ДОЛГО ВЫПОЛНЯЕТСЯ
        public async Task<IActionResult> UpdateUser(int id, RegistrationModel newUser)
        {
            var findUser = await _context.Users.FindAsync(id);
            if (findUser == null)
            {
                return BadRequest("User ID not found");
            }

            if (newUser.Email != null && newUser.Email != findUser.Email)
            {
                if (!CheckEmail(newUser.Email))
                {
                    return BadRequest("Email are invalid!");
                }
                findUser.Email = newUser.Email;
            }

            if (newUser.Password != null)
            {
                if (!CheckPassword(newUser.Password))
                {
                    return BadRequest("Very small password!");
                }
                findUser.PasswordSalt = GetSalt();
                findUser.PasswordHash = ToHash(newUser.Password, findUser.PasswordSalt);
            }

            if (!CheckSizeFIO(newUser.FirstName, newUser.SurName, newUser.LastName))
            {
                return BadRequest("Bad size fields FIO!");
            }
            if (newUser.FirstName != null)
            { 
                findUser.FirstName = newUser.FirstName; 
            }
            if (newUser.SurName != null)
            {
                findUser.SurName = newUser.SurName;
            }
            if (newUser.LastName != null)
            {
                findUser.LastName = newUser.LastName;
            }

            _context.Entry(findUser).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok("Updated.");
        }
        
        [HttpDelete("delete/{id}")] // TODO: нет проверки что это клиент удаляет себя самого! передать пароль и сверить?
        public async Task<ActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok("Account deleted.");
        }
    }
}

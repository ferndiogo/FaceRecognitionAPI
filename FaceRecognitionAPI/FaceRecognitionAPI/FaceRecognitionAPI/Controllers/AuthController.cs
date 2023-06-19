using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.DTO;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FaceRecognitionAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        //public static User user = new User();
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration, ApplicationDbContext db)
        {
            _configuration = configuration;
            this.db = db;
        }

        [Consumes("multipart/form-data")]
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromForm] String username, [FromForm] String password)
        {
            if (!(db.Users.Where(x => x.Username == username).Any()))
            {
                User user = new User();

                CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);

                user.Username = username;
                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;
                user.Role = "User";

                db.Users.Add(user);
                db.SaveChanges();

                return Ok(user);
            }
            else
            {
                return BadRequest("Nome de utilizador já esta a ser utilizado.");
            }
        }

        [Consumes("multipart/form-data")]
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login([FromForm] String username, [FromForm] String password)
        {
            if (!db.Users.Where(a => a.Username == username).Any())
            {
                return BadRequest("Utilizador não encontrado.");
            }

            User user = db.Users.Where(a => a.Username == username).FirstOrDefault();


            if (!VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            {
                return BadRequest("Password incorreta.");
            }

            string token = CreateToken(user);

            user.TokenCreated = DateTime.Now;
            user.TokenExpires = DateTime.Now.AddDays(7);
            db.SaveChanges();

            return Ok(token);
        }

        [HttpGet]
        public async Task<ActionResult<List<UserDTO>>> ListUsers()
        {
            List<UserDTO> list = await db.Users
                .OrderByDescending(a => a.Id)
                .Select(x => new UserDTO
                {
                    Id = x.Id,
                    Username = x.Username,
                    TokenCreated = x.TokenCreated,
                    Role = x.Role
                })
                .ToListAsync();

            if (!list.Any())
            { return BadRequest("There are no registered Users"); }

            return Ok(list);
        }

        [HttpPut("edit/{Id}")]
        public async Task<ActionResult<UserDTO>> EditEmployee(int Id, [FromForm] UserDTO user)
        {

            var userDB = await db.Users.FindAsync(Id);

            if (userDB == null)
            { return BadRequest("Unregistered User"); }

            userDB.Username = user.Username;
            userDB.Role = user.Role;

            UserDTO dto = new UserDTO
            {
                Id = userDB.Id,
                Username = userDB.Username,
                TokenCreated = userDB.TokenCreated,
                Role = userDB.Role
            };

            await db.SaveChangesAsync();

            return Ok(dto);
        }

        [HttpDelete("delete/{Id}")]
        public async Task<IActionResult> DeleteEmployee(int Id)
        {
            var user = await db.Users.FindAsync(Id);
            if (user == null)
            { return BadRequest("Unregistered employee"); }

            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Ok("User successfully removed");
        }



        [HttpGet("Username")]
        [Authorize]
        public async Task<ActionResult<string>> GetUsername()
        {
            var identity = User.Identity.Name;
            //var username = identity.Claims.FirstOrDefault(c => c.Type == "username").Value;

            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == identity);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.Username);

        }

        [HttpGet("Roles")]
        [Authorize]
        public async Task<ActionResult<string>> GetRoles()
        {
            var identity = User.Identity.Name;
            //var username = identity.Claims.FirstOrDefault(c => c.Type == "username").Value;

            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == identity);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.Role);

        }

        [HttpGet("Id")]
        [Authorize]
        public async Task<ActionResult<string>> GetId()
        {
            var identity = User.Identity.Name;
            //var username = identity.Claims.FirstOrDefault(c => c.Type == "username").Value;

            var user = await db.Users.FirstOrDefaultAsync(x => x.Username == identity);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.Id);

        }


        private string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                _configuration.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: user.TokenExpires,
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }
    }
}

using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.DTO;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Authorization;
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
        //variavel utilizada para comunicação com a base de dados
        private readonly ApplicationDbContext db;
        //variavel utilizada para aceder as configurações presentes no ficheiro appsettings.json
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Construtor da classe
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="db"></param>
        public AuthController(IConfiguration configuration, ApplicationDbContext db)
        {
            _configuration = configuration;
            this.db = db;
        }

        /// <summary>
        /// Registar um novo utilizador
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
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

        /// <summary>
        /// Alterar password do utilizador
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [Consumes("multipart/form-data")]
        [HttpPost("changePass")]
        public async Task<ActionResult<String>> ChangePassword([FromForm] String password)
        {
            var username = User.Identity.Name;
            User user = await db.Users.Where(a => a.Username == username).FirstOrDefaultAsync();

            if (User != null)
            {
                CreatePasswordHash(password, out byte[] passwordHash, out byte[] passwordSalt);

                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;

                await db.SaveChangesAsync();

                return Ok("Password atualizada com sucesso");
            }
            else
            {
                return BadRequest("Utilizador não encontrado.");
            }
        }


        /// <summary>
        /// Permite ao utilizador realizar login
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
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

            user.TokenCreated = DateTime.Now;
            user.TokenExpires = DateTime.Now.AddDays(7);
            await db.SaveChangesAsync();

            string token = CreateToken(user);

            return Ok(token);
        }

        /// <summary>
        /// Listar os utilizadores do sistema
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
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

        /// <summary>
        /// Editar o username e o 
        /// </summary>
        /// <param name="Id"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [HttpPut("edit/{Id}")]
        public async Task<ActionResult<UserDTO>> EditUser(int Id, [FromForm] UserDTO user)
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

    /// <summary>
    /// Eliminar utilizador
    /// </summary>
    /// <param name="Id"></param>
    /// <returns></returns>
    [Authorize(Roles = "Admin")]
    [HttpDelete("delete/{Id}")]
    public async Task<IActionResult> DeleteUser(int Id)
    {
        var user = await db.Users.FindAsync(Id);
        if (user == null)
        { return BadRequest("Unregistered employee"); }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Ok("User successfully removed");
    }

    /// <summary>
    /// Obter o username a partir do token
    /// </summary>
    /// <returns></returns>
    [HttpGet("Username")]
    [Authorize(Roles = "Admin, User")]
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

    /// <summary>
    /// Obter a role do utilizador a partir do token
    /// </summary>
    /// <returns></returns>
    [HttpGet("Roles")]
    [Authorize(Roles = "Admin, User")]
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

    /// <summary>
    /// Obter o id do utilizador a partir do token
    /// </summary>
    /// <returns></returns>
    [HttpGet("Id")]
    [Authorize(Roles = "Admin, User")]
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

    /// <summary>
    /// Função para criar o token JWT para o utilizador
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Criar o hash e o salt da password
    /// </summary>
    /// <param name="password"></param>
    /// <param name="passwordHash"></param>
    /// <param name="passwordSalt"></param>
    private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }

    /// <summary>
    /// Verifica se a password do utilizador corresponde ao hash armazenado
    /// </summary>
    /// <param name="password"></param>
    /// <param name="passwordHash"></param>
    /// <param name="passwordSalt"></param>
    /// <returns></returns>
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

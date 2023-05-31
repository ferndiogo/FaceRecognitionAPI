using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceRecognitionAPI.Controllers {
    [Route("[controller]")]
    [ApiController]
    public class RegistryController : ControllerBase {

        private readonly ApplicationDbContext _context;

        public RegistryController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<Registry>>> ListRegistries()
        {
            List <Registry> registries = await _context.Registries
                .Include(a => a.Employee)
                .OrderByDescending(a => a.Id)
                .Select(x => new Registry
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    Type = x.Type,
                    EmployeeId = x.EmployeeId,
                    Employee = x.Employee,
                })
                .ToListAsync();

            if (registries.Any())
            { return Ok(registries); }
            return BadRequest("There are no Registries");
        }

        [HttpGet("employee/{employeeId}")]
        public async Task<ActionResult<List<Registry>>> ListRegistriesEmployee(int employeeId)
        {
            if(await _context.Employees.FindAsync(employeeId) == null)
            { return BadRequest("Unregistered employee"); }

            List<Registry> list = await _context.Registries
                .Include(a => a.Employee)
                .OrderByDescending(a => a.Id)
                .Select(x => new Registry
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    Type = x.Type,
                    EmployeeId = x.EmployeeId,
                    Employee = x.Employee,
                })
                .Where(a => a.EmployeeId == employeeId) 
                .ToListAsync();

            if (list.Any())
            { return Ok(list); }
            return BadRequest("There are no Registries for this employee");
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Registry>> GetRegistry(int id)
        {
            Registry rgs = await _context.Registries.FindAsync(id);

            if (rgs == null)
            { return BadRequest("This registry does not exist"); }

            return Ok(rgs);
        }

        [HttpPost]
        public async Task<ActionResult<Registry>> AddRegistry(Registry registry)
        {
            Employee emp = await _context.Employees.FindAsync(registry.EmployeeId);

            if (emp == null)
            { return BadRequest("Unregistered employee"); }

            registry.Id = 0;
            registry.Employee = emp;
            _context.Registries.Add(registry);

            await _context.SaveChangesAsync();

            return Ok(registry);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegistry(int id)
        {
            var rg = await _context.Registries.FindAsync(id);

            if (rg == null)
            { return BadRequest("This registry does not exist"); }

            _context.Registries.Remove(rg);
            await _context.SaveChangesAsync();

            return Ok("Registry sucessfully removed");
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<Registry>> EditRegistry(int id, Registry registry)
        {
            if (id != registry.Id)
            { return BadRequest("Id not match"); }

            var rg = await _context.Registries.FindAsync(id);

            if (rg == null)
            { return BadRequest("This registry does not exist"); }

            Employee emp = await _context.Employees.FindAsync(registry.EmployeeId);
            if (emp == null)
            { return BadRequest("Unregistered employee"); }

            rg.Id = registry.Id;
            rg.DateTime = registry.DateTime;
            rg.Type = registry.Type;
            rg.EmployeeId = registry.EmployeeId;
            rg.Employee = emp;

            await _context.SaveChangesAsync();
            return Ok(rg);
        }
    }
}

using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceRecognitionAPI.Controllers {

    [ApiController]
    [Route("[controller]")]
    public class EmployeeController : ControllerBase {

        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            this._context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employee>>> ListEmployees()
        {
            List<Employee> list = await _context.Employees
                .Include(a => a.Registries)
                .OrderByDescending(a => a.Id)
                .Select(x => new Employee
                {
                    Id = x.Id,
                    Name = x.Name,
                    Contact = x.Contact,
                    CodPostal = x.CodPostal,
                    DataNasc = x.DataNasc,
                    Email = x.Email,
                    Morada = x.Morada,
                    Pais = x.Pais,
                    Sexo = x.Sexo,
                    Registries = x.Registries,
                })
                .ToListAsync();

            if (!list.Any())
            { return BadRequest("There are no registered employees"); }

            return Ok(list);
        }

        [HttpGet("{Id}")]
        public async Task<ActionResult<Employee>> GetEmployee(int Id)
        {
            Employee emp = await _context.Employees
                .Include(a => a.Registries)
                .OrderByDescending(a => a.Id)
                .Select(x => new Employee
                {
                    Id = x.Id,
                    Name = x.Name,
                    Contact = x.Contact,
                    CodPostal = x.CodPostal,
                    DataNasc = x.DataNasc,
                    Email = x.Email,
                    Morada = x.Morada,
                    Pais = x.Pais,
                    Sexo = x.Sexo,
                    Registries = x.Registries,
                })
                .Where(a => a.Id == Id)
                .FirstOrDefaultAsync();

            if (emp == null)
            { return NotFound("Unregistered employee"); }

            return Ok(emp);
        }

        [HttpPost]
        public async Task<ActionResult<Employee>> AddEmployee(Employee emp)
        {
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            return Ok(emp);
        }

        [HttpDelete("{Id}")]
        public async Task<IActionResult> DeleteEmployee(int Id)
        {
            var emps = await _context.Employees.FindAsync(Id);
            if (emps == null)
            { return BadRequest("Unregistered employee"); }

            var regs = await _context.Registries.Where(a => a.EmployeeId == Id).ToListAsync();

            if (regs.Any())
            {
                _context.Registries.RemoveRange(regs);
            }

            _context.Employees.Remove(emps);
            await _context.SaveChangesAsync();
            return Ok("Employee successfully removed");
        }

        [HttpPut("{Id}")]
        public async Task<ActionResult<Employee>> EditEmployee(int Id, Employee emp)
        {
            if (Id != emp.Id)
            { return BadRequest("Id not match"); }

            var empBD = await _context.Employees.FindAsync(Id);

            if (empBD == null)
            { return BadRequest("Unregistered employee"); }

            empBD.Id = emp.Id;
            empBD.Name = emp.Name;
            empBD.Contact = emp.Contact;
            empBD.CodPostal = emp.CodPostal;
            empBD.DataNasc = emp.DataNasc;
            empBD.Email = emp.Email;
            empBD.Morada = emp.Morada;
            empBD.Pais = emp.Pais;
            empBD.Sexo = emp.Sexo;

            await _context.SaveChangesAsync();
            return Ok(empBD);
        }
    }
}

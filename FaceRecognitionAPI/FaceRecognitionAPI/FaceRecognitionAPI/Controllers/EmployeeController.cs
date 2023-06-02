using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime.SharedInterfaces;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Text;

namespace FaceRecognitionAPI.Controllers {

    [ApiController]
    [Route("[controller]")]
    public class EmployeeController : ControllerBase {

        private readonly ApplicationDbContext _context;
        private readonly IAmazonS3 _amazonS3Client;
        private readonly IConfiguration _configuration;
        private readonly AmazonDynamoDBClient _dynamoDbClient;

        public EmployeeController(ApplicationDbContext context, IConfiguration config)
        {
            this._context = context;
            this._configuration = config;
            this._amazonS3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(_configuration["AWS:Region"]));

            var configDB = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_configuration["AWS:Region"])
            };

            _dynamoDbClient = new AmazonDynamoDBClient(configDB);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employee>>> ListEmployees()
        {
            List<Employee> list = await _context.Employees
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
                })
                .Where(a => a.Id == Id)
                .FirstOrDefaultAsync();

            if (emp == null)
            { return NotFound("Unregistered employee"); }

            return Ok(emp);
        }

        [Consumes("multipart/form-data")]
        [HttpPost]
        public async Task<ActionResult<Employee>> AddEmployee(IFormFile image, [FromForm] Employee emp)
        {
            emp.Id = 0;
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            if( !await UploadImageAWSS3(image, emp.Name, emp.Id))
            {
                await DeleteEmployee(emp.Id);
                return StatusCode(500, "Error sending image to AWS");
            }           

            return Ok(emp);
        }

        [HttpDelete("{Id}")]
        public async Task<IActionResult> DeleteEmployee(int Id)
        {
            var emp = await _context.Employees.FindAsync(Id);
            if (emp == null)
            { return BadRequest("Unregistered employee"); }

            if(!await RemoveDynamoDBItem("EmployeeId", Id.ToString()))
            {
                return StatusCode(500, "An error occurred when deleting a record from DynamoDB");
            }

            await RemoveAWSS3Item(RemoveAccents(emp.Name).Replace(' ', '_') + "-" + Id.ToString());


            var regs = await _context.Registries.Where(a => a.EmployeeId == Id).ToListAsync();

            if (regs.Any())
            {
                _context.Registries.RemoveRange(regs);
            }

            _context.Employees.Remove(emp);
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

        private async Task<Boolean> UploadImageAWSS3(IFormFile imageFile, string name, int employeeId)
        {
            if (imageFile == null || imageFile.Length == 0 || string.IsNullOrEmpty(name)|| employeeId < 0)
            {
                return false;
            }

            var fullName = RemoveAccents(name);
            var memoryStream = new MemoryStream();

            await imageFile.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var fileTransferUtility = new TransferUtility(_amazonS3Client);
            var fileTransferRequest = new TransferUtilityUploadRequest
            {
                BucketName = _configuration["AWS:BucketName"],
                Key = $"index/{fullName.Replace(' ','_')}-{employeeId}",
                InputStream = memoryStream
            };

            fileTransferRequest.Metadata.Add("FullName", fullName);
            fileTransferRequest.Metadata.Add("EmployeeId", employeeId+"");

            await fileTransferUtility.UploadAsync(fileTransferRequest);

            return true;
        }

        private async Task<Boolean> RemoveDynamoDBItem(string attribute, string value)
        {
            var table = Table.LoadTable(_dynamoDbClient, _configuration["AWS:DynamoDBTable"]);

            var filter = new ScanFilter();
            filter.AddCondition(attribute, ScanOperator.Equal, value);

            var search = table.Scan(filter);

            var record = await search.GetNextSetAsync();

            if (record.Count > 0)
            {
                var deleteItemOperation = table.DeleteItemAsync(record[0]);
                deleteItemOperation.Wait();

                return true;
            }
            return false;
        }

        private async Task RemoveAWSS3Item(string imageName)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _configuration["AWS:BucketName"],
                Key = $"index/{imageName}"
            };

            await _amazonS3Client.DeleteObjectAsync(deleteRequest);
        }

        private static string RemoveAccents(String str)
        {
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(str);
            return Encoding.ASCII.GetString(bytes);
        }

    }


}


﻿using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.DTO;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace FaceRecognitionAPI.Controllers {

    [ApiController]
    [Route("[controller]")]
    public class EmployeeController : ControllerBase {

        //variavel utilizada para comunicação com a base de dados 
        private readonly ApplicationDbContext _context;

        //variaveis para comunicação com os serviços da AWS
        private readonly IAmazonS3 _amazonS3Client;
        private readonly IConfiguration _configuration;
        private readonly AmazonDynamoDBClient _dynamoDbClient;

        /// <summary>
        /// Construtor da classe 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="config"></param>
        public EmployeeController(ApplicationDbContext context, IConfiguration config)
        {
            this._context = context;

            this._configuration = config;

            // Configurar manualmente as credenciais da AWS
            var awsAccessKeyId = _configuration["AWS:AccessKeyId"];
            var awsSecretAccessKey = _configuration["AWS:SecretAccessKey"];
            var awsRegion = _configuration["AWS:Region"];

            var credentials = new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);
            this._amazonS3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(awsRegion));
            this._dynamoDbClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(awsRegion));
        }

        /// <summary>
        /// Listar todos os funcionários
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [HttpGet]
        public async Task<ActionResult<List<EmployeeDTO>>> ListEmployees()
        {
            List<EmployeeDTO> list = await _context.Employees
                .OrderByDescending(a => a.Id)
                .Select(x => new EmployeeDTO
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
                    Image = _configuration["AWS:URLBucket"] + $"{x.Id}"
                })
                .ToListAsync();

            if (!list.Any())
            { return BadRequest("There are no registered employees"); }

            return Ok(list);
        }

        /// <summary>
        /// Obter os dados de um funcionário especifico
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [HttpGet("{Id}")]
        public async Task<ActionResult<EmployeeDTO>> GetEmployee(int Id)
        {
            Employee emp = await _context.Employees.FindAsync(Id);

            if (emp == null)
            { return BadRequest("Unregistered employee"); }

            EmployeeDTO dto = new EmployeeDTO
            {
                Id = emp.Id,
                Name = emp.Name,
                Contact = emp.Contact,
                CodPostal = emp.CodPostal,
                DataNasc = emp.DataNasc,
                Email = emp.Email,
                Morada = emp.Morada,
                Pais = emp.Pais,
                Sexo = emp.Sexo,
                Image = _configuration["AWS:URLBucket"] + $"{emp.Id}",
            };

            return Ok(dto);
        }

        /// <summary>
        /// Adicionar um novo funcionário ao sistema
        /// </summary>
        /// <param name="image"></param>
        /// <param name="emp"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [HttpPost]
        public async Task<ActionResult<EmployeeDTO>> AddEmployee(IFormFile image, [FromForm] Employee emp)
        {
            emp.Id = 0;
            _context.Employees.Add(emp);
            await _context.SaveChangesAsync();

            if (!await UploadImageAWSS3(image, emp.Name, emp.Id))
            {
                await DeleteEmployee(emp.Id);
                await _context.SaveChangesAsync();
                return StatusCode(500, "Error sending image to AWS");
            }

            string imgName = $"{emp.Id}";
            EmployeeDTO dto = new EmployeeDTO
            {
                Id = emp.Id,
                Name = emp.Name,
                Contact = emp.Contact,
                CodPostal = emp.CodPostal,
                DataNasc = emp.DataNasc,
                Email = emp.Email,
                Morada = emp.Morada,
                Pais = emp.Pais,
                Sexo = emp.Sexo,
                Image = _configuration["AWS:URLBucket"] + imgName,
            };

            return Ok(dto);
        }

        /// <summary>
        /// Eliminar um funcionário especifico do sistema
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{Id}")]
        public async Task<IActionResult> DeleteEmployee(int Id)
        {
            var emp = await _context.Employees.FindAsync(Id);
            if (emp == null)
            { return BadRequest("Unregistered employee"); }

            if (!await RemoveDynamoDBItem("EmployeeId", Id.ToString()))
            {
                return StatusCode(500, "An error occurred when deleting a record from DynamoDB");
            }

            await RemoveAWSS3Item(Id.ToString());


            var regs = await _context.Registries.Where(a => a.EmployeeId == Id).ToListAsync();

            if (regs.Any())
            {
                _context.Registries.RemoveRange(regs);
            }

            _context.Employees.Remove(emp);
            await _context.SaveChangesAsync();
            return Ok("Employee successfully removed");
        }

        /// <summary>
        /// Editar os dados de um funcionário
        /// </summary>
        /// <param name="image"></param>
        /// <param name="Id"></param>
        /// <param name="emp"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [HttpPut("{Id}")]
        public async Task<ActionResult<EmployeeDTO>> EditEmployee(IFormFile image ,int Id, [FromForm] Employee emp)
        {

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

            if(image != null)
            {
                if(await RemoveDynamoDBItem("EmployeeId", Id.ToString()))
                {
                    await RemoveAWSS3Item(Id.ToString());
                    await UploadImageAWSS3(image, empBD.Name, empBD.Id);
                }
            }

            string imgName = $"{emp.Id}";

            EmployeeDTO dto = new EmployeeDTO
            {
                Id = empBD.Id,
                Name = empBD.Name,
                Contact = empBD.Contact,
                CodPostal = empBD.CodPostal,
                DataNasc = empBD.DataNasc,
                Email = empBD.Email,
                Morada = empBD.Morada,
                Pais = empBD.Pais,
                Sexo = empBD.Sexo,
                Image = _configuration["AWS:URLBucket"] + imgName,
            };

            return Ok(dto);
        }

        /// <summary>
        /// Função para fazer upload de imagens para o Amazon S3
        /// </summary>
        /// <param name="imageFile"></param>
        /// <param name="name"></param>
        /// <param name="employeeId"></param>
        /// <returns></returns>
        private async Task<Boolean> UploadImageAWSS3(IFormFile imageFile, string name, int employeeId)
        {
            if (imageFile == null || imageFile.Length == 0 || string.IsNullOrEmpty(name) || employeeId < 0)
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
                Key = $"index/{employeeId}",
                InputStream = memoryStream
            };

            fileTransferRequest.Metadata.Add("FullName", fullName);
            fileTransferRequest.Metadata.Add("EmployeeId", employeeId + "");

            await fileTransferUtility.UploadAsync(fileTransferRequest);

            return true;
        }

        /// <summary>
        /// Função para remover um registo da tabela da DynamoDB
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Função para remover item do Amazon S3
        /// </summary>
        /// <param name="imageName"></param>
        /// <returns></returns>
        private async Task RemoveAWSS3Item(string imageName)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _configuration["AWS:BucketName"],
                Key = $"index/{imageName}"
            };

            await _amazonS3Client.DeleteObjectAsync(deleteRequest);
        }

        /// <summary>
        /// Função para remover os acentos de uma string
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string RemoveAccents(String str)
        {
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(str);
            return Encoding.ASCII.GetString(bytes);
        }

    }


}


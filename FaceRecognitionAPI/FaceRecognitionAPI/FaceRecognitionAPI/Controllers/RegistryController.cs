using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.DTO;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace FaceRecognitionAPI.Controllers {
    [Route("[controller]")]
    [ApiController]
    public class RegistryController : ControllerBase {
        //variavel utilizada para comunicação com a base de dados 
        private readonly ApplicationDbContext _context;

        //variaveis para comunicação com os serviços da AWS
        private readonly IConfiguration _configuration;
        private readonly AmazonRekognitionClient _rekognitionClient;
        private readonly AmazonDynamoDBClient _dynamoDbClient;

        /// <summary>
        /// construtor da classe
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        public RegistryController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            // Configurar manualmente as credenciais da AWS
            var awsAccessKeyId = _configuration["AWS:AccessKeyId"];
            var awsSecretAccessKey = _configuration["AWS:SecretAccessKey"];
            var awsRegion = _configuration["AWS:Region"];
            var credentials = new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);

            _rekognitionClient = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(_configuration["AWS:Region"]));
            this._dynamoDbClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(awsRegion));

        }

        /// <summary>
        /// Listar todos os registos
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [HttpGet]
        public async Task<ActionResult<List<RegistryDTO>>> ListRegistries()
        {
            List<RegistryDTO> registries = await _context.Registries
                .Include(a => a.Employee)
                .OrderByDescending(a => a.DateTime)
                .Select(x => new RegistryDTO
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    Type = x.Type,
                    EmployeeId = x.EmployeeId,
                    Employee = new EmployeeDTO
                    {
                        Id = x.Employee.Id,
                        Name = x.Employee.Name,
                        Contact = x.Employee.Contact,
                        CodPostal = x.Employee.CodPostal,
                        DataNasc = x.Employee.DataNasc,
                        Email = x.Employee.Email,
                        Morada = x.Employee.Morada,
                        Pais = x.Employee.Pais,
                        Sexo = x.Employee.Sexo,
                        Image = _configuration["AWS:URLBucket"] + $"{x.Employee.Id}",
                    },
                })
                .ToListAsync();

            if (registries.Any())
            { return Ok(registries); }
            return BadRequest("There are no Registries");
        }

        /// <summary>
        /// Listar os registos de um funcionário especifico
        /// </summary>
        /// <param name="employeeId"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [HttpGet("employee/{employeeId}")]
        public async Task<ActionResult<List<RegistryDTO>>> ListRegistriesEmployee(int employeeId)
        {

            if (await _context.Employees.FindAsync(employeeId) == null)
            { return BadRequest("Unregistered employee"); }

            List<RegistryDTO> list = await _context.Registries
                .Include(a => a.Employee)
                .OrderByDescending(a => a.DateTime)
                .Select(x => new RegistryDTO
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    Type = x.Type,
                    EmployeeId = x.EmployeeId,
                    Employee = new EmployeeDTO
                    {
                        Id = x.Employee.Id,
                        Name = x.Employee.Name,
                        Contact = x.Employee.Contact,
                        CodPostal = x.Employee.CodPostal,
                        DataNasc = x.Employee.DataNasc,
                        Email = x.Employee.Email,
                        Morada = x.Employee.Morada,
                        Pais = x.Employee.Pais,
                        Sexo = x.Employee.Sexo,
                        Image = _configuration["AWS:URLBucket"] + $"{x.Employee.Id}",
                    },
                }).Where(a => a.EmployeeId == employeeId)
                .ToListAsync();

            if (list.Any())
            { return Ok(list); }
            return BadRequest("There are no Registries for this employee");
        }

        /// <summary>
        /// Obter dados de um registo específico
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin, User")]
        [HttpGet("{id}")]
        public async Task<ActionResult<RegistryDTO>> GetRegistry(int id)
        {
            Registry rgs = await _context.Registries.FindAsync(id);

            RegistryDTO aux = new RegistryDTO
            {
                Id = rgs.Id,
                DateTime = rgs.DateTime,
                Type = rgs.Type,
                EmployeeId = rgs.EmployeeId,
                Employee = new EmployeeDTO
                {
                    Id = rgs.Employee.Id,
                    Name = rgs.Employee.Name,
                    Contact = rgs.Employee.Contact,
                    CodPostal = rgs.Employee.CodPostal,
                    DataNasc = rgs.Employee.DataNasc,
                    Email = rgs.Employee.Email,
                    Morada = rgs.Employee.Morada,
                    Pais = rgs.Employee.Pais,
                    Sexo = rgs.Employee.Sexo,
                    Image = _configuration["AWS:URLBucket"] + $"{rgs.Employee.Id}",
                },
            };

            if (rgs == null)
            { return BadRequest("This registry does not exist"); }

            return Ok(aux);
        }

        /// <summary>
        /// Adicionar um registo de forma manual, sem ser por reconhecimento facial
        /// </summary>
        /// <param name="registry"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [HttpPost("manual")]
        public async Task<ActionResult<Registry>> AddRegistry([FromForm]Registry registry)
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

        /// <summary>
        /// Adicionar um registo através do reconhecimento facial
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult<List<Registry>>> AddRegistry(IFormFile img)
        {
            var empsId = await RecognizeFace(img);

            if (empsId == null)
            { return BadRequest("No known faces found"); }

            List<Registry> list = new List<Registry>();

            foreach (var empId in empsId)
            {
                Registry lastRegistry = await _context.Registries
                .Include(a => a.Employee)
                .OrderByDescending(a => a.DateTime)
                .Select(x => new Registry
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    Type = x.Type,
                    EmployeeId = x.EmployeeId,
                    Employee = x.Employee,
                })
                .Where(a => a.EmployeeId == empId)
                .FirstOrDefaultAsync();

                String type = "E";

                if (lastRegistry is null)
                { type = "E"; }
                else if (lastRegistry.Type == "E" || lastRegistry.Type == "e")
                { type = "S"; }
                else if(lastRegistry.Type == "S" || lastRegistry.Type == "s")
                { type = "E"; }

                Registry rgs = new Registry
                {
                    DateTime = DateTime.Now,
                    Type = type,
                    EmployeeId = empId,
                    Employee = await _context.Employees.FindAsync(empId),
                };
                list.Add(rgs);
                _context.Registries.Add(rgs);
            }
            await _context.SaveChangesAsync();
            return Ok(list);
        }

        /// <summary>
        /// Eliminar um registo de ponto
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
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

        /// <summary>
        /// Editar um registo
        /// </summary>
        /// <param name="id"></param>
        /// <param name="registry"></param>
        /// <returns></returns>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<ActionResult<RegistryDTO>> EditRegistry(int id, [FromForm]Registry registry)
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

            RegistryDTO dto = new RegistryDTO
            {
                Id = rg.Id,
                DateTime = rg.DateTime,
                Type = rg.Type,
                EmployeeId = rg.EmployeeId,
                Employee = new EmployeeDTO
                {
                    Id = rg.Employee.Id,
                    Name = rg.Employee.Name,
                    Contact = rg.Employee.Contact,
                    CodPostal = rg.Employee.CodPostal,
                    DataNasc = rg.Employee.DataNasc,
                    Email = rg.Employee.Email,
                    Morada = rg.Employee.Morada,
                    Pais = rg.Employee.Pais,
                    Sexo = rg.Employee.Sexo,
                    Image = _configuration["AWS:URLBucket"] + $"{rg.Employee.Id}",
                },
            };

            await _context.SaveChangesAsync();
            return Ok(dto);
        }

        /// <summary>
        /// Reconhecer uma face em uma imagem usando Amazon Rekognition
        /// </summary>
        /// <param name="imageFile"></param>
        /// <returns></returns>
        private async Task<List<int>> RecognizeFace(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null ||
                    imageFile.Length == 0 ||
                    !(imageFile.FileName.EndsWith(".jpeg") ||
                    imageFile.FileName.EndsWith(".png") ||
                    imageFile.FileName.EndsWith(".jpg")))
                {
                    return null;
                }
                var memoryStream = new MemoryStream();
                await imageFile.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                var rekognitionRequest = new SearchFacesByImageRequest
                {
                    CollectionId = _configuration["AWS:RekognitionCollectionId"],
                    Image = new Image
                    {
                        Bytes = new MemoryStream(imageBytes)
                    }
                };
                var rekognitionResponse = await _rekognitionClient.SearchFacesByImageAsync(rekognitionRequest);

                if (rekognitionResponse.FaceMatches.Count > 0)
                {
                    // Processar os resultados dos matches e retornar os IDs dos funcionários correspondentes
                    List<int> emps = new List<int>();

                    foreach (var match in rekognitionResponse.FaceMatches)
                    {
                        List<Dictionary<string, AttributeValue>> items = await SearchItems("RekognitionId", match.Face.FaceId);

                        foreach (var item in items)
                        {
                            foreach (var attribute in item)
                            {
                                string attributeName = attribute.Key;
                                AttributeValue attributeValue = attribute.Value;

                                if (attributeName == "EmployeeId")
                                {
                                    if (attributeValue.S != null)
                                    {
                                        int attributeStringValue = int.Parse(attributeValue.S);
                                        emps.Add(attributeStringValue);
                                    }
                                }
                            }
                        }
                    }

                    return emps;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Procurar itens na tabela da DynamoDB
        /// </summary>
        /// <param name="attribute"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private async Task<List<Dictionary<string, AttributeValue>>> SearchItems(string attribute, string value)
        {
            var request = new ScanRequest
            {
                TableName = _configuration["AWS:DynamoDBTable"],
                FilterExpression = $"#{attribute} = :value",
                ExpressionAttributeNames = new Dictionary<string, string>
            {
                { $"#{attribute}", attribute }
            },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":value", new AttributeValue { S = value } }
            }
            };

            var response = await _dynamoDbClient.ScanAsync(request);

            return response.Items;

        }
    }
}

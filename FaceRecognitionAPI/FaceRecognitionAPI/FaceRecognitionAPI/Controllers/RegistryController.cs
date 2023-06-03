
﻿using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using FaceRecognitionAPI.Data;
using FaceRecognitionAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace FaceRecognitionAPI.Controllers {
    [Route("[controller]")]
    [ApiController]
    public class RegistryController : ControllerBase {

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly AmazonRekognitionClient _rekognitionClient;
        private readonly AmazonDynamoDBClient _dynamoDbClient;

        public RegistryController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.GetBySystemName(_configuration["AWS:Region"]));

            var configDB = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(_configuration["AWS:Region"])
            };

            _dynamoDbClient = new AmazonDynamoDBClient(configDB);

        }

        [HttpGet]
        public async Task<ActionResult<List<Registry>>> ListRegistries()
        {
            List<Registry> registries = await _context.Registries
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

            if (await _context.Employees.FindAsync(employeeId) == null)
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


        [HttpPost("manual")]
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
                    // Process the face matches and return the results
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

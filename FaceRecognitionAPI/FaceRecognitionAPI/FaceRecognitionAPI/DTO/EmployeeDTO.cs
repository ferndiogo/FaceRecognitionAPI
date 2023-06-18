using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FaceRecognitionAPI.DTO {
    public class EmployeeDTO {
        /// <summary>
        /// PK do funcionario
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome do funcionario
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Contacto do funcionario
        /// </summary>
        public String Contact { get; set; }

        /// <summary>
        /// Email do funcionario
        /// </summary>
        public String Email { get; set; }

        /// <summary>
        /// Morada do funcionario
        /// </summary>
        public string Morada { get; set; }

        /// <summary>
        /// País do funcionario
        /// </summary>
        public string Pais { get; set; }

        /// <summary>
        /// Código Postal do funcionario
        /// </summary>
        public string CodPostal { get; set; }

        /// <summary>
        /// Sexo do funcionario
        /// </summary>
        public string Sexo { get; set; }

        /// <summary>
        /// Data de nascimento do funcionario
        /// </summary>
        public DateTime DataNasc { get; set; }

        /// <summary>
        /// Imagem do Funcionário
        /// </summary>
        public String Image { get; set; }


    }
}

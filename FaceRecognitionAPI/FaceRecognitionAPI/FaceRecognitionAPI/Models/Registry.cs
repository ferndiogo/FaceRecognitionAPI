using System.ComponentModel.DataAnnotations;

namespace FaceRecognitionAPI.Models {
    public class Registry {
        /// <summary>
        /// PK do Registo
        /// </summary>
        [Required]
        public int Id { get; set; }

        /// <summary>
        /// Regista a hora e data do registo
        /// </summary>
        [Required]
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Regista se o registo foi de entrada ou saida
        /// </summary>
        [Required]
        [RegularExpression("[EeSs]", ErrorMessage = "You can only use E for Input, or S for Output, in the Type field")]
        public string Type { get; set; } //Entrada ou Saida

        /// <summary>
        /// FK para o funcionário
        /// </summary>
        [Required]
        public int EmployeeId { get; set; }

        /// <summary>
        /// Referencia para o Funcionário
        /// </summary>
        public Employee Employee { get; set; }
    }
}

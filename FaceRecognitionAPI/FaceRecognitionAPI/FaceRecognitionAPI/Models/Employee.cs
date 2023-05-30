using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace FaceRecognitionAPI.Models {
    public class Employee {

        /// <summary>
        /// PK do funcionario
        /// </summary>
        [Required]
        public int Id { get; set; }

        /// <summary>
        /// Nome do funcionario
        /// </summary>
        [StringLength(32, ErrorMessage = "O {0} não pode ter mais do que {1} carateres.")]
        [Display(Name = "Nome")]
        [Required(ErrorMessage = "O {0} é de preenchimento obrigatório.")]
        public String Name { get; set; }

        /// <summary>
        /// Contacto do funcionario
        /// </summary>
        [Display(Name = "Contacto")]
        [Required(ErrorMessage = "O {0} é de preenchimento obrigatório.")]
        [RegularExpression("[2,8,7,9]{1}[0-9]{8}", ErrorMessage = "Insira um {0} válido.")]
        public String Contact { get; set; }

        /// <summary>
        /// Email do funcionario
        /// </summary>
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage ="Insira um {0} válido.")]
        public String Email { get; set; }

        /// <summary>
        /// Morada do funcionario
        /// </summary>
        [Display(Name = "Morada")]
        [Required(ErrorMessage = "A {0} é de preenchimento obrigatório.")]
        public string Morada { get; set; }

        /// <summary>
        /// País do funcionario
        /// </summary>
        [Required(ErrorMessage = "O {0} é de preenchimento obrigatório.")]
        [Display(Name = "País")]
        public string Pais { get; set; }

        /// <summary>
        /// Código Postal do funcionario
        /// </summary>
        [Required(ErrorMessage = "O {0} é de preenchimento obrigatório.")]
        [Display(Name = "Código Postal")]
        [RegularExpression("[0-9]{4}[-][0-9]{3}", ErrorMessage = "O {0} deve ter o seguinte formato: xxxx-xxx.")]
        public string CodPostal { get; set; }

        /// <summary>
        /// Sexo do funcionario
        /// </summary>
        [Required(ErrorMessage = "O {0} é de preenchimento obrigatório.")]
        [Display(Name = "Sexo")]
        [RegularExpression("[MmFf]", ErrorMessage = "Só pode usar F, ou M, no campo {0}")]
        public string Sexo { get; set; }

        /// <summary>
        /// Data de nascimento do funcionario
        /// </summary>
        [Required(ErrorMessage = "A {0} é de preenchimento obrigatório.")]
        [Display(Name = "Data de Nascimento")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [DataType(DataType.Date)]
        public DateTime DataNasc { get; set; }

        /// <summary>
        /// Coleção de registos do funcionrio
        /// </summary>
        public ICollection<Registry> Registries { get; set; }
    }

}
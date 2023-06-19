namespace FaceRecognitionAPI.DTO
{
    public class UserDTO
    {
        /// <summary>
        /// PK da tabela dos utilizadores
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome do utilizador
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Token para autenticação
        /// </summary>
        public DateTime TokenCreated { get; set; }

        /// <summary>
        /// Regra do utilizador
        /// </summary>
        public String Role { get; set; }
    }
}

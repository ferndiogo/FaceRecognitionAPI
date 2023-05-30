namespace FaceRecognitionAPI.Models
{
    public class User
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
        /// HASH da password
        /// </summary>
        public byte[] PasswordHash { get; set; }

        /// <summary>
        /// Sequência aleatória de caracteres que é adicionada à password antes de ser armazenada
        /// </summary>
        public byte[] PasswordSalt { get; set; }

        /// <summary>
        /// Token para autenticação
        /// </summary>
        public DateTime TokenCreated { get; set; }

        /// <summary>
        /// Data e hora de expiração do token
        /// </summary>
        public DateTime TokenExpires { get; set; }

        /// <summary>
        /// Regra do utilizador
        /// </summary>
        public String Role { get; set; }
    }
}

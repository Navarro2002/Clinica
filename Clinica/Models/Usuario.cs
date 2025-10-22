using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Clinica.Models
{
    public class Usuario
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroDocumentoIdentidad { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        [Required]
        [StringLength(50)]
        public string Apellido { get; set; }

        [Required]
        [StringLength(50)]
        public string Correo { get; set; }

        [Required]
        [StringLength(64)] // SHA-256 en HEX = 64 caracteres
        public string Clave { get; set; }

        public int? IdRolUsuario { get; set; }

        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdRolUsuario")]
        public virtual RolUsuario Rol { get; set; }
    }
}
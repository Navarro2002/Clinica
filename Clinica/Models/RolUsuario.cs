using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Clinica.Models
{
    public class RolUsuario
    {
        [Key]
        public int IdRolUsuario { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        public DateTime? FechaCreacion { get; set; }

        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Clinica.Models
{
    public class EstadoCita
    {
        [Key]
        public int IdEstadoCita { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        public DateTime? FechaCreacion { get; set; }

        public virtual ICollection<Cita> Citas { get; set; }
    }
}

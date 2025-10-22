using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clinica.Models
{
    public class Doctor
    {
        [Key]
        public int IdDoctor { get; set; }

        [Required]
        [StringLength(50)]
        public string NumeroDocumentoIdentidad { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombres { get; set; }

        [Required]
        [StringLength(50)]
        public string Apellidos { get; set; }

        [StringLength(1)]
        public string Genero { get; set; }

        public int? IdEspecialidad { get; set; }

        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdEspecialidad")]
        public virtual Especialidad Especialidad { get; set; }

        public virtual ICollection<DoctorHorario> Horarios { get; set; }
    }
}

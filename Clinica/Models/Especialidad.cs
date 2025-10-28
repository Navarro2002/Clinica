using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Clinica.Models
{
    public class Especialidad
    {
        [Key]
        public int IdEspecialidad { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(50, ErrorMessage = "El nombre no puede exceder los 50 caracteres")]
        [UniqueEspecialidadName(ErrorMessage = "Ya existe una especialidad con este nombre")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime? FechaCreacion { get; set; }

        public virtual ICollection<Doctor> Doctores { get; set; }
    }
}

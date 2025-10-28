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

        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [StringLength(50, ErrorMessage = "El número de documento no puede exceder los 50 caracteres")]
        [Display(Name = "Número de Documento")]
        public string NumeroDocumentoIdentidad { get; set; }

        [Required(ErrorMessage = "Los nombres son obligatorios")]
        [StringLength(50, ErrorMessage = "Los nombres no pueden exceder los 50 caracteres")]
        [Display(Name = "Nombres")]
        public string Nombres { get; set; }

        [Required(ErrorMessage = "Los apellidos son obligatorios")]
        [StringLength(50, ErrorMessage = "Los apellidos no pueden exceder los 50 caracteres")]
        [Display(Name = "Apellidos")]
        public string Apellidos { get; set; }

        [Required(ErrorMessage = "El género es obligatorio")]
        [StringLength(1, ErrorMessage = "El género debe ser un solo carácter")]
        [Display(Name = "Género")]
        public string Genero { get; set; }

        [Required(ErrorMessage = "La especialidad es obligatoria")]
        [Display(Name = "Especialidad")]
        public int? IdEspecialidad { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdEspecialidad")]
        public virtual Especialidad Especialidad { get; set; }

        public virtual ICollection<DoctorHorario> Horarios { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clinica.Models
{
    public class DoctorHorario
    {
        [Key]
        public int IdDoctorHorario { get; set; }

        [Required(ErrorMessage = "El doctor es obligatorio")]
        [Display(Name = "Doctor")]
        public int? IdDoctor { get; set; }

        [Required(ErrorMessage = "El mes es obligatorio")]
        [Display(Name = "Mes")]
        public int? NumeroMes { get; set; }

        [Required(ErrorMessage = "La hora de inicio AM es obligatoria")]
        [Display(Name = "Hora Inicio AM")]
        public TimeSpan? HoraInicioAM { get; set; }

        [Required(ErrorMessage = "La hora de fin AM es obligatoria")]
        [Display(Name = "Hora Fin AM")]
        public TimeSpan? HoraFinAM { get; set; }

        [Required(ErrorMessage = "La hora de inicio PM es obligatoria")]
        [Display(Name = "Hora Inicio PM")]
        public TimeSpan? HoraInicioPM { get; set; }

        [Required(ErrorMessage = "La hora de fin PM es obligatoria")]
        [Display(Name = "Hora Fin PM")]
        public TimeSpan? HoraFinPM { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdDoctor")]
        public virtual Doctor Doctor { get; set; }

        public virtual ICollection<DoctorHorarioDetalle> Detalles { get; set; } = new HashSet<DoctorHorarioDetalle>();
    }
}

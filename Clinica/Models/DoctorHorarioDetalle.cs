using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clinica.Models
{
    public class DoctorHorarioDetalle
    {
        [Key]
        public int IdDoctorHorarioDetalle { get; set; }

        public int? IdDoctorHorario { get; set; }

        public DateTime? Fecha { get; set; }

        [StringLength(2)]
        public string Turno { get; set; }

        public TimeSpan? TurnoHora { get; set; }

        public bool? Reservado { get; set; }

        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdDoctorHorario")]
        public virtual DoctorHorario DoctorHorario { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Clinica.Models
{
    public class Cita
    {
        [Key]
        public int IdCita { get; set; }

        public int? IdUsuario { get; set; }

        public int? IdDoctorHorarioDetalle { get; set; }

        public int? IdEstadoCita { get; set; }

        public DateTime? FechaCita { get; set; }

        [StringLength(1000)]
        public string Indicaciones { get; set; }

        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdUsuario")]
        public virtual Usuario Usuario { get; set; }

        [ForeignKey("IdDoctorHorarioDetalle")]
        public virtual DoctorHorarioDetalle DoctorHorarioDetalle { get; set; }

        [ForeignKey("IdEstadoCita")]
        public virtual EstadoCita EstadoCita { get; set; }
    }
}

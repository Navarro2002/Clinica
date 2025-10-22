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

        public int? IdDoctor { get; set; }

        public int? NumeroMes { get; set; }

        public TimeSpan? HoraInicioAM { get; set; }
        public TimeSpan? HoraFinAM { get; set; }
        public TimeSpan? HoraInicioPM { get; set; }
        public TimeSpan? HoraFinPM { get; set; }

        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdDoctor")]
        public virtual Doctor Doctor { get; set; }

        public virtual ICollection<DoctorHorarioDetalle> Detalles { get; set; } = new HashSet<DoctorHorarioDetalle>();
    }
}

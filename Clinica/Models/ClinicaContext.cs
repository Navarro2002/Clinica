using System.Data.Entity;

namespace Clinica.Models
{
    public class ClinicaContext : DbContext
    {
        // Ajusta el nombre de la cadena de conexión en Web.config si cambias este valor
        public ClinicaContext() : base("name=ClinicaConnection")
        {
        }

        public virtual DbSet<RolUsuario> RolUsuarios { get; set; }
        public virtual DbSet<Usuario> Usuarios { get; set; }
        public virtual DbSet<Especialidad> Especialidades { get; set; }
        public virtual DbSet<Doctor> Doctores { get; set; }
        public virtual DbSet<DoctorHorario> DoctorHorarios { get; set; }
        public virtual DbSet<DoctorHorarioDetalle> DoctorHorarioDetalles { get; set; }
        public virtual DbSet<EstadoCita> EstadoCitas { get; set; }
        public virtual DbSet<Cita> Citas { get; set; }

    }
}

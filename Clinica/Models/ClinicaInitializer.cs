using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Security.Cryptography;
using System.Text;

namespace Clinica.Models
{
    public class ClinicaInitializer : CreateDatabaseIfNotExists<ClinicaContext>
    {
        protected override void Seed(ClinicaContext context)
        {
            // Roles
            if (!context.RolUsuarios.Any())
            {
                var roles = new List<RolUsuario>
                {
                    new RolUsuario { Nombre = "Administrador", FechaCreacion = DateTime.Now },
                    new RolUsuario { Nombre = "Doctor", FechaCreacion = DateTime.Now },
                    new RolUsuario { Nombre = "Paciente", FechaCreacion = DateTime.Now }
                };
                context.RolUsuarios.AddRange(roles);
                context.SaveChanges();
            }

            // Estados de Cita
            if (!context.EstadoCitas.Any())
            {
                var estadoCitas = new List<EstadoCita>
                {
                    new EstadoCita { Nombre = "Pendiente", FechaCreacion = DateTime.Now },
                    new EstadoCita { Nombre = "Atendido", FechaCreacion = DateTime.Now },
                    new EstadoCita { Nombre = "Anulado", FechaCreacion = DateTime.Now }
                };
                context.EstadoCitas.AddRange(estadoCitas);
                context.SaveChanges();
            }


            // Usuarios demo
            if (!context.Usuarios.Any())
            {
                var rolAdminId = context.RolUsuarios.Where(r => r.Nombre == "Administrador").Select(r => r.IdRolUsuario).FirstOrDefault();
                var rolDoctorId = context.RolUsuarios.Where(r => r.Nombre == "Doctor").Select(r => r.IdRolUsuario).FirstOrDefault();

                Func<string, string> Hash = pwd =>
                {
                    using (var sha = SHA256.Create())
                    {
                        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(pwd ?? string.Empty))).Replace("-", "");
                    }
                };

                var usuarios = new List<Usuario>
                {
                    new Usuario
                    {
                        NumeroDocumentoIdentidad = "75757575",
                        Nombre = "Jose",
                        Apellido = "Mendez",
                        Correo = "Jose@clinica.pe",
                        Clave = Hash("1234"),
                        IdRolUsuario = rolAdminId,
                        FechaCreacion = DateTime.Now
                    },
                    new Usuario
                    {
                        NumeroDocumentoIdentidad = "74747474",
                        Nombre = "Maria",
                        Apellido = "Espinoza",
                        Correo = "maria@clinica.pe",
                        Clave = Hash("1234"),
                        IdRolUsuario = rolDoctorId,
                        FechaCreacion = DateTime.Now
                    }
                };
                context.Usuarios.AddRange(usuarios);
                context.SaveChanges();
            }

            var especialidades = new List<Especialidad>
            {
                new Especialidad { Nombre = "Psicología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Urología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Pediatría", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Otorrinolaringología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Oftalmología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Neurología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Neumología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Nutrición", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Medicina General", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Gastroenterología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Endocrinología", FechaCreacion = DateTime.Now },
                new Especialidad { Nombre = "Dermatología", FechaCreacion = DateTime.Now }
            };
            context.Especialidades.AddRange(especialidades);
            context.SaveChanges();

            base.Seed(context);
        }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Clinica.Models
{
    public class UniqueEspecialidadNameAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                // Si el nombre está vacío, deja que [Required] lo maneje
                return ValidationResult.Success;
            }

            var nombre = value.ToString().Trim();
            var db = new ClinicaContext();

            try
            {
                // Obtener la especialidad actual si existe
                var especialidad = validationContext.ObjectInstance as Especialidad;
                
                if (especialidad != null && especialidad.IdEspecialidad > 0)
                {
                    // Es una edición, excluir la misma especialidad
                    var existe = db.Especialidades.Any(e => e.Nombre.ToLower() == nombre.ToLower() && e.IdEspecialidad != especialidad.IdEspecialidad);
                    if (existe)
                    {
                        return new ValidationResult(ErrorMessage ?? "Ya existe una especialidad con este nombre");
                    }
                }
                else
                {
                    // Es una creación
                    var existe = db.Especialidades.Any(e => e.Nombre.ToLower() == nombre.ToLower());
                    if (existe)
                    {
                        return new ValidationResult(ErrorMessage ?? "Ya existe una especialidad con este nombre");
                    }
                }

                return ValidationResult.Success;
            }
            finally
            {
                db.Dispose();
            }
        }
    }
}

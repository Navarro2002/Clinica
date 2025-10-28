using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Clinica.Models
{
    public class UniqueEmailAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                // Si el correo est� vac�o, deja que [Required] lo maneje
                return ValidationResult.Success;
            }

            var correo = value.ToString().Trim().ToLower();
            var db = new ClinicaContext();

            try
            {
                // Obtener el usuario actual si existe
                var usuario = validationContext.ObjectInstance as Usuario;
                
                if (usuario != null && usuario.IdUsuario > 0)
                {
                    // Es una edici�n, excluir el mismo usuario
                    var existe = db.Usuarios.Any(u => u.Correo.ToLower() == correo && u.IdUsuario != usuario.IdUsuario);
                    if (existe)
                    {
                        return new ValidationResult(ErrorMessage ?? "Ya existe un usuario con este correo electr�nico");
                    }
                }
                else
                {
                    // Es una creaci�n
                    var existe = db.Usuarios.Any(u => u.Correo.ToLower() == correo);
                    if (existe)
                    {
                        return new ValidationResult(ErrorMessage ?? "Ya existe un usuario con este correo electr�nico");
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

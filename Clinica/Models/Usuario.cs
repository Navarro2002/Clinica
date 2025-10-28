using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Clinica.Models
{
    public class Usuario
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [StringLength(50, ErrorMessage = "El número de documento no puede exceder los 50 caracteres")]
        [UniqueDocument(ErrorMessage = "Ya existe un usuario con este número de documento")]
        [Display(Name = "Nro. Documento")]
        public string NumeroDocumentoIdentidad { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(50, ErrorMessage = "El nombre no puede exceder los 50 caracteres")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(50, ErrorMessage = "El apellido no puede exceder los 50 caracteres")]
        [Display(Name = "Apellido")]
        public string Apellido { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [StringLength(50, ErrorMessage = "El correo no puede exceder los 50 caracteres")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [UniqueEmail(ErrorMessage = "Ya existe un usuario con este correo electrónico")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; }

        [StringLength(64, ErrorMessage = "La contraseña no puede exceder los 64 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Clave { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio")]
        [Display(Name = "Rol")]
        public int? IdRolUsuario { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime? FechaCreacion { get; set; }

        [ForeignKey("IdRolUsuario")]
        public virtual RolUsuario Rol { get; set; }
    }
}
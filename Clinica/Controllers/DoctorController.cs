using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class DoctorController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        private bool UsuarioAutenticado()
        {
            return Session["IdUsuario"] != null;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!UsuarioAutenticado())
            {
                filterContext.Result = RedirectToAction("Index", "Login");
            }
            base.OnActionExecuting(filterContext);
        }

        // GET: Doctor
        public ActionResult Index()
        {
            var doctores = db.Doctores
                .Include(d => d.Especialidad)
                .AsNoTracking()
                .ToList();

            var especialidades = db.Especialidades
                .AsNoTracking()
                .ToList();

            ViewBag.Especialidades = especialidades;
            return View(doctores);
        }

        // POST: Doctor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "NumeroDocumentoIdentidad,Nombres,Apellidos,Genero,IdEspecialidad")] Doctor doctor)
        {
            if (ModelState.IsValid)
            {
                doctor.FechaCreacion = DateTime.Now;
                db.Doctores.Add(doctor);
                db.SaveChanges();

                // Crear usuario automáticamente
                // Busca el rol Doctor
                var rolDoctor = db.RolUsuarios.FirstOrDefault(r => r.Nombre.ToLower() == "doctor");
                int? idRolDoctor = rolDoctor?.IdRolUsuario;
                if (idRolDoctor == null)
                {
                    // Si no existe el rol, crea uno
                    rolDoctor = new RolUsuario { Nombre = "Doctor", FechaCreacion = DateTime.Now };
                    db.RolUsuarios.Add(rolDoctor);
                    db.SaveChanges();
                    idRolDoctor = rolDoctor.IdRolUsuario;
                }
                // Verifica si ya existe usuario con ese documento
                bool existeUsuario = db.Usuarios.Any(u => u.NumeroDocumentoIdentidad == doctor.NumeroDocumentoIdentidad);
                if (!existeUsuario)
                {
                    string clave = doctor.NumeroDocumentoIdentidad;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(clave);
                        clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                    }
                    var usuario = new Usuario
                    {
                        NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad,
                        Nombre = doctor.Nombres,
                        Apellido = doctor.Apellidos,
                        Correo = null, // evita exceder StringLength(50) y es válido según el modelo
                        Clave = clave,
                        IdRolUsuario = idRolDoctor,
                        FechaCreacion = DateTime.Now
                    };
                    try
                    {
                        db.Usuarios.Add(usuario);
                        db.SaveChanges();
                    }
                    catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                    {
                        var errors = ex.EntityValidationErrors
                            .SelectMany(e => e.ValidationErrors)
                            .Select(e => e.PropertyName + ": " + e.ErrorMessage);
                        TempData["Error"] = "Error de validación: " + string.Join(", ", errors);
                        return RedirectToAction("Index");
                    }
                }
                TempData["Success"] = "Doctor y usuario creados correctamente.";
                return RedirectToAction("Index");
            }
            TempData["Error"] = "Datos inválidos.";
            return RedirectToAction("Index");
        }

        // POST: Doctor/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "IdDoctor,NumeroDocumentoIdentidad,Nombres,Apellidos,Genero,IdEspecialidad")] Doctor doctor)
        {
            var existente = db.Doctores.Find(doctor.IdDoctor);
            if (existente == null)
            {
                TempData["Error"] = "Doctor no encontrado.";
                return RedirectToAction("Index");
            }
            // Actualiza datos del doctor
            existente.NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad;
            existente.Nombres = doctor.Nombres;
            existente.Apellidos = doctor.Apellidos;
            existente.Genero = doctor.Genero;
            existente.IdEspecialidad = doctor.IdEspecialidad;
            db.Entry(existente).State = EntityState.Modified;
            db.SaveChanges();

            // Actualiza usuario asociado si existe
            var usuario = db.Usuarios.FirstOrDefault(u => u.NumeroDocumentoIdentidad == doctor.NumeroDocumentoIdentidad);
            if (usuario != null)
            {
                usuario.Nombre = doctor.Nombres;
                usuario.Apellido = doctor.Apellidos;
                usuario.NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad;
                db.Entry(usuario).State = EntityState.Modified;
                db.SaveChanges();
            }
            TempData["Success"] = "Doctor y usuario actualizados correctamente.";
            return RedirectToAction("Index");
        }

        // POST: Doctor/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var doctor = db.Doctores.Find(id);
            if (doctor == null)
                return HttpNotFound();

            db.Doctores.Remove(doctor);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}

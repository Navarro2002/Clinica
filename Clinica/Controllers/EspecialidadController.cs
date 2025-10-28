using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Clinica.Models;
using System.Web;

namespace Clinica.Controllers
{
    public class EspecialidadController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        private bool UsuarioAutenticado()
        {
            return Session["IdUsuario"] != null;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if ((Session["RolUsuario"] as string)?.ToLower() != "administrador")
            {
                filterContext.Result = RedirectToAction("Index", "Home");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // GET: Especialidad
        public ActionResult Index()
        {
            var especialidades = db.Especialidades
                .AsNoTracking()
                .ToList();
            return View(especialidades);
        }

        // POST: Especialidad/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Especialidad especialidad)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            var nombre = (especialidad.Nombre ?? string.Empty).Trim();

            // Validar nombre único
            bool existe = db.Especialidades.Any(e => e.Nombre.ToLower() == nombre.ToLower());
            if (existe)
            {
                return Json(new { success = false, errors = new { Nombre = new[] { "Ya existe una especialidad con este nombre" } } });
            }

            try
            {
                especialidad.Nombre = nombre;
                especialidad.FechaCreacion = DateTime.Now;
                db.Especialidades.Add(especialidad);
                db.SaveChanges();
                return Json(new { success = true, message = "Especialidad creada correctamente." });
            }
            catch (Exception)
            {
                return Json(new { success = false, errors = new { General = new[] { "No se pudo crear la especialidad" } } });
            }
        }

        // POST: Especialidad/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Especialidad especialidad)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            var existente = db.Especialidades.Find(especialidad.IdEspecialidad);
            if (existente == null)
            {
                return Json(new { success = false, errors = new { General = new[] { "Especialidad no encontrada" } } });
            }

            var nombre = (especialidad.Nombre ?? string.Empty).Trim();

            // Nombre único excluyendo la misma especialidad
            bool existeNombre = db.Especialidades.Any(e => e.Nombre.ToLower() == nombre.ToLower() && e.IdEspecialidad != especialidad.IdEspecialidad);
            if (existeNombre)
            {
                return Json(new { success = false, errors = new { Nombre = new[] { "Ya existe una especialidad con este nombre" } } });
            }

            try
            {
                existente.Nombre = nombre;
                db.Entry(existente).State = EntityState.Modified;
                db.SaveChanges();
                return Json(new { success = true, message = "Especialidad editada correctamente." });
            }
            catch (Exception)
            {
                return Json(new { success = false, errors = new { General = new[] { "No se pudo editar la especialidad" } } });
            }
        }

        // POST: Especialidad/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var especialidad = db.Especialidades.Find(id);
            if (especialidad == null)
                return HttpNotFound();

            // Verifica si hay doctores asociados
            bool tieneDoctores = db.Doctores.Any(d => d.IdEspecialidad == id);
            if (tieneDoctores)
            {
                TempData["Error"] = "No se puede eliminar la especialidad porque tiene doctores asociados.";
                return RedirectToAction("Index");
            }

            try
            {
                db.Especialidades.Remove(especialidad);
                db.SaveChanges();
                TempData["Success"] = "Especialidad eliminada correctamente.";
            }
            catch (Exception)
            {
                TempData["Error"] = "No se pudo eliminar la especialidad.";
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
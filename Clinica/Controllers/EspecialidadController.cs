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
        public ActionResult Create([Bind(Include = "Nombre")] Especialidad especialidad)
        {
            var nombre = (especialidad?.Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["Error"] = "El nombre es obligatorio.";
                return RedirectToAction("Index");
            }

            bool existe = db.Especialidades.AsNoTracking().Any(e => e.Nombre.ToLower() == nombre.ToLower());
            if (existe)
            {
                TempData["Error"] = "Ya existe una especialidad con ese nombre.";
                return RedirectToAction("Index");
            }

            try
            {
                especialidad.Nombre = nombre;
                especialidad.FechaCreacion = DateTime.Now;
                db.Especialidades.Add(especialidad);
                db.SaveChanges();
                TempData["Success"] = "Especialidad creada correctamente.";
            }
            catch (Exception)
            {
                TempData["Error"] = "No se pudo crear la especialidad.";
            }
            return RedirectToAction("Index");
        }

        // POST: Especialidad/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int IdEspecialidad, string Nombre)
        {
            var especialidad = db.Especialidades.Find(IdEspecialidad);
            if (especialidad == null)
                return HttpNotFound();

            var nombre = (Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["Error"] = "El nombre es obligatorio.";
                return RedirectToAction("Index");
            }

            bool existe = db.Especialidades.AsNoTracking()
                .Any(e => e.Nombre.ToLower() == nombre.ToLower() && e.IdEspecialidad != IdEspecialidad);
            if (existe)
            {
                TempData["Error"] = "Ya existe una especialidad con ese nombre.";
                return RedirectToAction("Index");
            }

            try
            {
                especialidad.Nombre = nombre;
                db.Entry(especialidad).State = EntityState.Modified;
                db.SaveChanges();
                TempData["Success"] = "Especialidad editada correctamente.";
            }
            catch (Exception)
            {
                TempData["Error"] = "No se pudo editar la especialidad.";
            }
            return RedirectToAction("Index");
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
    }
}
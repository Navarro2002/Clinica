using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class EspecialidadController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

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
            if (ModelState.IsValid)
            {
                especialidad.FechaCreacion = DateTime.Now;
                db.Especialidades.Add(especialidad);
                db.SaveChanges();
                return RedirectToAction("Index");
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

            if (!string.IsNullOrWhiteSpace(Nombre))
            {
                especialidad.Nombre = Nombre;
                db.Entry(especialidad).State = EntityState.Modified;
                db.SaveChanges();
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

            db.Especialidades.Remove(especialidad);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
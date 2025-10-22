using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class DoctorController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

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
                return RedirectToAction("Index");
            }
            // Si hay error, recarga especialidades y vuelve a la vista
            ViewBag.Especialidades = db.Especialidades.ToList();
            var doctores = db.Doctores.Include(d => d.Especialidad).ToList();
            return View("Index", doctores);
        }

        // POST: Doctor/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int IdDoctor, string NumeroDocumentoIdentidad, string Nombres, string Apellidos, string Genero, int IdEspecialidad)
        {
            var doctor = db.Doctores.Find(IdDoctor);
            if (doctor == null)
                return HttpNotFound();

            doctor.NumeroDocumentoIdentidad = NumeroDocumentoIdentidad;
            doctor.Nombres = Nombres;
            doctor.Apellidos = Apellidos;
            doctor.Genero = Genero;
            doctor.IdEspecialidad = IdEspecialidad;

            db.Entry(doctor).State = EntityState.Modified;
            db.SaveChanges();
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

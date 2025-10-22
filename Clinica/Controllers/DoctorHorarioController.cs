using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class DoctorHorarioController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        // GET: DoctorHorario
        public ActionResult Index()
        {
            var horarios = db.DoctorHorarios
                .Include(h => h.Doctor)
                .AsNoTracking()
                .ToList();

            var doctores = db.Doctores
                .AsNoTracking()
                .ToList();

            ViewBag.Doctores = doctores;
            return View(horarios);
        }

        // POST: DoctorHorario/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "IdDoctor,NumeroMes,DiasAtencion,HoraInicioAM,HoraFinAM,HoraInicioPM,HoraFinPM")] DoctorHorario horario)
        {
            if (ModelState.IsValid)
            {
                horario.FechaCreacion = DateTime.Now;
                db.DoctorHorarios.Add(horario);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.Doctores = db.Doctores.ToList();
            var horarios = db.DoctorHorarios.Include(h => h.Doctor).ToList();
            return View("Index", horarios);
        }

        // POST: DoctorHorario/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int IdDoctorHorario, int IdDoctor, int NumeroMes, string DiasAtencion, TimeSpan HoraInicioAM, TimeSpan HoraFinAM, TimeSpan HoraInicioPM, TimeSpan HoraFinPM)
        {
            var horario = db.DoctorHorarios.Find(IdDoctorHorario);
            if (horario == null)
                return HttpNotFound();

            horario.IdDoctor = IdDoctor;
            horario.NumeroMes = NumeroMes;
            horario.HoraInicioAM = HoraInicioAM;
            horario.HoraFinAM = HoraFinAM;
            horario.HoraInicioPM = HoraInicioPM;
            horario.HoraFinPM = HoraFinPM;

            db.Entry(horario).State = EntityState.Modified;
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // POST: DoctorHorario/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int IdDoctorHorario)
        {
            var horario = db.DoctorHorarios.Find(IdDoctorHorario);
            if (horario == null)
                return HttpNotFound();

            db.DoctorHorarios.Remove(horario);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}

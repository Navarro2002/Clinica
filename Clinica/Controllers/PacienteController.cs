using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class PacienteController : Controller
    {
        private readonly ClinicaContext db = new ClinicaContext();

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

        // GET: Paciente/MisCitas
        public ActionResult Index()
        {
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            var citas = db.Citas
                .Include(c => c.DoctorHorarioDetalle)
                .Include(c => c.DoctorHorarioDetalle.DoctorHorario)
                .Include(c => c.DoctorHorarioDetalle.DoctorHorario.Doctor)
                .Include(c => c.DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad)
                .Where(c => c.IdUsuario == idUsuario && c.IdEstadoCita == 1 && c.FechaCita >= DateTime.Now)
                .OrderBy(c => c.FechaCita)
                .ThenBy(c => c.DoctorHorarioDetalle.TurnoHora)
                .AsNoTracking()
                .ToList();
            return View(citas);
        }

        // Paso 1: Especialidad
        [HttpGet]
        public ActionResult NuevaCita()
        {
            ViewBag.Especialidades = db.Especialidades.AsNoTracking().OrderBy(e => e.Nombre).ToList();
            return View();
        }

        // Paso 2: Doctor por especialidad
        [HttpGet]
        public ActionResult SeleccionDoctor(int idEspecialidad)
        {
            var doctores = db.Doctores
                .Where(d => d.IdEspecialidad == idEspecialidad)
                .OrderBy(d => d.Nombres)
                .AsNoTracking()
                .ToList();
            ViewBag.IdEspecialidad = idEspecialidad;
            return View(doctores);
        }

        // Paso 3: Horario disponible de un doctor (simple placeholder)
        [HttpGet]
        public ActionResult SeleccionHorario(int idDoctor)
        {
            var horarios = db.DoctorHorarioDetalles
                .Include(d => d.DoctorHorario)
                .Where(d => d.DoctorHorario.IdDoctor == idDoctor && d.Fecha >= DateTime.Today && d.Reservado != true)
                .OrderBy(d => d.Fecha).ThenBy(d => d.TurnoHora)
                .Take(200)
                .AsNoTracking()
                .ToList();
            ViewBag.IdDoctor = idDoctor;
            return View(horarios);
        }

        // POST: Paciente/Reservar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reservar(int idDoctorHorarioDetalle)
        {
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            var detalle = db.DoctorHorarioDetalles.FirstOrDefault(d => d.IdDoctorHorarioDetalle == idDoctorHorarioDetalle);
            if (detalle == null)
            {
                TempData["Error"] = "Horario no encontrado.";
                return RedirectToAction("Index");
            }
            if (detalle.Reservado == true)
            {
                TempData["Error"] = "El horario seleccionado ya fue reservado.";
                return RedirectToAction("Index");
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    detalle.Reservado = true;
                    db.Entry(detalle).State = EntityState.Modified;

                    var cita = new Cita
                    {
                        IdUsuario = idUsuario,
                        IdDoctorHorarioDetalle = detalle.IdDoctorHorarioDetalle,
                        IdEstadoCita = 1,
                        FechaCita = detalle.Fecha,
                        FechaCreacion = DateTime.Now
                    };
                    db.Citas.Add(cita);
                    db.SaveChanges();

                    tx.Commit();
                    TempData["Success"] = "Cita reservada correctamente.";
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    TempData["Error"] = ex.Message;
                }
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

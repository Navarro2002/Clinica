using Clinica.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using System.Web;

namespace Clinica.Controllers
{
    public class DoctorHorarioController : Controller
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
        public ActionResult Create(
            [Bind(Include = "IdDoctor,NumeroMes,HoraInicioAM,HoraFinAM,HoraInicioPM,HoraFinPM")] DoctorHorario horario,
            string DiasAtencion)
        {
            // Validaciones básicas
            if (!horario.IdDoctor.HasValue)
                ModelState.AddModelError("IdDoctor", "Seleccione un doctor.");
            if (!horario.NumeroMes.HasValue)
                ModelState.AddModelError("NumeroMes", "Seleccione el mes.");
            if (horario.HoraInicioAM == null || horario.HoraFinAM == null || horario.HoraInicioPM == null || horario.HoraFinPM == null)
                ModelState.AddModelError("", "Debe ingresar los rangos de horas AM y PM.");
            if (string.IsNullOrWhiteSpace(DiasAtencion))
                ModelState.AddModelError("DiasAtencion", "Debe ingresar las fechas (separadas por coma).");

            // Si ya hay errores de modelo, volvemos a Index
            if (!ModelState.IsValid)
            {
                ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                var horariosErr = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                return View("Index", horariosErr);
            }

            // Parseo de fechas con formato d/M/yyyy (equivalente a SET DATEFORMAT DMY del SP)
            var formatos = new[] { "d/M/yyyy", "dd/MM/yyyy" };
            var cultura = CultureInfo.GetCultureInfo("es-ES");
            var fechas = new List<DateTime>();
            foreach (var token in DiasAtencion.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = token.Trim();
                if (!DateTime.TryParseExact(s, formatos, cultura, DateTimeStyles.None, out var fecha))
                {
                    ModelState.AddModelError("DiasAtencion", $"Fecha inválida: {s}. Use el formato dd/MM/yyyy.");
                    ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                    var horariosErr = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                    return View("Index", horariosErr);
                }
                fechas.Add(fecha.Date);
            }

            // Validación: todas las fechas deben pertenecer al mismo mes seleccionado
            int mesSeleccionado = horario.NumeroMes.Value;
            if (fechas.Any(f => f.Month != mesSeleccionado))
            {
                ModelState.AddModelError("", "Todas las fechas deben estar dentro del mismo mes");
                ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                var horariosErr = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                return View("Index", horariosErr);
            }

            // Validación: doctor ya tiene horario para el mes
            bool existeHorarioMes = db.DoctorHorarios.AsNoTracking()
                .Any(h => h.IdDoctor == horario.IdDoctor && h.NumeroMes == mesSeleccionado);
            if (existeHorarioMes)
            {
                ModelState.AddModelError("", "El doctor ya tiene registrado su horario para el mes seleccionado");
                ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                var horariosErr = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                return View("Index", horariosErr);
            }

            // Generación de intervalos de 30 minutos (incluye hora de fin, como en el SP)
            List<TimeSpan> GenerarTurnos(TimeSpan inicio, TimeSpan fin)
            {
                var lista = new List<TimeSpan>();
                var t = inicio;
                lista.Add(t);
                while (t.Add(TimeSpan.FromMinutes(30)) <= fin)
                {
                    t = t.Add(TimeSpan.FromMinutes(30));
                    lista.Add(t);
                }
                return lista;
            }

            var turnosAM = GenerarTurnos(horario.HoraInicioAM.Value, horario.HoraFinAM.Value);
            var turnosPM = GenerarTurnos(horario.HoraInicioPM.Value, horario.HoraFinPM.Value);

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Inserta cabecera DoctorHorario
                    var cabecera = new DoctorHorario
                    {
                        IdDoctor = horario.IdDoctor,
                        NumeroMes = mesSeleccionado,
                        HoraInicioAM = horario.HoraInicioAM,
                        HoraFinAM = horario.HoraFinAM,
                        HoraInicioPM = horario.HoraInicioPM,
                        HoraFinPM = horario.HoraFinPM,
                        FechaCreacion = DateTime.Now
                    };
                    db.DoctorHorarios.Add(cabecera);
                    db.SaveChanges(); // Necesario para obtener IdDoctorHorario

                    // Construye detalles (cruz producto de fechas x turnos AM/PM)
                    var detalles = new List<DoctorHorarioDetalle>(fechas.Count * (turnosAM.Count + turnosPM.Count));
                    foreach (var fecha in fechas.OrderBy(f => f))
                    {
                        foreach (var t in turnosAM)
                        {
                            detalles.Add(new DoctorHorarioDetalle
                            {
                                IdDoctorHorario = cabecera.IdDoctorHorario,
                                Fecha = fecha,
                                Turno = "AM",
                                TurnoHora = t,
                                Reservado = false,
                                FechaCreacion = DateTime.Now
                            });
                        }

                        foreach (var t in turnosPM)
                        {
                            detalles.Add(new DoctorHorarioDetalle
                            {
                                IdDoctorHorario = cabecera.IdDoctorHorario,
                                Fecha = fecha,
                                Turno = "PM",
                                TurnoHora = t,
                                Reservado = false,
                                FechaCreacion = DateTime.Now
                            });
                        }
                    }

                    // Inserta detalles
                    db.DoctorHorarioDetalles.AddRange(detalles);
                    db.SaveChanges();

                    tx.Commit();
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    ModelState.AddModelError("", ex.Message);
                    ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                    var horariosErr = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                    return View("Index", horariosErr);
                }
            }
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

            // Verifica si algún detalle está reservado
            bool tieneReservado = db.DoctorHorarioDetalles.Any(d => d.IdDoctorHorario == IdDoctorHorario && d.Reservado == true);
            if (tieneReservado)
            {
                ModelState.AddModelError("", "No se puede eliminar porque un turno ya fue reservado");
                ViewBag.Doctores = db.Doctores.AsNoTracking().ToList();
                var horarios = db.DoctorHorarios.Include(h => h.Doctor).AsNoTracking().ToList();
                return View("Index", horarios);
            }

            // Elimina los detalles y el horario
            var detalles = db.DoctorHorarioDetalles.Where(d => d.IdDoctorHorario == IdDoctorHorario).ToList();
            db.DoctorHorarioDetalles.RemoveRange(detalles);
            db.DoctorHorarios.Remove(horario);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}

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
            if ((Session["RolUsuario"] as string)?.ToLower() != "administrador")
            {
                filterContext.Result = RedirectToAction("Index", "Home");
                return;
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
                TempData["Error"] = "Seleccione un doctor.";
            if (!horario.NumeroMes.HasValue)
                TempData["Error"] = "Seleccione el mes.";
            if (horario.HoraInicioAM == null || horario.HoraFinAM == null || horario.HoraInicioPM == null || horario.HoraFinPM == null)
                TempData["Error"] = "Debe ingresar los rangos de horas AM y PM.";
            if (string.IsNullOrWhiteSpace(DiasAtencion))
                TempData["Error"] = "Debe ingresar las fechas (separadas por coma).";

            if (TempData["Error"] != null)
                return RedirectToAction("Index");

            var formatos = new[] { "d/M/yyyy", "dd/MM/yyyy" };
            var cultura = CultureInfo.GetCultureInfo("es-ES");
            var fechas = new List<DateTime>();
            foreach (var token in DiasAtencion.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = token.Trim();
                if (!DateTime.TryParseExact(s, formatos, cultura, DateTimeStyles.None, out var fecha))
                {
                    TempData["Error"] = $"Fecha inválida: {s}. Use el formato dd/MM/yyyy.";
                    return RedirectToAction("Index");
                }
                fechas.Add(fecha.Date);
            }

            int mesSeleccionado = horario.NumeroMes.Value;
            if (fechas.Any(f => f.Month != mesSeleccionado))
            {
                TempData["Error"] = "Todas las fechas deben estar dentro del mismo mes";
                return RedirectToAction("Index");
            }

            bool existeHorarioMes = db.DoctorHorarios.AsNoTracking()
                .Any(h => h.IdDoctor == horario.IdDoctor && h.NumeroMes == mesSeleccionado);
            if (existeHorarioMes)
            {
                TempData["Error"] = "El doctor ya tiene registrado su horario para el mes seleccionado";
                return RedirectToAction("Index");
            }

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
                    db.SaveChanges();

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
            db.DoctorHorarioDetalles.AddRange(detalles);
            db.SaveChanges();
            tx.Commit();
            TempData["Success"] = "Horario creado correctamente.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index");
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
            {
                TempData["Error"] = "No se encontró el horario.";
                return RedirectToAction("Index");
            }

            // Verifica si algún detalle está reservado
            bool tieneReservado = db.DoctorHorarioDetalles.Any(d => d.IdDoctorHorario == IdDoctorHorario && d.Reservado == true);
            if (tieneReservado)
            {
                TempData["Error"] = "No se puede eliminar porque un turno ya fue reservado.";
                return RedirectToAction("Index");
            }

            // Elimina los detalles y el horario
            var detalles = db.DoctorHorarioDetalles.Where(d => d.IdDoctorHorario == IdDoctorHorario).ToList();
            db.DoctorHorarioDetalles.RemoveRange(detalles);
            db.DoctorHorarios.Remove(horario);
            db.SaveChanges();
            TempData["Success"] = "Horario eliminado correctamente.";
            return RedirectToAction("Index");
        }
    }
}

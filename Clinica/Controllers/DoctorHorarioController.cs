using Clinica.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using System.Web;
using iTextSharp.text; // PDF
using iTextSharp.text.pdf; // PDF

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
            // Validaciones basicas
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
                    TempData["Error"] = $"Fecha invalida: {s}. Use el formato dd/MM/yyyy.";
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
                TempData["Error"] = "No se encontro el horario.";
                return RedirectToAction("Index");
            }

            // Verifica si algun detalle esta reservado
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

        // GET: DoctorHorario/ReporteHorarioPDF
        [HttpGet]
        public FileResult ReporteHorarioPDF(int idDoctorHorario)
        {
            var horario = db.DoctorHorarios
                .Include(h => h.Doctor)
                .FirstOrDefault(h => h.IdDoctorHorario == idDoctorHorario);
            if (horario == null)
            {
                using (var msErr = new System.IO.MemoryStream())
                {
                    var docErr = new Document(PageSize.A4);
                    PdfWriter.GetInstance(docErr, msErr);
                    docErr.Open();
                    docErr.Add(new Paragraph("No se encontro el horario solicitado."));
                    docErr.Close();
                    return File(msErr.ToArray(), "application/pdf", "ErrorReporteHorario.pdf");
                }
            }

            var detalles = db.DoctorHorarioDetalles
                .Where(d => d.IdDoctorHorario == idDoctorHorario)
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Turno)
                .ThenBy(d => d.TurnoHora)
                .ToList();

            using (var ms = new System.IO.MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLUE);
                var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.DARK_GRAY);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

                var culturaES = CultureInfo.GetCultureInfo("es-ES");
                var mesNombre = culturaES.TextInfo.ToTitleCase(culturaES.DateTimeFormat.GetMonthName(horario.NumeroMes ?? 0));

                var title = new Paragraph("REPORTE DE HORARIO DEL DOCTOR", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 8f };
                doc.Add(title);
                var subTitle = new Paragraph(
                    $"Doctor: {horario.Doctor?.Nombres} {horario.Doctor?.Apellidos}\n" +
                    $"Mes: {mesNombre}\n" +
                    $"Rangos AM: {FormatRange(horario.HoraInicioAM, horario.HoraFinAM)}   |   Rangos PM: {FormatRange(horario.HoraInicioPM, horario.HoraFinPM)}\n" +
                    $"Fecha de generacion: {DateTime.Now:dd/MM/yyyy HH:mm}", subTitleFont)
                { Alignment = Element.ALIGN_CENTER, SpacingAfter = 16f };
                doc.Add(subTitle);

                var table = new PdfPTable(4) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 22, 18, 20, 20 });

                string[] headers = { "Fecha", "Turno", "Hora", "Estado Cita" };
                foreach (var h in headers)
                {
                    var cell = new PdfPCell(new Phrase(h, headerFont))
                    {
                        BackgroundColor = new BaseColor(60, 60, 60),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5
                    };
                    table.AddCell(cell);
                }

                // Agrupar por fecha y usar Rowspan para no repetir celdas vacías
                var gruposPorFecha = detalles
                    .GroupBy(d => d.Fecha.HasValue ? d.Fecha.Value.Date : DateTime.MinValue)
                    .OrderBy(g => g.Key);

                foreach (var grupo in gruposPorFecha)
                {
                    string fechaText;
                    if (grupo.Key == DateTime.MinValue)
                    {
                        fechaText = string.Empty;
                    }
                    else
                    {
                        var diaNombre = culturaES.DateTimeFormat.GetDayName(grupo.Key.DayOfWeek);
                        var mesLargo = culturaES.DateTimeFormat.GetMonthName(grupo.Key.Month);
                        fechaText = $"{culturaES.TextInfo.ToTitleCase(diaNombre)} {grupo.Key.Day} de {culturaES.TextInfo.ToTitleCase(mesLargo)} de {grupo.Key.Year}";
                    }

                    var fechaCell = new PdfPCell(new Phrase(fechaText, cellFont))
                    {
                        Padding = 4,
                        Rowspan = grupo.Count()
                    };

                    bool first = true;
                    foreach (var d in grupo)
                    {
                        if (first)
                        {
                            table.AddCell(fechaCell);
                            first = false;
                        }
                        // Turno, Hora y Estado
                        var turno = d.Turno ?? "-";
                        var hora = d.TurnoHora.HasValue ? (DateTime.Today + d.TurnoHora.Value).ToString("HH:mm") : string.Empty;
                        var estado = d.Reservado == true ? "Reservado" : "Disponible";

                        table.AddCell(new PdfPCell(new Phrase(turno, cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER });
                        table.AddCell(new PdfPCell(new Phrase(hora, cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_RIGHT });

                        var estadoCell = new PdfPCell(new Phrase(estado, cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER };
                        if (d.Reservado == true)
                        {
                            estadoCell.BackgroundColor = new BaseColor(255, 235, 238); // rojo suave
                        }
                        else
                        {
                            estadoCell.BackgroundColor = new BaseColor(232, 245, 233); // verde suave
                        }
                        table.AddCell(estadoCell);
                    }
                }

                doc.Add(table);
                doc.Close();
                return File(ms.ToArray(), "application/pdf", $"Horario_{horario.Doctor?.Apellidos}_{mesNombre}.pdf");
            }

            // Helpers locales
            string FormatRange(TimeSpan? ini, TimeSpan? fin)
            {
                if (!ini.HasValue || !fin.HasValue) return "-";
                var hi = (DateTime.Today + ini.Value).ToString("hh:mm tt");
                var hf = (DateTime.Today + fin.Value).ToString("hh:mm tt");
                return hi + " - " + hf;
            }
        }
    }
}

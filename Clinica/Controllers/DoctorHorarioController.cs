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
using System.Text;

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
        public ActionResult Index(string filtroDocumento, string filtroNombreApellido)
        {
            var horarios = db.DoctorHorarios
                .Include(h => h.Doctor)
                .AsNoTracking()
                .ToList();

            if (!string.IsNullOrWhiteSpace(filtroDocumento))
            {
                string filtroNormalizado = RemoverDiacriticos(filtroDocumento.ToLower());
                horarios = horarios
                    .Where(h => RemoverDiacriticos((h.Doctor?.NumeroDocumentoIdentidad ?? "").ToLower()).Contains(filtroNormalizado))
                    .ToList();
            }
            if (!string.IsNullOrWhiteSpace(filtroNombreApellido))
            {
                string filtroNA = RemoverDiacriticos(filtroNombreApellido.ToLower());
                horarios = horarios
                    .Where(h => RemoverDiacriticos(((h.Doctor?.Nombres ?? "") + " " + (h.Doctor?.Apellidos ?? "")).ToLower()).Contains(filtroNA))
                    .ToList();
            }

            var doctores = db.Doctores
                .AsNoTracking()
                .ToList();

            ViewBag.Doctores = doctores;
            ViewBag.FiltroDocumento = filtroDocumento;
            ViewBag.FiltroNombreApellido = filtroNombreApellido;
            return View(horarios);
        }

        // POST: DoctorHorario/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            [Bind(Include = "IdDoctor,NumeroMes,HoraInicioAM,HoraFinAM,HoraInicioPM,HoraFinPM")] DoctorHorario horario,
            string DiasAtencion)
        {
            // Validación de DiasAtencion
            if (string.IsNullOrWhiteSpace(DiasAtencion))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, errors = new { DiasAtencion = new[] { "Debe ingresar las fechas (separadas por coma)" } } });
                TempData["Error"] = "Debe ingresar las fechas (separadas por coma)";
                return RedirectToAction("Index");
            }

            // Validación condicional de horarios AM/PM
            bool amCompleto = horario.HoraInicioAM.HasValue && horario.HoraFinAM.HasValue;
            bool pmCompleto = horario.HoraInicioPM.HasValue && horario.HoraFinPM.HasValue;
            if (!amCompleto && !pmCompleto)
            {
                ModelState.AddModelError("General", "Debe ingresar al menos un rango de horario AM o PM.");
            }
            if (amCompleto)
            {
                ModelState.Remove("HoraInicioPM");
                ModelState.Remove("HoraFinPM");
            }
            if (pmCompleto)
            {
                ModelState.Remove("HoraInicioAM");
                ModelState.Remove("HoraFinAM");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, errors = errors });
                TempData["Error"] = string.Join("; ", errors.SelectMany(e => e.Value));
                return RedirectToAction("Index");
            }

            var formatos = new[] { "d/M/yyyy", "dd/MM/yyyy" };
            var cultura = CultureInfo.GetCultureInfo("es-ES");
            var fechas = new List<DateTime>();
            foreach (var token in DiasAtencion.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = token.Trim();
                if (!DateTime.TryParseExact(s, formatos, cultura, DateTimeStyles.None, out var fecha))
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, errors = new { DiasAtencion = new[] { $"Fecha inválida: {s}. Use el formato dd/MM/yyyy" } } });
                    TempData["Error"] = $"Fecha inválida: {s}. Use el formato dd/MM/yyyy";
                    return RedirectToAction("Index");
                }
                fechas.Add(fecha.Date);
            }

            int mesSeleccionado = horario.NumeroMes.Value;
            if (fechas.Any(f => f.Month != mesSeleccionado))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, errors = new { DiasAtencion = new[] { "Todas las fechas deben estar dentro del mismo mes" } } });
                TempData["Error"] = "Todas las fechas deben estar dentro del mismo mes";
                return RedirectToAction("Index");
            }

            bool existeHorarioMes = db.DoctorHorarios.AsNoTracking()
                .Any(h => h.IdDoctor == horario.IdDoctor && h.NumeroMes == mesSeleccionado);
            if (existeHorarioMes)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, errors = new { NumeroMes = new[] { "El doctor ya tiene registrado su horario para el mes seleccionado" } } });
                TempData["Error"] = "El doctor ya tiene registrado su horario para el mes seleccionado";
                return RedirectToAction("Index");
            }

            List<TimeSpan> GenerarTurnos(TimeSpan? inicio, TimeSpan? fin)
            {
                var lista = new List<TimeSpan>();
                if (!inicio.HasValue || !fin.HasValue) return lista;
                var t = inicio.Value;
                lista.Add(t);
                while (t.Add(TimeSpan.FromMinutes(30)) <= fin.Value)
                {
                    t = t.Add(TimeSpan.FromMinutes(30));
                    lista.Add(t);
                }
                return lista;
            }

            var turnosAM = GenerarTurnos(horario.HoraInicioAM, horario.HoraFinAM);
            var turnosPM = GenerarTurnos(horario.HoraInicioPM, horario.HoraFinPM);

            if (turnosAM.Count == 0 && turnosPM.Count == 0)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, errors = new { General = new[] { "Debe ingresar al menos un rango de horario AM o PM" } } });
                TempData["Error"] = "Debe ingresar al menos un rango de horario AM o PM";
                return RedirectToAction("Index");
            }

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
                    if (Request.IsAjaxRequest())
                        return Json(new { success = true, message = "Horario creado correctamente." });
                    TempData["Success"] = "Horario creado correctamente.";
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, errors = new { General = new[] { "No se pudo crear el horario: " + ex.Message } } });
                    TempData["Error"] = "No se pudo crear el horario: " + ex.Message;
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

        private string RemoverDiacriticos(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            var normalized = texto.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}

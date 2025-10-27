using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class PacienteController : Controller
    {
        private readonly ClinicaContext db = new ClinicaContext();

        public ActionResult Index()
        {
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            int estadoPendiente = db.EstadoCitas.Where(e => e.Nombre == "Pendiente").Select(e => e.IdEstadoCita).FirstOrDefault();
            var citas = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Where(c => c.IdUsuario == idUsuario && c.IdEstadoCita == estadoPendiente)
                .OrderBy(c => c.FechaCita)
                .ThenBy(c => c.DoctorHorarioDetalle.TurnoHora)
                .ToList();
            return View(citas);
        }

        [HttpGet]
        public ActionResult ComprobantePdf(int idCita)
        {
            var cita = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Include("Usuario")
                .Include("EstadoCita")
                .FirstOrDefault(c => c.IdCita == idCita);
            if (cita == null)
                return HttpNotFound();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();
                var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, BaseColor.WHITE);
                var titleTable = new PdfPTable(1) { WidthPercentage = 100 };
                var cellTitle = new PdfPCell(new Phrase("Comprobante de Reserva de Cita", fontTitle))
                {
                    BackgroundColor = new BaseColor(52, 152, 219),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 12,
                    Border = Rectangle.NO_BORDER
                };
                titleTable.AddCell(cellTitle);
                doc.Add(titleTable);
                doc.Add(new Paragraph("\n"));
                var fontLabel = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var fontValue = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var infoTable = new PdfPTable(2) { WidthPercentage = 70 };
                infoTable.HorizontalAlignment = Element.ALIGN_LEFT;
                infoTable.SpacingBefore = 10f;
                infoTable.DefaultCell.Border = Rectangle.NO_BORDER;
                infoTable.SetWidths(new float[] { 30, 70 });
                infoTable.AddCell(new PdfPCell(new Phrase("Fecha de emisión:", fontLabel)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Paciente:", fontLabel)) { Border = Rectangle.NO_BORDER });
                string paciente = cita.Usuario != null ? ($"{cita.Usuario.Nombre} {cita.Usuario.Apellido}") : "";
                infoTable.AddCell(new PdfPCell(new Phrase(paciente, fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Especialidad:", fontLabel)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase(cita.DoctorHorarioDetalle?.DoctorHorario?.Doctor?.Especialidad?.Nombre ?? "", fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Doctor:", fontLabel)) { Border = Rectangle.NO_BORDER });
                string doctor = cita.DoctorHorarioDetalle?.DoctorHorario?.Doctor != null ? ($"{cita.DoctorHorarioDetalle.DoctorHorario.Doctor.Nombres} {cita.DoctorHorarioDetalle.DoctorHorario.Doctor.Apellidos}") : "";
                infoTable.AddCell(new PdfPCell(new Phrase(doctor, fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Fecha de la cita:", fontLabel)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase(cita.FechaCita?.ToString("dd/MM/yyyy") ?? "", fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Hora:", fontLabel)) { Border = Rectangle.NO_BORDER });
                string hora = cita.DoctorHorarioDetalle?.TurnoHora != null ? cita.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                infoTable.AddCell(new PdfPCell(new Phrase(hora, fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Turno:", fontLabel)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase(cita.DoctorHorarioDetalle?.Turno ?? "", fontValue)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase("Estado:", fontLabel)) { Border = Rectangle.NO_BORDER });
                infoTable.AddCell(new PdfPCell(new Phrase(cita.EstadoCita?.Nombre ?? "", fontValue)) { Border = Rectangle.NO_BORDER });
                doc.Add(infoTable);
                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph("______________________________________________________________"));
                doc.Close();
                byte[] pdfBytes = ms.ToArray();
                return File(pdfBytes, "application/pdf", "ComprobanteCita.pdf");
            }
        }

        public ActionResult NuevaCita()
        {
            ViewBag.Especialidades = db.Especialidades.AsNoTracking().OrderBy(e => e.Nombre).ToList();
            return View();
        }

        public ActionResult Historial()
        {
            if (!EsPaciente())
            {
                TempData["Error"] = "Solo los usuarios con rol Paciente pueden ver el historial de citas.";
                return RedirectToAction("Index");
            }
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            var estadosHistorial = db.EstadoCitas
                .Where(e => e.Nombre == "Anulado" || e.Nombre == "Cancelada" || e.Nombre == "Atendido")
                .Select(e => e.IdEstadoCita)
                .ToList();

            var citas = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Include("EstadoCita")
                .Where(c => c.IdUsuario == idUsuario && estadosHistorial.Contains(c.IdEstadoCita ?? 0))
                .OrderByDescending(c => c.FechaCita)
                .ThenByDescending(c => c.DoctorHorarioDetalle.TurnoHora)
                .ToList();
            return View(citas);
        }

        public ActionResult SeleccionDoctor(int idEspecialidad)
        {
            var doctores = db.Doctores
                .Where(d => d.IdEspecialidad == idEspecialidad)
                .OrderBy(d => d.Nombres)
                .ToList();
            ViewBag.IdEspecialidad = idEspecialidad;
            return View(doctores);
        }

        public ActionResult SeleccionHorario(int idDoctor)
        {
            if (!EsPaciente())
            {
                TempData["Error"] = "Solo los usuarios con rol Paciente pueden ver los horarios disponibles.";
                return RedirectToAction("Index");
            }
            // Obtener los detalles de horario del doctor seleccionado que no estén reservados
            var detalles = db.DoctorHorarioDetalles
                .Where(d => d.DoctorHorario.IdDoctor == idDoctor)
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Turno)
                .ThenBy(d => d.TurnoHora)
                .ToList();
            return View(detalles);
        }

        private bool EsPaciente()
        {
            var idUsuario = Session["IdUsuario"];
            if (idUsuario == null) return false;
            var usuario = db.Usuarios.Find((int)idUsuario);
            return usuario != null && usuario.IdRolUsuario.HasValue && usuario.Rol != null && usuario.Rol.Nombre == "Paciente";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reservar(int idDoctorHorarioDetalle)
        {
            if (!EsPaciente())
            {
                TempData["Error"] = "Solo los usuarios con rol Paciente pueden agendar citas.";
                return RedirectToAction("Index");
            }
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            var detalle = db.DoctorHorarioDetalles.Find(idDoctorHorarioDetalle);
            if (detalle == null || detalle.Reservado == true)
            {
                TempData["Error"] = "El horario seleccionado ya no está disponible.";
                return RedirectToAction("SeleccionHorario", new { idDoctor = detalle?.DoctorHorario?.IdDoctor });
            }
            // Marcar el detalle como reservado
            detalle.Reservado = true;
            db.SaveChanges();
            // Crear la cita
            var cita = new Cita
            {
                IdUsuario = idUsuario,
                IdDoctorHorarioDetalle = idDoctorHorarioDetalle,
                FechaCita = detalle.Fecha,
                IdEstadoCita = db.EstadoCitas.Where(e => e.Nombre == "Pendiente").Select(e => e.IdEstadoCita).FirstOrDefault(),
                FechaCreacion = DateTime.Now
            };
            db.Citas.Add(cita);
            db.SaveChanges();
            // Redirigir mostrando comprobante en el modal
            return RedirectToAction("SeleccionHorario", new { idDoctor = detalle.DoctorHorario.IdDoctor, reserva = "ok", idCita = cita.IdCita });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancelar(int idCita)
        {
            if (!EsPaciente())
            {
                TempData["Error"] = "Solo los usuarios con rol Paciente pueden cancelar citas.";
                return RedirectToAction("Index");
            }
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var cita = db.Citas.Find(idCita);
                    if (cita == null)
                    {
                        TempData["Error"] = "La cita no existe.";
                        return RedirectToAction("Index");
                    }
                    int idCancelada = db.EstadoCitas.Where(e => e.Nombre == "Cancelada").Select(e => e.IdEstadoCita).FirstOrDefault();
                    if (idCancelada != 0)
                    {
                        cita.IdEstadoCita = idCancelada;
                        db.SaveChanges();
                    }
                    if (cita.IdDoctorHorarioDetalle.HasValue)
                    {
                        var detalle = db.DoctorHorarioDetalles.Find(cita.IdDoctorHorarioDetalle.Value);
                        if (detalle != null)
                        {
                            detalle.Reservado = false;
                            db.SaveChanges();
                        }
                    }
                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    TempData["Error"] = "Error al cancelar la cita.";
                }
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult HistorialPdf()
        {
            int idUsuario = Convert.ToInt32(Session["IdUsuario"]);
            var estadosHistorial = db.EstadoCitas
                .Where(e => e.Nombre == "Anulado" || e.Nombre == "Cancelada" || e.Nombre == "Atendido")
                .Select(e => e.IdEstadoCita)
                .ToList();
            var citas = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Include("EstadoCita")
                .Where(c => c.IdUsuario == idUsuario && estadosHistorial.Contains(c.IdEstadoCita ?? 0))
                .OrderByDescending(c => c.FechaCita)
                .ThenByDescending(c => c.DoctorHorarioDetalle.TurnoHora)
                .ToList();

            using (var ms = new System.IO.MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate());
                iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                doc.Open();
                var fontTitle = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 20, BaseColor.WHITE);
                var titleTable = new PdfPTable(1) { WidthPercentage = 100 };
                var cellTitle = new PdfPCell(new Phrase("Historial de Citas", fontTitle))
                {
                    BackgroundColor = new BaseColor(46, 204, 113),
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 12,
                    Border = Rectangle.NO_BORDER
                };
                titleTable.AddCell(cellTitle);
                doc.Add(titleTable);
                doc.Add(new Paragraph("\n"));
                var fontHeader = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 13, BaseColor.WHITE);
                var fontCell = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 12);
                var table = new iTextSharp.text.pdf.PdfPTable(5) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 15, 15, 25, 25, 20 });
                string[] headers = { "Fecha", "Hora", "Especialidad", "Doctor", "Estado" };
                foreach (var h in headers)
                {
                    var cell = new PdfPCell(new Phrase(h, fontHeader))
                    {
                        BackgroundColor = new BaseColor(52, 73, 94),
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 8
                    };
                    table.AddCell(cell);
                }
                foreach (var c in citas)
                {
                    var fecha = c.FechaCita.HasValue ? c.FechaCita.Value.ToString("dd/MM/yyyy") : "";
                    var hora = c.DoctorHorarioDetalle?.TurnoHora.HasValue == true ? c.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                    var esp = c.DoctorHorarioDetalle?.DoctorHorario?.Doctor?.Especialidad?.Nombre ?? "";
                    var docName = c.DoctorHorarioDetalle?.DoctorHorario?.Doctor != null ? (c.DoctorHorarioDetalle.DoctorHorario.Doctor.Nombres + " " + c.DoctorHorarioDetalle.DoctorHorario.Doctor.Apellidos) : "";
                    var estado = c.EstadoCita?.Nombre ?? "";
                    var cells = new[] { fecha, hora, esp, docName, estado };
                    foreach (var value in cells)
                    {
                        var cell = new PdfPCell(new Phrase(value, fontCell))
                        {
                            HorizontalAlignment = Element.ALIGN_CENTER,
                            Padding = 6
                        };
                        table.AddCell(cell);
                    }
                }
                doc.Add(table);
                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph("______________________________________________________________"));
                doc.Close();
                byte[] pdfBytes = ms.ToArray();
                return File(pdfBytes, "application/pdf", "HistorialCitas.pdf");
            }
        }
    }
}

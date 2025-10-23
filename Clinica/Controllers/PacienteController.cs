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
                .Where(c => c.IdUsuario == idUsuario && c.IdEstadoCita == estadoPendiente && c.FechaCita >= DateTime.Now)
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
                var fontTitle = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                doc.Add(new Paragraph("Comprobante de Reserva de Cita", fontTitle));
                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph("Fecha de emisión: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
                doc.Add(new Paragraph("\n"));

                // Paciente
                string paciente = cita.Usuario != null ? ($"{cita.Usuario.Nombre} {cita.Usuario.Apellido}") : "";
                doc.Add(new Paragraph("Paciente:"));
                doc.Add(new Paragraph(paciente));
                doc.Add(new Paragraph("\n"));

                // Especialidad
                doc.Add(new Paragraph("Especialidad:"));
                doc.Add(new Paragraph(cita.DoctorHorarioDetalle?.DoctorHorario?.Doctor?.Especialidad?.Nombre ?? ""));
                doc.Add(new Paragraph("\n"));

                // Doctor
                string doctor = cita.DoctorHorarioDetalle?.DoctorHorario?.Doctor != null ? ($"{cita.DoctorHorarioDetalle.DoctorHorario.Doctor.Nombres} {cita.DoctorHorarioDetalle.DoctorHorario.Doctor.Apellidos}") : "";
                doc.Add(new Paragraph("Doctor:"));
                doc.Add(new Paragraph(doctor));
                doc.Add(new Paragraph("\n"));

                // Fecha de la cita
                doc.Add(new Paragraph("Fecha de la cita:"));
                doc.Add(new Paragraph(cita.FechaCita?.ToString("dd/MM/yyyy") ?? ""));
                doc.Add(new Paragraph("\n"));

                // Hora
                string hora = cita.DoctorHorarioDetalle?.TurnoHora != null ? cita.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                doc.Add(new Paragraph("Hora:"));
                doc.Add(new Paragraph(hora));
                doc.Add(new Paragraph("\n"));

                // Turno
                doc.Add(new Paragraph("Turno:"));
                doc.Add(new Paragraph(cita.DoctorHorarioDetalle?.Turno ?? ""));
                doc.Add(new Paragraph("\n"));

                // Estado
                doc.Add(new Paragraph("Estado:"));
                doc.Add(new Paragraph(cita.EstadoCita?.Nombre ?? ""));
                doc.Add(new Paragraph("\n"));

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
            int estadoPendiente = db.EstadoCitas.Where(e => e.Nombre == "Pendiente").Select(e => e.IdEstadoCita).FirstOrDefault();
            var citas = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Include("EstadoCita")
                .Where(c => c.IdUsuario == idUsuario && (c.FechaCita < DateTime.Now || c.IdEstadoCita != estadoPendiente))
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
            var citas = db.Citas
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor")
                .Include("DoctorHorarioDetalle.DoctorHorario.Doctor.Especialidad")
                .Include("EstadoCita")
                .Where(c => c.IdUsuario == idUsuario && c.EstadoCita.Nombre != "Pendiente")
                .OrderByDescending(c => c.FechaCita)
                .ThenByDescending(c => c.DoctorHorarioDetalle.TurnoHora)
                .ToList();

            using (var ms = new System.IO.MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4.Rotate());
                iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                doc.Open();
                var fontTitle = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 16);
                doc.Add(new iTextSharp.text.Paragraph("Historial de Citas", fontTitle));
                doc.Add(new iTextSharp.text.Paragraph("\n"));
                doc.Add(new iTextSharp.text.Paragraph("Fecha de emisión: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
                doc.Add(new iTextSharp.text.Paragraph("\n"));

                var table = new iTextSharp.text.pdf.PdfPTable(5) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 15, 15, 25, 25, 20 });
                var fontHeader = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 12);
                table.AddCell(new iTextSharp.text.Phrase("Fecha", fontHeader));
                table.AddCell(new iTextSharp.text.Phrase("Hora", fontHeader));
                table.AddCell(new iTextSharp.text.Phrase("Especialidad", fontHeader));
                table.AddCell(new iTextSharp.text.Phrase("Doctor", fontHeader));
                table.AddCell(new iTextSharp.text.Phrase("Estado", fontHeader));

                var fontCell = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 11);
                foreach (var c in citas)
                {
                    var fecha = c.FechaCita.HasValue ? c.FechaCita.Value.ToString("dd/MM/yyyy") : "";
                    var hora = c.DoctorHorarioDetalle?.TurnoHora.HasValue == true ? c.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                    var esp = c.DoctorHorarioDetalle?.DoctorHorario?.Doctor?.Especialidad?.Nombre ?? "";
                    var docName = c.DoctorHorarioDetalle?.DoctorHorario?.Doctor != null ? (c.DoctorHorarioDetalle.DoctorHorario.Doctor.Nombres + " " + c.DoctorHorarioDetalle.DoctorHorario.Doctor.Apellidos) : "";
                    var estado = c.EstadoCita?.Nombre ?? "";
                    table.AddCell(new iTextSharp.text.Phrase(fecha, fontCell));
                    table.AddCell(new iTextSharp.text.Phrase(hora, fontCell));
                    table.AddCell(new iTextSharp.text.Phrase(esp, fontCell));
                    table.AddCell(new iTextSharp.text.Phrase(docName, fontCell));
                    table.AddCell(new iTextSharp.text.Phrase(estado, fontCell));
                }
                doc.Add(table);
                doc.Close();
                byte[] pdfBytes = ms.ToArray();
                return File(pdfBytes, "application/pdf", "HistorialCitas.pdf");
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["IdUsuario"] == null)
            {
                filterContext.Result = RedirectToAction("Index", "Login");
                return;
            }
            base.OnActionExecuting(filterContext);
        }
    }
}

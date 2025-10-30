using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Clinica.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Clinica.Controllers
{
    public class DoctorController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        private bool UsuarioAutenticado()
        {
            return Session["IdUsuario"] != null;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var action = filterContext.ActionDescriptor.ActionName.ToLower();
            var rol = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            // Permitir acceso a acciones de doctor
            if ((action == "citasasignadas" || action == "citasatendidas" || action == "terminarcita" || action == "reportecitasatendidaspdf" || action == "reportecitasasignadaspdf") && rol == "doctor")
            {
                base.OnActionExecuting(filterContext);
                return;
            }
            // El resto solo para administrador
            if (rol != "administrador")
            {
                filterContext.Result = RedirectToAction("Index", "Home");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // GET: Doctor
        public ActionResult Index(string filtroDocumento, string filtroNombreApellido)
        {
            var doctores = db.Doctores
                .Include(d => d.Especialidad)
                .AsNoTracking()
                .ToList();

            if (!string.IsNullOrWhiteSpace(filtroDocumento))
            {
                string filtroNormalizado = RemoverDiacriticos(filtroDocumento.ToLower());
                doctores = doctores
                    .Where(d => RemoverDiacriticos((d.NumeroDocumentoIdentidad ?? "").ToLower()).Contains(filtroNormalizado))
                    .ToList();
            }
            if (!string.IsNullOrWhiteSpace(filtroNombreApellido))
            {
                string filtroNA = RemoverDiacriticos(filtroNombreApellido.ToLower());
                doctores = doctores
                    .Where(d => RemoverDiacriticos(((d.Nombres ?? "") + " " + (d.Apellidos ?? "")).ToLower()).Contains(filtroNA))
                    .ToList();
            }

            var especialidades = db.Especialidades
                .AsNoTracking()
                .ToList();

            ViewBag.Especialidades = especialidades;
            ViewBag.FiltroDocumento = filtroDocumento;
            ViewBag.FiltroNombreApellido = filtroNombreApellido;
            return View(doctores);
        }

        private string RemoverDiacriticos(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return texto;
            var normalized = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // POST: Doctor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Doctor doctor)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            try
            {
                doctor.FechaCreacion = DateTime.Now;
                db.Doctores.Add(doctor);
                db.SaveChanges();

                // Crear usuario automáticamente
                var rolDoctor = db.RolUsuarios.FirstOrDefault(r => r.Nombre.ToLower() == "doctor");
                if (rolDoctor == null)
                {
                    rolDoctor = new RolUsuario { Nombre = "Doctor", FechaCreacion = DateTime.Now };
                    db.RolUsuarios.Add(rolDoctor);
                    db.SaveChanges();
                }

                // Verifica si ya existe usuario con ese documento
                bool existeUsuario = db.Usuarios.Any(u => u.NumeroDocumentoIdentidad == doctor.NumeroDocumentoIdentidad);
                if (!existeUsuario)
                {
                    string clave = doctor.NumeroDocumentoIdentidad;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(clave);
                        clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                    }

                    // Generar correo automático basado en el documento
                    string correoGenerado = $"{doctor.NumeroDocumentoIdentidad}@clinica.com";

                    var usuario = new Usuario
                    {
                        NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad,
                        Nombre = doctor.Nombres,
                        Apellido = doctor.Apellidos,
                        Correo = correoGenerado,
                        Clave = clave,
                        IdRolUsuario = rolDoctor.IdRolUsuario,
                        FechaCreacion = DateTime.Now
                    };

                    db.Usuarios.Add(usuario);
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Doctor y usuario creados correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, errors = new { General = new[] { "No se pudo crear el doctor: " + ex.Message } } });
            }
        }

        // POST: Doctor/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Doctor doctor)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            var existente = db.Doctores.Find(doctor.IdDoctor);
            if (existente == null)
            {
                return Json(new { success = false, errors = new { General = new[] { "Doctor no encontrado." } } });
            }

            try
            {
                // Actualiza datos del doctor
                existente.NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad;
                existente.Nombres = doctor.Nombres;
                existente.Apellidos = doctor.Apellidos;
                existente.Genero = doctor.Genero;
                existente.IdEspecialidad = doctor.IdEspecialidad;
                db.Entry(existente).State = EntityState.Modified;
                db.SaveChanges();

                // Actualiza usuario asociado si existe
                var usuario = db.Usuarios.FirstOrDefault(u => u.NumeroDocumentoIdentidad == doctor.NumeroDocumentoIdentidad);
                if (usuario != null)
                {
                    usuario.Nombre = doctor.Nombres;
                    usuario.Apellido = doctor.Apellidos;
                    usuario.NumeroDocumentoIdentidad = doctor.NumeroDocumentoIdentidad;
                    db.Entry(usuario).State = EntityState.Modified;
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Doctor y usuario actualizados correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, errors = new { General = new[] { "No se pudo editar el doctor: " + ex.Message } } });
            }
        }

        // POST: Doctor/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var doctor = db.Doctores.Find(id);
            if (doctor == null)
            {
                TempData["Error"] = "No se encontró el doctor.";
                return RedirectToAction("Index");
            }
            try
            {
                db.Doctores.Remove(doctor);
                db.SaveChanges();
                TempData["Success"] = "Doctor eliminado correctamente.";
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
            {
                TempData["Error"] = "No se pudo eliminar el doctor. Puede tener registros relacionados. Detalle: " + (ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error inesperado: " + ex.Message;
            }
            return RedirectToAction("Index");
        }

        // Vista de citas asignadas para el doctor
        [HttpGet]
        public ActionResult CitasAsignadas(string estado = "Pendiente")
        {
            // Solo permite acceso a usuarios con rol Doctor
            if ((Session["RolUsuario"] as string)?.ToLower() != "doctor")
            {
                return RedirectToAction("Index", "Home");
            }
            // Obtener el id del usuario logueado y buscar el doctor correspondiente
            var idUsuario = Session["IdUsuario"] as int?;
            var doctor = db.Doctores.FirstOrDefault(d => d.NumeroDocumentoIdentidad == db.Usuarios.Where(u => u.IdUsuario == idUsuario).Select(u => u.NumeroDocumentoIdentidad).FirstOrDefault());
            if (doctor == null)
            {
                TempData["Error"] = "No se encontró el doctor asociado al usuario.";
                return RedirectToAction("Index", "Home");
            }
            // Filtrar citas asignadas al doctor por estado
            var citas = db.Citas
                .Include("Usuario")
                .Include("EstadoCita")
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Where(c => c.DoctorHorarioDetalle.DoctorHorario.IdDoctor == doctor.IdDoctor && c.EstadoCita.Nombre == estado)
                .OrderBy(c => c.FechaCita)
                .ThenBy(c => c.DoctorHorarioDetalle.TurnoHora)
                .ToList();
            // Estados disponibles para el filtro
            var estados = db.EstadoCitas.OrderBy(e => e.Nombre).ToList();
            ViewBag.Estados = estados;
            ViewBag.EstadoSeleccionado = estado;
            return View(citas);
        }

        // POST: Doctor/TerminarCita
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TerminarCita(int idCita, string indicaciones)
        {
            var cita = db.Citas.Include("EstadoCita").FirstOrDefault(c => c.IdCita == idCita);
            if (cita == null)
            {
                TempData["Error"] = "No se encontró la cita.";
                return RedirectToAction("CitasAsignadas");
            }
            // No permitir modificar indicaciones si la cita ya está atendida
            var estadoAtendido = db.EstadoCitas.FirstOrDefault(e => e.Nombre == "Atendido");
            if (cita.IdEstadoCita == estadoAtendido?.IdEstadoCita)
            {
                TempData["Error"] = "No se pueden modificar indicaciones de una cita atendida.";
                return RedirectToAction("CitasAsignadas");
            }
            if (estadoAtendido != null)
            {
                cita.IdEstadoCita = estadoAtendido.IdEstadoCita;
            }
            cita.Indicaciones = string.IsNullOrWhiteSpace(indicaciones) ? cita.Indicaciones : indicaciones;
            db.SaveChanges();
            TempData["Success"] = "La cita fue marcada como atendida.";
            return RedirectToAction("CitasAsignadas");
        }

        // Vista de citas atendidas para el doctor
        [HttpGet]
        public ActionResult CitasAtendidas(int? mes = null)
        {
            var rolUsuario = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            TempData["RolUsuarioDebug"] = rolUsuario;
            if (rolUsuario != "doctor")
            {
                return RedirectToAction("Index", "Home");
            }
            var idUsuario = Session["IdUsuario"] as int?;
            var doctor = db.Doctores.FirstOrDefault(d => d.NumeroDocumentoIdentidad == db.Usuarios.Where(u => u.IdUsuario == idUsuario).Select(u => u.NumeroDocumentoIdentidad).FirstOrDefault());
            if (doctor == null)
            {
                TempData["Error"] = "No se encontró el doctor asociado al usuario.";
                return RedirectToAction("Index", "Home");
            }
            var citas = db.Citas
                .Include("Usuario")
                .Include("EstadoCita")
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Where(c => c.DoctorHorarioDetalle.DoctorHorario.IdDoctor == doctor.IdDoctor && c.EstadoCita.Nombre == "Atendido")
                .OrderBy(c => c.FechaCita)
                .ThenBy(c => c.DoctorHorarioDetalle.TurnoHora);
            if (mes.HasValue)
            {
                citas = citas.Where(c => c.FechaCita.HasValue && c.FechaCita.Value.Month == mes.Value)
                    .OrderBy(c => c.FechaCita)
                    .ThenBy(c => c.DoctorHorarioDetalle.TurnoHora);
            }
            var citasList = citas.ToList();
            ViewBag.MesSeleccionado = mes;
            return View(citasList);
        }

        // GET: Doctor/ReporteCitasAtendidas
        [HttpGet]
        public ActionResult ReporteCitasAtendidas(int? mes = null)
        {
            if ((Session["RolUsuario"] as string)?.ToLower() != "doctor")
            {
                return RedirectToAction("Index", "Home");
            }
            var idUsuario = Session["IdUsuario"] as int?;
            var doctor = db.Doctores.FirstOrDefault(d => d.NumeroDocumentoIdentidad == db.Usuarios.Where(u => u.IdUsuario == idUsuario).Select(u => u.NumeroDocumentoIdentidad).FirstOrDefault());
            if (doctor == null)
            {
                TempData["Error"] = "No se encontró el doctor asociado al usuario.";
                return RedirectToAction("Index", "Home");
            }
            var citas = db.Citas
                .Include("Usuario")
                .Include("EstadoCita")
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Where(c => c.DoctorHorarioDetalle.DoctorHorario.IdDoctor == doctor.IdDoctor && c.EstadoCita.Nombre == "Atendido");
            if (mes.HasValue)
            {
                citas = citas.Where(c => c.FechaCita.HasValue && c.FechaCita.Value.Month == mes.Value);
            }
            var citasList = citas.ToList();
            // Aquí deberías generar el PDF o el reporte según tu lógica
            // Por ahora, solo retorna la misma vista
            return View("CitasAtendidas", citasList);
        }

        // GET: Doctor/ReporteCitasAtendidasPDF
        [HttpGet]
        public FileResult ReporteCitasAtendidasPDF(int? mes = null)
        {
            var rolUsuario = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            var idUsuario = Session["IdUsuario"] as int?;
            if (rolUsuario != "doctor" || idUsuario == null)
            {
                using (var ms = new MemoryStream())
                {
                    var doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, ms);
                    doc.Open();
                    doc.Add(new Paragraph("Error de sesión o rol. No tienes permisos para generar este reporte."));
                    doc.Add(new Paragraph($"RolUsuario: {rolUsuario}, IdUsuario: {idUsuario}"));
                    doc.Close();
                    return File(ms.ToArray(), "application/pdf", "ErrorReporteCitasAtendidas.pdf");
                }
            }
            var doctor = db.Doctores.FirstOrDefault(d => d.NumeroDocumentoIdentidad == db.Usuarios.Where(u => u.IdUsuario == idUsuario).Select(u => u.NumeroDocumentoIdentidad).FirstOrDefault());
            if (doctor == null)
            {
                using (var ms = new MemoryStream())
                {
                    var doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, ms);
                    doc.Open();
                    doc.Add(new Paragraph("No se encontró el doctor asociado al usuario."));
                    doc.Close();
                    return File(ms.ToArray(), "application/pdf", "ErrorReporteCitasAtendidas.pdf");
                }
            }
            var citas = db.Citas
                .Include("Usuario")
                .Include("EstadoCita")
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Where(c => c.DoctorHorarioDetalle.DoctorHorario.IdDoctor == doctor.IdDoctor && c.EstadoCita.Nombre == "Atendido");
            if (mes.HasValue)
            {
                citas = citas.Where(c => c.FechaCita.HasValue && c.FechaCita.Value.Month == mes.Value);
            }
            var citasList = citas.ToList();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Título principal
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLUE);
                var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.DARK_GRAY);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

                var title = new Paragraph("REPORTE DE CITAS ATENDIDAS", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 10f };
                doc.Add(title);
                var subTitle = new Paragraph($"Doctor: {doctor.Nombres} {doctor.Apellidos}\nMes: {(mes.HasValue ? new DateTime(2000, mes.Value, 1).ToString("MMMM") : "Todos")}\nFecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}", subTitleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20f };
                doc.Add(subTitle);

                // Tabla de citas
                var table = new PdfPTable(5) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 18, 15, 22, 30, 15 });

                // Encabezados con fondo gris oscuro y texto blanco
                string[] headers = { "Fecha Cita", "Hora Cita", "Paciente", "Indicaciones", "Estado" };
                foreach (var h in headers)
                {
                    var cell = new PdfPCell(new Phrase(h, headerFont))
                    {
                        BackgroundColor = new BaseColor(60, 60, 60), // Gris oscuro
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 6
                    };
                    table.AddCell(cell);
                }

                // Filas de datos
                foreach (var c in citasList)
                {
                    var fecha = c.FechaCita.HasValue ? c.FechaCita.Value.ToString("dd/MM/yyyy") : "";
                    var hora = c.DoctorHorarioDetalle?.TurnoHora.HasValue == true ? c.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                    var paciente = c.Usuario != null ? (c.Usuario.Nombre + " " + c.Usuario.Apellido) : "";
                    var indicaciones = string.IsNullOrEmpty(c.Indicaciones) ? "-" : c.Indicaciones;
                    var estado = c.EstadoCita?.Nombre ?? "";
                    table.AddCell(new PdfPCell(new Phrase(fecha, cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(hora, cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(paciente, cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(indicaciones, cellFont)) { Padding = 5 });
                    var estadoCell = new PdfPCell(new Phrase(estado, cellFont)) { Padding = 5 };
                    if (estado.ToLower() == "atendido")
                    {
                        estadoCell.BackgroundColor = new BaseColor(76, 175, 80); // Verde
                        estadoCell.Phrase = new Phrase("Atendido", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE));
                        estadoCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    }
                    table.AddCell(estadoCell);
                }
                doc.Add(table);
                doc.Close();
                return File(ms.ToArray(), "application/pdf", "ReporteCitasAtendidas.pdf");
            }
        }

        // GET: Doctor/ReporteCitasAsignadasPDF
        [HttpGet]
        public FileResult ReporteCitasAsignadasPDF(int? mes = null)
        {
            var rolUsuario = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            var idUsuario = Session["IdUsuario"] as int?;
            if (rolUsuario != "doctor" || idUsuario == null)
            {
                using (var ms = new MemoryStream())
                {
                    var doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, ms);
                    doc.Open();
                    doc.Add(new Paragraph("Error de sesión o rol. No tienes permisos para generar este reporte."));
                    doc.Add(new Paragraph($"RolUsuario: {rolUsuario}, IdUsuario: {idUsuario}"));
                    doc.Close();
                    return File(ms.ToArray(), "application/pdf", "ErrorReporteCitasAsignadas.pdf");
                }
            }
            var doctor = db.Doctores.FirstOrDefault(d => d.NumeroDocumentoIdentidad == db.Usuarios.Where(u => u.IdUsuario == idUsuario).Select(u => u.NumeroDocumentoIdentidad).FirstOrDefault());
            if (doctor == null)
            {
                using (var ms = new MemoryStream())
                {
                    var doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, ms);
                    doc.Open();
                    doc.Add(new Paragraph("No se encontró el doctor asociado al usuario."));
                    doc.Close();
                    return File(ms.ToArray(), "application/pdf", "ErrorReporteCitasAsignadas.pdf");
                }
            }
            var citas = db.Citas
                .Include("Usuario")
                .Include("EstadoCita")
                .Include("DoctorHorarioDetalle")
                .Include("DoctorHorarioDetalle.DoctorHorario")
                .Where(c => c.DoctorHorarioDetalle.DoctorHorario.IdDoctor == doctor.IdDoctor && c.EstadoCita.Nombre == "Pendiente");
            if (mes.HasValue)
            {
                citas = citas.Where(c => c.FechaCita.HasValue && c.FechaCita.Value.Month == mes.Value);
            }
            var citasList = citas.ToList();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Título principal
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLUE);
                var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.DARK_GRAY);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

                var title = new Paragraph("REPORTE DE CITAS ASIGNADAS", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 10f };
                doc.Add(title);
                var subTitle = new Paragraph($"Doctor: {doctor.Nombres} {doctor.Apellidos}\nMes: {(mes.HasValue ? new DateTime(2000, mes.Value, 1).ToString("MMMM") : "Todos")}\nFecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}", subTitleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 20f };
                doc.Add(subTitle);

                // Tabla de citas
                var table = new PdfPTable(5) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 18, 15, 22, 15, 30 });

                // Encabezados con fondo gris oscuro y texto blanco
                string[] headers = { "Fecha Cita", "Hora Cita", "Paciente", "Estado", "Indicaciones" };
                foreach (var h in headers)
                {
                    var cell = new PdfPCell(new Phrase(h, headerFont))
                    {
                        BackgroundColor = new BaseColor(60, 60, 60), // Gris oscuro
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 6
                    };
                    table.AddCell(cell);
                }

                // Filas de datos
                foreach (var c in citasList)
                {
                    var fecha = c.FechaCita.HasValue ? c.FechaCita.Value.ToString("dd/MM/yyyy") : "";
                    var hora = c.DoctorHorarioDetalle?.TurnoHora.HasValue == true ? c.DoctorHorarioDetalle.TurnoHora.Value.ToString("hh\\:mm") : "";
                    var paciente = c.Usuario != null ? (c.Usuario.Nombre + " " + c.Usuario.Apellido) : "";
                    var estado = c.EstadoCita?.Nombre ?? "";
                    var indicaciones = string.IsNullOrEmpty(c.Indicaciones) ? "-" : c.Indicaciones;
                    table.AddCell(new PdfPCell(new Phrase(fecha, cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(hora, cellFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(paciente, cellFont)) { Padding = 5 });
                    var estadoCell = new PdfPCell(new Phrase(estado, cellFont)) { Padding = 5 };
                    if (estado.ToLower() == "pendiente")
                    {
                        estadoCell.BackgroundColor = new BaseColor(33, 150, 243); // Azul
                        estadoCell.Phrase = new Phrase("Pendiente", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE));
                        estadoCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    }
                    table.AddCell(estadoCell);
                    table.AddCell(new PdfPCell(new Phrase(indicaciones, cellFont)) { Padding = 5 });
                }
                doc.Add(table);
                doc.Close();
                return File(ms.ToArray(), "application/pdf", "ReporteCitasAsignadas.pdf");
            }
        }

        // GET: Doctor/ReporteDoctoresPDF
        [HttpGet]
        public FileResult ReporteDoctoresPDF()
        {
            var rol = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            if (rol != "administrador")
            {
                using (var msErr = new MemoryStream())
                {
                    var docErr = new Document(PageSize.A4);
                    PdfWriter.GetInstance(docErr, msErr);
                    docErr.Open();
                    docErr.Add(new Paragraph("No tienes permisos para generar este reporte."));
                    docErr.Close();
                    return File(msErr.ToArray(), "application/pdf", "ErrorReporteDoctores.pdf");
                }
            }

            var doctores = db.Doctores
                .Include(d => d.Especialidad)
                .OrderBy(d => d.Apellidos)
                .ToList();

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36, 36, 36, 36);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLUE);
                var subTitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.DARK_GRAY);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);

                var title = new Paragraph("REPORTE DE DOCTORES", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 8f };
                doc.Add(title);
                var subTitle = new Paragraph($"Total: {doctores.Count}    |    Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}", subTitleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 16f };
                doc.Add(subTitle);

                var table = new PdfPTable(5) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 18, 22, 22, 18, 20 });

                string[] headers = { "Documento", "Nombres", "Apellidos", "Género", "Especialidad" };
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

                foreach (var d in doctores)
                {
                    table.AddCell(new PdfPCell(new Phrase(d.NumeroDocumentoIdentidad ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(d.Nombres ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(d.Apellidos ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(d.Genero ?? "-", cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(d.Especialidad?.Nombre ?? "-", cellFont)) { Padding = 4 });
                }

                doc.Add(table);
                doc.Close();
                return File(ms.ToArray(), "application/pdf", "ReporteDoctores.pdf");
            }
        }
    }
}

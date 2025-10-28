using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Clinica.Models;
using iTextSharp.text; // agregado para PDF
using iTextSharp.text.pdf; // agregado para PDF

namespace Clinica.Controllers
{
    public class UsuarioController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        private bool UsuarioAutenticado()
        {
            return Session["IdUsuario"] != null;
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Permitir acceso libre a Register (GET y POST)
            var action = filterContext.ActionDescriptor.ActionName.ToLower();
            if (action == "register")
            {
                base.OnActionExecuting(filterContext);
                return;
            }
            if (!UsuarioAutenticado())
            {
                filterContext.Result = RedirectToAction("Index", "Login");
            }
            base.OnActionExecuting(filterContext);
        }

        // GET: Usuario
        public ActionResult Index()
        {
            var roles = db.RolUsuarios
                .AsNoTracking()
                .ToList();

            var usuarios = db.Usuarios
                .Include(u => u.Rol)
                .AsNoTracking()
                .ToList();

            ViewBag.Roles = roles;
            return View(usuarios);
        }

        // GET: Usuario/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return HttpNotFound();
            var usuario = db.Usuarios.Include(u => u.Rol).FirstOrDefault(u => u.IdUsuario == id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        // GET: Usuario/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return HttpNotFound();
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            ViewBag.Roles = db.RolUsuarios.AsNoTracking().ToList();
            return View(usuario);
        }

        // POST: Usuario/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Usuario usuario)
        {
            // Remover validación de Clave del ModelState si viene vacía (es opcional al editar)
            if (string.IsNullOrWhiteSpace(usuario.Clave))
            {
                ModelState.Remove("Clave");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            // Cargar existente para preservar campos si aplica
            var existente = db.Usuarios.FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario);
            if (existente == null)
            {
                return Json(new { success = false, errors = new { General = new[] { "Usuario no encontrado." } } });
            }

            var correo = (usuario.Correo ?? string.Empty).Trim();
            var documento = (usuario.NumeroDocumentoIdentidad ?? string.Empty).Trim();
            
            // Documento único excluyendo el mismo Id
            bool existeDocumento = db.Usuarios.Any(u => u.NumeroDocumentoIdentidad == documento && u.IdUsuario != usuario.IdUsuario);
            if (existeDocumento)
            {
                return Json(new { success = false, errors = new { NumeroDocumentoIdentidad = new[] { "Ya existe un usuario con este número de documento" } } });
            }

            // Correo único excluyendo el mismo Id
            bool existeCorreo = db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower() && u.IdUsuario != usuario.IdUsuario);
            if (existeCorreo)
            {
                return Json(new { success = false, errors = new { Correo = new[] { "Ya existe un usuario con este correo electrónico" } } });
            }

            // Actualizar campos
            existente.NumeroDocumentoIdentidad = documento;
            existente.Nombre = usuario.Nombre?.Trim();
            existente.Apellido = usuario.Apellido?.Trim();
            existente.Correo = correo;
            existente.IdRolUsuario = usuario.IdRolUsuario;

            // Si se envía nueva clave, hashearla; si viene vacía, conservar la actual
            if (!string.IsNullOrWhiteSpace(usuario.Clave))
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(usuario.Clave);
                    existente.Clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                }
            }

            db.SaveChanges();
            return Json(new { success = true, message = "Usuario editado correctamente." });
        }

        // GET: Usuario/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return HttpNotFound();
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        // POST: Usuario/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var usuario = db.Usuarios.Find(id);
            if (usuario != null)
            {
                try
                {
                    db.Usuarios.Remove(usuario);
                    db.SaveChanges();
                    TempData["Success"] = "Usuario eliminado correctamente.";
                }
                catch (System.Data.Entity.Infrastructure.DbUpdateException ex)
                {
                    var sqlEx = ex.InnerException?.InnerException as System.Data.SqlClient.SqlException;
                    if (sqlEx != null && sqlEx.Message.Contains("REFERENCE constraint"))
                    {
                        TempData["Error"] = "No se puede eliminar el usuario porque tiene citas asociadas.";
                    }
                    else
                    {
                        TempData["Error"] = "No se pudo eliminar el usuario. Detalle: " + (ex.InnerException?.Message ?? ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error inesperado: " + ex.Message;
                }
            }
            else
            {
                TempData["Error"] = "Usuario no encontrado.";
            }
            return RedirectToAction("Index");
        }

        // POST: Usuario/Create (desde modal)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Usuario usuario)
        {
            // Validar que la contraseña sea obligatoria al crear
            if (string.IsNullOrWhiteSpace(usuario.Clave))
            {
                ModelState.AddModelError("Clave", "La contraseña es obligatoria");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                return Json(new { success = false, errors = errors });
            }

            var correo = (usuario.Correo ?? string.Empty).Trim();
            var documento = (usuario.NumeroDocumentoIdentidad ?? string.Empty).Trim();

            // Validar documento único
            var existeDocumento = db.Usuarios.Any(u => u.NumeroDocumentoIdentidad == documento);
            if (existeDocumento)
            {
                return Json(new { success = false, errors = new { NumeroDocumentoIdentidad = new[] { "Ya existe un usuario con este número de documento" } } });
            }

            // Validar correo único
            var existeCorreo = db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower());
            if (existeCorreo)
            {
                return Json(new { success = false, errors = new { Correo = new[] { "Ya existe un usuario con este correo electrónico" } } });
            }

            // Hash de contraseña SHA256 en HEX
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(usuario.Clave ?? string.Empty);
                usuario.Clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }

            usuario.Correo = correo;
            usuario.NumeroDocumentoIdentidad = documento;
            usuario.FechaCreacion = DateTime.Now;
            db.Usuarios.Add(usuario);
            db.SaveChanges();
            
            return Json(new { success = true, message = "Usuario creado correctamente." });
        }

        // POST: Usuario/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Usuario usuario, string ConfirmarClave)
        {
            // Remover validación de IdRolUsuario ya que se asigna automáticamente
            ModelState.Remove("IdRolUsuario");
            
            if (!ModelState.IsValid)
            {
                return View(usuario);
            }

            if (string.IsNullOrWhiteSpace(usuario.Clave) || string.IsNullOrWhiteSpace(ConfirmarClave))
            {
                ModelState.AddModelError("Clave", "La contraseña es obligatoria.");
                return View(usuario);
            }
            
            if (usuario.Clave != ConfirmarClave)
            {
                ModelState.AddModelError("ConfirmarClave", "Las contraseñas no coinciden.");
                return View(usuario);
            }

            var correo = (usuario.Correo ?? string.Empty).Trim();
            var documento = (usuario.NumeroDocumentoIdentidad ?? string.Empty).Trim();
            
            if (db.Usuarios.Any(u => u.NumeroDocumentoIdentidad == documento))
            {
                ModelState.AddModelError("NumeroDocumentoIdentidad", "Ya existe un usuario con este número de documento.");
                return View(usuario);
            }
            
            if (db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower()))
            {
                ModelState.AddModelError("Correo", "Ya existe un usuario con este correo electrónico.");
                return View(usuario);
            }
            
            // Asignar rol Paciente automáticamente
            var rolPaciente = db.RolUsuarios.FirstOrDefault(r => r.Nombre.ToLower() == "paciente");
            if (rolPaciente == null)
            {
                rolPaciente = new RolUsuario { Nombre = "Paciente", FechaCreacion = DateTime.Now };
                db.RolUsuarios.Add(rolPaciente);
                db.SaveChanges();
            }
            usuario.IdRolUsuario = rolPaciente.IdRolUsuario;
            
            // Hash de contraseña SHA256 en HEX
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(usuario.Clave ?? string.Empty);
                usuario.Clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
            
            usuario.Correo = correo;
            usuario.NumeroDocumentoIdentidad = documento;
            usuario.FechaCreacion = DateTime.Now;
            db.Usuarios.Add(usuario);
            db.SaveChanges();
            
            ViewBag.Success = "Cuenta creada correctamente. Ahora puedes iniciar sesión.";
            ModelState.Clear();
            return View(new Usuario());
        }

        // GET: Usuario/Register
        [HttpGet]
        public ActionResult Register()
        {
            return View(new Usuario());
        }

        // GET: Usuario/ReporteUsuariosPDF
        [HttpGet]
        public FileResult ReporteUsuariosPDF()
        {
            var rol = (Session["RolUsuario"] as string ?? string.Empty).Trim().ToLower();
            if (rol != "administrador")
            {
                using (var msErr = new System.IO.MemoryStream())
                {
                    var docErr = new Document(PageSize.A4);
                    PdfWriter.GetInstance(docErr, msErr);
                    docErr.Open();
                    docErr.Add(new Paragraph("No tienes permisos para generar este reporte."));
                    docErr.Close();
                    return File(msErr.ToArray(), "application/pdf", "ErrorReporteUsuarios.pdf");
                }
            }

            var usuarios = db.Usuarios
                .Include(u => u.Rol)
                .OrderByDescending(u => u.FechaCreacion)
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

                var title = new Paragraph("REPORTE DE USUARIOS REGISTRADOS", titleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 8f };
                doc.Add(title);
                var subTitle = new Paragraph($"Total: {usuarios.Count}    |    Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}", subTitleFont) { Alignment = Element.ALIGN_CENTER, SpacingAfter = 16f };
                doc.Add(subTitle);

                var table = new PdfPTable(6) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 18, 18, 18, 24, 12, 18 });

                string[] headers = { "Documento", "Nombre", "Apellido", "Correo", "Rol", "Creación" };
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

                foreach (var u in usuarios)
                {
                    table.AddCell(new PdfPCell(new Phrase(u.NumeroDocumentoIdentidad ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(u.Nombre ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(u.Apellido ?? "-", cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(string.IsNullOrWhiteSpace(u.Correo) ? "-" : u.Correo, cellFont)) { Padding = 4 });
                    table.AddCell(new PdfPCell(new Phrase(u.Rol?.Nombre ?? "-", cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase(u.FechaCreacion.HasValue ? u.FechaCreacion.Value.ToString("dd/MM/yyyy") : "-", cellFont)) { Padding = 4, HorizontalAlignment = Element.ALIGN_CENTER });
                }

                doc.Add(table);
                doc.Close();
                return File(ms.ToArray(), "application/pdf", "ReporteUsuariosRegistrados.pdf");
            }
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
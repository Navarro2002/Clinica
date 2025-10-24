using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Clinica.Models;

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
            // Cargar existente para preservar campos si aplica
            var existente = db.Usuarios.FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario);
            if (existente == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index");
            }

            var correo = (usuario.Correo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                TempData["Error"] = "El correo es obligatorio.";
                return RedirectToAction("Index");
            }
            if (!usuario.IdRolUsuario.HasValue || usuario.IdRolUsuario.Value == 0)
            {
                TempData["Error"] = "El rol es obligatorio.";
                return RedirectToAction("Index");
            }

            // Correo único excluyendo el mismo Id
            bool existeCorreo = db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower() && u.IdUsuario != usuario.IdUsuario);
            if (existeCorreo)
            {
                TempData["Error"] = "Ya existe un usuario con ese correo.";
                return RedirectToAction("Index");
            }

            // Actualizar campos
            existente.NumeroDocumentoIdentidad = usuario.NumeroDocumentoIdentidad?.Trim();
            existente.Nombre = usuario.Nombre?.Trim();
            existente.Apellido = usuario.Apellido?.Trim();
            existente.Correo = correo;
            existente.IdRolUsuario = usuario.IdRolUsuario;

            // Si se envía nueva clave, hashearla; si viene vacía, conservar
            if (!string.IsNullOrWhiteSpace(usuario.Clave))
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(usuario.Clave);
                    existente.Clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                }
            }

            db.SaveChanges();
            TempData["Success"] = "Usuario editado correctamente.";
            return RedirectToAction("Index");
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
            var correo = (usuario.Correo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                TempData["Error"] = "El correo es obligatorio.";
                return RedirectToAction("Index");
            }
            if (!usuario.IdRolUsuario.HasValue || usuario.IdRolUsuario.Value == 0)
            {
                TempData["Error"] = "El rol es obligatorio.";
                return RedirectToAction("Index");
            }

            // Validar correo único
            var existe = db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower());
            if (existe)
            {
                TempData["Error"] = "Ya existe un usuario con ese correo.";
                return RedirectToAction("Index");
            }

            // Hash de contraseña SHA256 en HEX
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(usuario.Clave ?? string.Empty);
                usuario.Clave = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }

            usuario.Correo = correo;
            usuario.FechaCreacion = DateTime.Now;
            db.Usuarios.Add(usuario);
            db.SaveChanges();
            TempData["Success"] = "Usuario creado correctamente.";
            return RedirectToAction("Index");
        }

        // POST: Usuario/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(Usuario usuario, string ConfirmarClave)
        {
            var correo = (usuario.Correo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correo))
            {
                ViewBag.Error = "El correo es obligatorio.";
                return View();
            }
            if (string.IsNullOrWhiteSpace(usuario.Clave) || string.IsNullOrWhiteSpace(ConfirmarClave))
            {
                ViewBag.Error = "La contraseña es obligatoria.";
                return View();
            }
            if (usuario.Clave != ConfirmarClave)
            {
                ViewBag.Error = "Las contraseñas no coinciden.";
                return View();
            }
            if (db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower()))
            {
                ViewBag.Error = "Ya existe un usuario con ese correo.";
                return View();
            }
            // Asignar rol Paciente
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
            usuario.FechaCreacion = DateTime.Now;
            db.Usuarios.Add(usuario);
            db.SaveChanges();
            ViewBag.Success = "Cuenta creada correctamente. Ahora puedes iniciar sesión.";
            ModelState.Clear();
            return View();
        }

        // GET: Usuario/Register
        [HttpGet]
        public ActionResult Register()
        {
            return View();
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
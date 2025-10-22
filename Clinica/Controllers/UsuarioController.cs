using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Clinica.Models;

namespace Clinica.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ClinicaContext _db = new ClinicaContext();

        // GET: Usuario
        public ActionResult Index()
        {
            var roles = _db.RolUsuarios
                .AsNoTracking()
                .ToList();

            var usuarios = _db.Usuarios
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
            var usuario = _db.Usuarios.Include(u => u.Rol).FirstOrDefault(u => u.IdUsuario == id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        // GET: Usuario/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null) return HttpNotFound();
            var usuario = _db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            ViewBag.Roles = _db.RolUsuarios.AsNoTracking().ToList();
            return View(usuario);
        }

        // POST: Usuario/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Usuario usuario)
        {
            // Cargar existente para preservar campos si aplica
            var existente = _db.Usuarios.FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario);
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
            bool existeCorreo = _db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower() && u.IdUsuario != usuario.IdUsuario);
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

            _db.SaveChanges();
            TempData["Success"] = "Usuario editado correctamente.";
            return RedirectToAction("Index");
        }

        // GET: Usuario/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null) return HttpNotFound();
            var usuario = _db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        // POST: Usuario/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var usuario = _db.Usuarios.Find(id);
            if (usuario != null)
            {
                _db.Usuarios.Remove(usuario);
                _db.SaveChanges();
                TempData["Success"] = "Usuario eliminado correctamente.";
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
            var existe = _db.Usuarios.Any(u => u.Correo.ToLower() == correo.ToLower());
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
            _db.Usuarios.Add(usuario);
            _db.SaveChanges();
            TempData["Success"] = "Usuario creado correctamente.";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
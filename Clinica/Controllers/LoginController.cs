using System;
using System.Linq;
using System.Web.Mvc;
using Clinica.Models;
using System.Security.Cryptography;
using System.Text;

namespace Clinica.Controllers
{
    public class LoginController : Controller
    {
        private ClinicaContext db = new ClinicaContext();

        // GET: Login
        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(string NumeroDocumentoIdentidad, string Clave)
        {
            if (string.IsNullOrWhiteSpace(NumeroDocumentoIdentidad) || string.IsNullOrWhiteSpace(Clave))
            {
                ViewBag.Error = "Ingrese su documento y contraseña.";
                return View();
            }

            // Encriptar la clave para comparar (SHA-256)
            string claveHash = GetSha256(Clave);
            var usuario = db.Usuarios.FirstOrDefault(u => u.NumeroDocumentoIdentidad == NumeroDocumentoIdentidad && u.Clave == claveHash);

            if (usuario == null)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos.";
                return View();
            }

            // Guardar datos en sesión
            Session["IdUsuario"] = usuario.IdUsuario;
            Session["NombreUsuario"] = usuario.Nombre + " " + usuario.Apellido;
            Session["RolUsuario"] = usuario.Rol?.Nombre;

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Index", "Login");
        }

        private string GetSha256(string str)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}

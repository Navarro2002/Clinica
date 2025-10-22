using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Data.Entity;
using Clinica.Models;

namespace Clinica
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Registrar e inicializar el seeding de base de datos
            Database.SetInitializer(new ClinicaInitializer());
            using (var db = new ClinicaContext())
            {
                db.Database.Initialize(false); // crea y ejecuta Seed si no existe
            }
        }
    }
}

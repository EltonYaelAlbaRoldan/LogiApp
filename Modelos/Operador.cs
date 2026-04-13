using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogiApp.Modelos
{
    public class Operador
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string NumLicencia { get; set; }
        public DateTime VigenciaLicencia { get; set; }
        public DateTime VigenciaMedico { get; set; }
        public string LinkCarpeta { get; set; }
        public string Estado { get; set; }
    }
}

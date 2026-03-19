using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogiApp.Modelos
{
    public class ViajeHistorial
    {
        public string Folio { get; set; }
        public string Fecha { get; set; }
        public string Unidad { get; set; } 
        public string Estado { get; set; }

        // Lógica para que el color del "Badge" cambie solo
        public Color ColorEstado => Estado == "FINALIZADO" ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF9800");
    }
}

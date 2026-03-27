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
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Rendimiento { get; set; }
        public string Duracion { get; set; }
        public string FechaLlegada { get; set; }    
        public string KmRecorrido { get; set; }      
        public string CombConsumido { get; set; } 

        public Color ColorEstado => Estado == "FINALIZADO" ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF9800");
    }
}

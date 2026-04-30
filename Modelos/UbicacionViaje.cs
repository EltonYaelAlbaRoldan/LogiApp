using SQLite;

namespace LogiApp.Modelos
{
    public class UbicacionViaje
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? FolioViaje { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public DateTime FechaHora { get; set; }
        public bool Sincronizado { get; set; }
    }
}
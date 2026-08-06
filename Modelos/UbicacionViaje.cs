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
        public string? HojaTracking { get; set; }
        public string? ColumnaTrackingA { get; set; }
        public string? ColumnaTrackingB { get; set; }
        public int FilaTracking { get; set; }
    }
}

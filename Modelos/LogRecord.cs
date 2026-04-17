using SQLite; 
using System;

namespace LogiApp.Modelos
{
    public class LogRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string? TipoError { get; set; }
        public string? Mensaje { get; set; }
        public string? StackTrace { get; set; }
        public bool Sincronizado { get; set; }

        // NUEVA COLUMNA
        public string? IdOperador { get; set; }
    }
}
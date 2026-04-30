using SQLite;
using System.IO;
using LogiApp.Modelos;

namespace LogiApp.Servicios
{
    public class BaseDeDatosLocal
    {
        private SQLiteAsyncConnection? _database;

        public async Task InicializarAsync()
        {
            if (_database != null) return;

            // Crea la ruta del archivo en una carpeta segura del celular
            string ruta = Path.Combine(FileSystem.AppDataDirectory, "LogiAppLocal.db3");
            _database = new SQLiteAsyncConnection(ruta);

            // Crea la tabla de Logs si no existe
            await _database.CreateTableAsync<LogRecord>();
        }

        public async Task<int> InsertarLogAsync(LogRecord log)
        {
            await InicializarAsync();
            return await _database.InsertAsync(log);
        }
        // Obtiene todos los logs que aún no se han mandado a la nube
        public async Task<List<LogRecord>> ObtenerLogsPendientesAsync()
        {
            await InicializarAsync();
            return await _database.Table<LogRecord>().Where(x => !x.Sincronizado).ToListAsync();
        }

        // Borra los logs que ya se subieron para no llenar la memoria del cel
        public async Task EliminarLogsSincronizadosAsync()
        {
            await InicializarAsync();
            await _database.Table<LogRecord>().Where(x => x.Sincronizado).DeleteAsync();
        }
        public async Task BorrarLogPorIdAsync(int id)
        {
            await InicializarAsync();
            // Borra el error específico para que no se duplique
            await _database.Table<LogRecord>().Where(x => x.Id == id).DeleteAsync();
        }

        // ACTUALIZA un registro (necesario cuando cambias Sincronizado a true)
        public async Task<int> ActualizarLogAsync(LogRecord log)
        {
            await InicializarAsync();
            return await _database.UpdateAsync(log);
        }



        public async Task GuardarUbicacionLocalAsync(UbicacionViaje ubi)
        {
            await InicializarAsync();
            await _database.InsertAsync(ubi);
        }

        public async Task<List<UbicacionViaje>> ObtenerUbicacionesPendientesAsync()
        {
            await InicializarAsync();
            return await _database.Table<UbicacionViaje>().ToListAsync();
        }

        public async Task BorrarUbicacionAsync(int id)
        {
            await InicializarAsync();
            await _database.DeleteAsync<UbicacionViaje>(id);
        }
    }
}
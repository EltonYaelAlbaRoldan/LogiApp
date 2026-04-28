using SQLite;
using System.IO;
using LogiApp.Modelos;
using System.Threading;
using System.Threading.Tasks;

namespace LogiApp.Servicios
{
    public class BaseDeDatosLocal
    {
        private static SQLiteAsyncConnection _database;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public BaseDeDatosLocal()
        {
            // Inicialización inmediata al crear la clase
            Inicializar();
        }

        private void Inicializar()
        {
            if (_database != null) return;

            string ruta = Path.Combine(FileSystem.AppDataDirectory, "LogiAppLocal.db3");
            _database = new SQLiteAsyncConnection(ruta);

            // Forzamos la creación de la tabla para que esté lista siempre
            _database.CreateTableAsync<LogRecord>().Wait();
        }

        public async Task<int> InsertarLogAsync(LogRecord log)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _database.InsertAsync(log);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<LogRecord>> ObtenerLogsPendientesAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _database.Table<LogRecord>().Where(x => !x.Sincronizado).ToListAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<int> ActualizarLogAsync(LogRecord log)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _database.UpdateAsync(log);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task EliminarLogsSincronizadosAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                await _database.Table<LogRecord>().Where(x => x.Sincronizado).DeleteAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task BorrarLogPorIdAsync(int id)
        {
            await _semaphore.WaitAsync();
            try
            {
                await _database.Table<LogRecord>().Where(x => x.Id == id).DeleteAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
using SQLite;
using System.IO;
using LogiApp.Modelos;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace LogiApp.Servicios
{
    public class BaseDeDatosLocal
    {
        private static SQLiteAsyncConnection _database;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public BaseDeDatosLocal()
        {
            Inicializar();
        }

        private void Inicializar()
        {
            // Si la conexión ya está lista, no hacemos nada.
            if (_database != null) return;

            string ruta = Path.Combine(FileSystem.AppDataDirectory, "LogiAppLocal.db3");

            try
            {
                // 1. CREACIÓN SÍNCRONA (A prueba de balas contra choque de hilos)
                using (var conexionSegura = new SQLiteConnection(ruta))
                {
                    conexionSegura.CreateTable<LogRecord>();
                    conexionSegura.CreateTable<UbicacionViaje>();
                }

                // 2. CONEXIÓN ASÍNCRONA
                _database = new SQLiteAsyncConnection(ruta);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al inicializar SQLite: {ex.Message}");
            }
        }

        #region Métodos para LogRecord
        public async Task<int> InsertarLogAsync(LogRecord log)
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); return await _database.InsertAsync(log); }
            finally { _semaphore.Release(); }
        }

        public async Task<List<LogRecord>> ObtenerLogsPendientesAsync()
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); return await _database.Table<LogRecord>().Where(x => !x.Sincronizado).ToListAsync(); }
            finally { _semaphore.Release(); }
        }

        public async Task<int> ActualizarLogAsync(LogRecord log)
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); return await _database.UpdateAsync(log); }
            finally { _semaphore.Release(); }
        }

        public async Task EliminarLogsSincronizadosAsync()
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); await _database.Table<LogRecord>().Where(x => x.Sincronizado).DeleteAsync(); }
            finally { _semaphore.Release(); }
        }

        public async Task BorrarLogPorIdAsync(int id)
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); await _database.Table<LogRecord>().Where(x => x.Id == id).DeleteAsync(); }
            finally { _semaphore.Release(); }
        }
        #endregion

        #region Métodos para UbicacionViaje (GPS Offline)
        public async Task GuardarUbicacionLocalAsync(UbicacionViaje ubi)
        {
            await _semaphore.WaitAsync();
            try
            {
                Inicializar();
                var ultima = await _database.Table<UbicacionViaje>()
                    .Where(x => x.FolioViaje == ubi.FolioViaje)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                // Un mismo punto puede llegar desde el botón y desde el servicio casi al
                // mismo tiempo. No repetimos coordenadas prácticamente idénticas.
                if (ultima != null &&
                    Math.Abs(ultima.Latitud - ubi.Latitud) <= 0.00001 &&
                    Math.Abs(ultima.Longitud - ubi.Longitud) <= 0.00001)
                {
                    ubi.Id = ultima.Id;
                    return;
                }

                await _database.InsertAsync(ubi);
            }
            finally { _semaphore.Release(); }
        }

        public async Task<List<UbicacionViaje>> ObtenerUbicacionesPendientesAsync()
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); return await _database.Table<UbicacionViaje>().ToListAsync(); }
            finally { _semaphore.Release(); }
        }

        public async Task BorrarUbicacionAsync(int id)
        {
            await _semaphore.WaitAsync();
            try { Inicializar(); await _database.DeleteAsync<UbicacionViaje>(id); }
            finally { _semaphore.Release(); }
        }
        #endregion
    }
}

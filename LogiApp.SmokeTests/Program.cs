using LogiApp.Servicios;

static void Verificar(bool condicion, string mensaje)
{
    if (!condicion) throw new InvalidOperationException(mensaje);
}

const string password = "UnaPasswordSegura123!";
string hash1 = SeguridadPassword.CrearHash(password);
string hash2 = SeguridadPassword.CrearHash(password);

Verificar(SeguridadPassword.EsHash(hash1), "El valor generado debe identificarse como hash.");
Verificar(hash1 != hash2, "Dos hashes de la misma contraseña deben usar salts distintos.");
Verificar(SeguridadPassword.Verificar(password, hash1), "La contraseña correcta debe validarse.");
Verificar(!SeguridadPassword.Verificar("incorrecta", hash1), "Una contraseña incorrecta debe rechazarse.");
Verificar(SeguridadPassword.Verificar("legado", "legado"), "Las contraseñas heredadas deben poder migrarse al iniciar sesión.");
Verificar(!SeguridadPassword.Verificar("otra", "legado"), "Una contraseña heredada incorrecta debe rechazarse.");

Console.WriteLine("Smoke tests completados correctamente.");

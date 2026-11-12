# Backend

La lógica del sistema, en C#. Aquí no hay HTML.

| Carpeta | Qué contiene | Ejemplo |
|---|---|---|
| `Controladores/` | Una clase por sección del sistema. Cada método público es una página o una acción (guardar, anular). Deciden qué vista mostrar y quién puede entrar. | `HomeController.Index()` muestra el Inicio del administrador o del vendedor. |
| `AccesoDatos/` | La conexión a SQL Server (`Db.cs`) y los **repositorios**: clases con las consultas SQL hechas con ADO.NET (`SqlCommand`, `SqlDataReader`). | `UsuarioRepositorio.ObtenerPorNombreAsync()` busca un usuario para el login. |
| `Modelos/` | Clases que representan una fila de una tabla. | `Usuario` = una fila de `dbo.Usuarios`. |
| `Seguridad/` | Roles, cifrado de contraseñas (PBKDF2) y atajos para saber quién está conectado. | `User.EsAdministrador()` |

**Nombres de los controladores:** ASP.NET exige que terminen en `Controller` (`VentasController`). La parte anterior es la que aparece en la dirección: `VentasController` → `/Ventas`.

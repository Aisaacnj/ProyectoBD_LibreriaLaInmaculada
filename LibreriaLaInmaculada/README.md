# Librería La Inmaculada: Sistema web

Sistema de inventario y facturación en **ASP.NET Core MVC (.NET 8)**, C#, vistas Razor y **ADO.NET** contra SQL Server.

## Cómo está organizado

```
LibreriaLaInmaculada/
│
├── BaseDeDatos/                  ← SCRIPTS SQL
│   ├── LibreriaLaInmaculada_v7.sql         1° crea la base vacía
│   └── LibreriaLaInmaculada_v7_datos.sql   2° la llena con datos de prueba
│
├── Backend/                      ← LÓGICA (C#): recibe peticiones, consulta la base, decide permisos
│   ├── Controladores/    Reciben cada clic o formulario y eligen qué pantalla mostrar
│   ├── AccesoDatos/      ADO.NET: conexión y consultas SQL (repositorios)
│   ├── Modelos/          Clases que representan filas de la base (Usuario, ...)
│   └── Seguridad/        Roles, cifrado de contraseñas, datos del usuario conectado
│
├── Frontend/                     ← LO QUE SE VE (Razor, HTML, CSS)
│   ├── Vistas/           Pantallas .cshtml, una carpeta por controlador
│   │   ├── Shared/       Partes comunes: menú (_Layout), decoración, errores
│   │   ├── Cuenta/       Login y acceso denegado
│   │   └── Home/         Inicio del administrador y del vendedor
│   ├── ModelosDeVista/   Los datos que cada pantalla necesita mostrar
│   ├── Ayudantes/        Formato de córdobas y fechas, iconos
│   └── wwwroot/css/      site.css con todo el diseño
│
├── Properties/launchSettings.json   Puertos (https 7180, http 5180)
├── Program.cs                       Arranque: une Backend, Frontend y base de datos
├── appsettings.json                 Cadena de conexión a SQL Server
└── LibreriaLaInmaculada.csproj      Proyecto (.NET 8 + Microsoft.Data.SqlClient)
```

**Cómo viaja una petición:** el navegador pide una página → un **Controlador** (Backend) la recibe → le pide los datos a un **repositorio** de AccesoDatos, que ejecuta el SQL → el controlador arma un **ModeloDeVista** → la **Vista** (Frontend) lo convierte en HTML con el estilo de `site.css`.

Cada carpeta tiene su propio `LEEME.md` con más detalle.

## Cómo ponerlo a funcionar

1. En SQL Server Management Studio ejecute los dos scripts de `BaseDeDatos/`, en orden.
2. Revise la cadena de conexión en `appsettings.json`:
   - Con **autenticación de Windows** no cambie nada.
   - Con el usuario **sa**: `Server=.;Database=LibreriaLaInmaculada;User Id=sa;Password=SU_CONTRASEÑA;TrustServerCertificate=True;`
   - Si su instancia tiene nombre, use el que aparece en SSMS. En el JSON la barra va doble: `Server=.\\SQLEXPRESS`.
3. Ejecute:
   - **Visual Studio:** abra `LibreriaLaInmaculada.csproj` y presione F5.
   - **VS Code:** abra esta carpeta, y en la terminal escriba `dotnet run --launch-profile http`. Abra http://localhost:5180 y no toque la terminal mientras lo usa (Ctrl + C lo apaga).

## Usuarios de prueba

| Usuario | Contraseña | Rol |
|---|---|---|
| `ADMIN` | `Admin2026!` | Administrador |
| `jporras` | `Jorge2026!` | Vendedor |

## Reglas para programar los siguientes módulos

- Todas las consultas van con **parámetros** (`cmd.Parameters.Add(...)`), nunca armando el SQL con texto.
- Toda consulta de dinero o cantidades filtra `WHERE Estado = 'Activa'`.
- Registrar o anular ventas y compras, y registrar pérdidas, va dentro de una **SqlTransaction**.
- Lo que es solo del administrador lleva `[Authorize(Roles = Roles.Administrador)]` en el controlador **y** se oculta en la vista con `@if (User.EsAdministrador())`.
- Una pantalla nueva = un método en su controlador (Backend/Controladores) + su vista en `Frontend/Vistas/<Controlador>/<Método>.cshtml`.

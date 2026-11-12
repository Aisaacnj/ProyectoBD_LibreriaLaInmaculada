# Frontend

Todo lo que el usuario ve en el navegador.

| Carpeta | Qué contiene |
|---|---|
| `Vistas/` | Las pantallas, archivos Razor `.cshtml` (HTML con C# usando `@`). Hay una carpeta por controlador: las vistas de `HomeController` están en `Vistas/Home/`. |
| `Vistas/Shared/` | Lo que comparten todas las pantallas: `_Layout.cshtml` (menú lateral por rol y encabezado decorado), `_LayoutLogin.cshtml`, la decoración y las páginas de error. |
| `Vistas/_ViewImports.cshtml` | Los `@using` que usan todas las vistas. |
| `Vistas/_ViewStart.cshtml` | Indica que todas las vistas usan `_Layout`. |
| `ModelosDeVista/` | Clases con exactamente los datos que una pantalla necesita (por ejemplo, `InicioAdminViewModel`). El controlador las llena y la vista las muestra. |
| `Ayudantes/` | `Formato` (córdobas, fechas en español) e `Iconos` (los trazos SVG del menú y las tarjetas). |
| `wwwroot/css/site.css` | Todo el diseño: colores, menú, tarjetas, botones, login. Los colores principales están al inicio, en `:root`. |

**Para cambiar un color en todo el sistema,** edite la variable en `:root` de `site.css`; por ejemplo, `--azul` es el color principal.

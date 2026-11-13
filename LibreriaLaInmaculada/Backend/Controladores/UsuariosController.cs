using LibreriaLaInmaculada.Backend.AccesoDatos;
using LibreriaLaInmaculada.Backend.Seguridad;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.Controladores;

/// <summary>
/// Usuarios: solo el Administrador.
/// Reglas: nadie se desactiva a sí mismo, nadie se quita su propio rol de administrador,
/// y siempre debe quedar al menos un administrador activo.
/// </summary>
[Authorize(Roles = Roles.Administrador)]
public class UsuariosController : Controller
{
    private static readonly string[] RolesValidos = { Roles.Administrador, Roles.Vendedor };
    private readonly UsuarioRepositorio _usuarios;

    public UsuariosController(UsuarioRepositorio usuarios) => _usuarios = usuarios;

    public async Task<IActionResult> Index() => View(await _usuarios.ListarAsync());

    // ---------------------------------------------------------------- Nuevo
    [HttpGet]
    public IActionResult Nuevo() => View(new UsuarioNuevoViewModel { Rol = Roles.Vendedor });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(UsuarioNuevoViewModel vm)
    {
        vm.Usuario = vm.Usuario?.Trim();
        vm.NombreCompleto = Validaciones.LimpiarTexto(vm.NombreCompleto);

        if (vm.Rol is not null && !RolesValidos.Contains(vm.Rol))
            ModelState.AddModelError(nameof(vm.Rol), "Elija un rol válido.");
        if (ModelState.IsValid && await _usuarios.ExisteUsuarioAsync(vm.Usuario!))
            ModelState.AddModelError(nameof(vm.Usuario), "Ese nombre de usuario ya existe.");

        if (ModelState.IsValid)
        {
            try
            {
                await _usuarios.CrearAsync(vm.Usuario!, vm.NombreCompleto!, PasswordHasher.Hash(vm.Password!), vm.Rol!);
                TempData["Mensaje"] = $"Se creó el usuario «{vm.Usuario}». Ya puede iniciar sesión con la contraseña que usted le asignó.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (SqlErrores.EsDuplicado(ex))
            {
                ModelState.AddModelError(nameof(vm.Usuario), "Ese nombre de usuario ya existe.");
            }
        }

        vm.Password = vm.ConfirmarPassword = null;   // nunca se devuelve una contraseña al navegador
        return View(vm);
    }

    // ---------------------------------------------------------------- Editar nombre y rol
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var u = await _usuarios.ObtenerPorIdAsync(id);
        if (u is null) return NotFound();

        return View(new UsuarioEditarViewModel
        {
            IdUsuario = u.IdUsuario,
            Usuario = u.NombreUsuario,
            NombreCompleto = u.NombreCompleto,
            Rol = u.Rol,
            EsUsuarioActual = u.IdUsuario == User.IdUsuario()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(UsuarioEditarViewModel vm)
    {
        var u = await _usuarios.ObtenerPorIdAsync(vm.IdUsuario);
        if (u is null) return NotFound();

        vm.Usuario = u.NombreUsuario;
        vm.EsUsuarioActual = u.IdUsuario == User.IdUsuario();
        vm.NombreCompleto = Validaciones.LimpiarTexto(vm.NombreCompleto);

        if (vm.Rol is not null && !RolesValidos.Contains(vm.Rol))
            ModelState.AddModelError(nameof(vm.Rol), "Elija un rol válido.");

        bool dejaDeSerAdmin = u.Rol == Roles.Administrador && vm.Rol != Roles.Administrador;
        if (dejaDeSerAdmin && vm.EsUsuarioActual)
            ModelState.AddModelError(nameof(vm.Rol), "No puede quitarse a sí mismo el rol de Administrador.");
        else if (dejaDeSerAdmin && u.Activo && await _usuarios.ContarOtrosAdministradoresActivosAsync(u.IdUsuario) == 0)
            ModelState.AddModelError(nameof(vm.Rol), "Es el único administrador activo: debe quedar al menos uno.");

        if (!ModelState.IsValid)
            return View(vm);

        await _usuarios.ActualizarAsync(u.IdUsuario, vm.NombreCompleto!, vm.Rol!);
        TempData["Mensaje"] = u.Rol != vm.Rol
            ? $"Se guardaron los cambios de «{u.NombreUsuario}». El nuevo rol se aplica la próxima vez que inicie sesión."
            : $"Se guardaron los cambios de «{u.NombreUsuario}».";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Cambiar contraseña
    [HttpGet]
    public async Task<IActionResult> Password(int id)
    {
        var u = await _usuarios.ObtenerPorIdAsync(id);
        if (u is null) return NotFound();
        return View(new UsuarioPasswordViewModel { IdUsuario = u.IdUsuario, Usuario = u.NombreUsuario });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(UsuarioPasswordViewModel vm)
    {
        var u = await _usuarios.ObtenerPorIdAsync(vm.IdUsuario);
        if (u is null) return NotFound();
        vm.Usuario = u.NombreUsuario;

        if (!ModelState.IsValid)
        {
            vm.Password = vm.ConfirmarPassword = null;
            return View(vm);
        }

        await _usuarios.CambiarPasswordAsync(u.IdUsuario, PasswordHasher.Hash(vm.Password!));
        TempData["Mensaje"] = $"Se cambió la contraseña de «{u.NombreUsuario}».";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- Activar / desactivar
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var u = await _usuarios.ObtenerPorIdAsync(id);
        if (u is null) return NotFound();

        if (u.Activo)
        {
            if (u.IdUsuario == User.IdUsuario())
            {
                TempData["Error"] = "No puede desactivar su propio usuario.";
                return RedirectToAction(nameof(Index));
            }
            if (u.Rol == Roles.Administrador && await _usuarios.ContarOtrosAdministradoresActivosAsync(u.IdUsuario) == 0)
            {
                TempData["Error"] = "No se puede desactivar: es el único administrador activo.";
                return RedirectToAction(nameof(Index));
            }
        }

        await _usuarios.CambiarEstadoAsync(u.IdUsuario, !u.Activo);
        TempData["Mensaje"] = u.Activo
            ? $"«{u.NombreUsuario}» se desactivó: ya no puede iniciar sesión. Sus ventas y registros se conservan."
            : $"«{u.NombreUsuario}» se activó de nuevo.";
        return RedirectToAction(nameof(Index));
    }
}

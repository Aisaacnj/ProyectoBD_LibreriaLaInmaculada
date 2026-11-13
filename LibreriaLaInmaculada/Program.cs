// ============================================================
//  Program.cs: arranque y configuración del sistema
//  (aquí se conecta todo: Backend, Frontend y base de datos)
// ============================================================
using System.Globalization;
using LibreriaLaInmaculada.Backend.AccesoDatos;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Los archivos públicos (css, imágenes) viven en Frontend/wwwroot
    WebRootPath = "Frontend/wwwroot"
});

builder.Services.AddControllersWithViews(opciones =>
{
    // Solo se exige lo que tiene [Required] (con su mensaje en español)
    opciones.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    // Mensajes de error en español cuando un dato no se puede leer (por ejemplo, letras en un precio)
    var mensajes = opciones.ModelBindingMessageProvider;
    mensajes.SetValueMustNotBeNullAccessor(_ => "Este dato es obligatorio.");
    mensajes.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"«{valor}» no es un valor válido.");
    mensajes.SetValueIsInvalidAccessor(valor => $"«{valor}» no es un valor válido.");
    mensajes.SetValueMustBeANumberAccessor(campo => "Escriba un número.");
});

// Las vistas viven en Frontend/Vistas (en vez de la carpeta Views por defecto)
builder.Services.Configure<RazorViewEngineOptions>(opciones =>
{
    opciones.ViewLocationFormats.Clear();
    opciones.ViewLocationFormats.Add("/Frontend/Vistas/{1}/{0}" + RazorViewEngine.ViewExtension);
    opciones.ViewLocationFormats.Add("/Frontend/Vistas/Shared/{0}" + RazorViewEngine.ViewExtension);
});

// Inicio de sesión con cookie. El rol del usuario viaja dentro de la cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.LoginPath = "/Cuenta/Login";
        opciones.LogoutPath = "/Cuenta/Salir";
        opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.Cookie.Name = "LibreriaLaInmaculada.Auth";
        opciones.Cookie.HttpOnly = true;
    });

// Toda página exige haber iniciado sesión, salvo las marcadas con [AllowAnonymous].
builder.Services.AddAuthorization(opciones =>
{
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Backend: acceso a datos con ADO.NET
builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<UsuarioRepositorio>();
builder.Services.AddScoped<InicioRepositorio>();
builder.Services.AddScoped<ArticuloRepositorio>();
builder.Services.AddScoped<CategoriaRepositorio>();
builder.Services.AddScoped<ProveedorRepositorio>();
builder.Services.AddScoped<CompraRepositorio>();
builder.Services.AddScoped<VentaRepositorio>();
builder.Services.AddScoped<PerdidaRepositorio>();
builder.Services.AddScoped<ReporteRepositorio>();

var app = builder.Build();

// Formatos de fecha y número de Nicaragua
var cultura = new CultureInfo("es-NI");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = new[] { cultura },
    SupportedUICultures = new[] { cultura }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

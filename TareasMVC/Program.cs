using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json.Serialization;
using TareasMVC;
using TareasMVC.Servicios;

var builder = WebApplication.CreateBuilder(args);

//  A) Servicios comunes
var policia = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();

builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new AuthorizeFilter(policia)))
  .AddViewLocalization()
  .AddDataAnnotationsLocalization()
  .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication()
  .AddMicrosoftAccount("Microsoft", opts =>
  {
      opts.ClientId = builder.Configuration["MicrosoftClientId"];
      opts.ClientSecret = builder.Configuration["MicrosoftSecretId"];
      // solo ruta relativa: PathBase la añade en producción
      opts.CallbackPath = "/signin-microsoft";
  });

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
  .AddEntityFrameworkStores<ApplicationDbContext>()
  .AddDefaultTokenProviders();

// Cookies de Identity
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    IdentityConstants.ApplicationScheme, opts =>
    {
        opts.LoginPath = "/usuarios/login";
        opts.AccessDeniedPath = "/usuarios/login";
        opts.Cookie.SameSite = SameSiteMode.Lax;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// Cookie de correlación externa (OAuth)
builder.Services.PostConfigure<CookieAuthenticationOptions>(
    IdentityConstants.ExternalScheme, opts =>
    {
        opts.Cookie.SameSite = SameSiteMode.None;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddLocalization(o => o.ResourcesPath = "Recursos");
builder.Services.AddTransient<IServicioUsuarios, ServicioUsuarios>();
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddTransient<IAlmacenadorArchivos, AlmacenadorArchivosLocal>();

var app = builder.Build();

// B) Pipeline por ambiente
if (app.Environment.IsDevelopment())
{
    // Nada especial: no montamos PathBase ni ForwardedHeaders
    app.UseDeveloperExceptionPage();
}
else
{
    // Producción: confío en proxy de Nginx y monto el prefijo
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor
                        | ForwardedHeaders.XForwardedProto
    });

    // Aquí se “quita” /tareasmvc y el resto de rutas las procesa tu app
    app.UsePathBase("/tareasmvc");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(o =>
{
    o.DefaultRequestCulture = new RequestCulture("es");
    o.SupportedUICultures = new[] { "es", "en" }
        .Select(c => new CultureInfo(c))
        .ToList();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PlanejaAi.Data;


var builder = WebApplication.CreateBuilder(args);

var chaveCriptografia = builder.Configuration["CriptografiaSettings:ChaveSecreta"];
var ivCriptografia = builder.Configuration["CriptografiaSettings:IV"];
PlanejaAi.Helpers.CriptografiaHelper.Inicializar(chaveCriptografia, ivCriptografia);

var connectionString = builder.Configuration.GetConnectionString("ConexaoPadrao");


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; 
        options.ExpireTimeSpan = TimeSpan.FromHours(8); 
    });


builder.Services.AddControllersWithViews();

var app = builder.Build();

var defaultCulture = new System.Globalization.CultureInfo("pt-BR");
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCulture),
    SupportedCultures = new[] { defaultCulture },
    SupportedUICultures = new[] { defaultCulture }
};

app.UseRequestLocalization(localizationOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    
    app.UseHsts();
}

var forwardedOptions = new ForwardedHeadersOptions

{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};

forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SenYapi.Business.Extensions;
using SenYapi.DataAccess;
using SenYapi.DataAccess.Data;
using SenYapi.Entity;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration (Microsoft SQL Server / MSSQL)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Server=.\\SQLEXPRESS;Database=SenYapiDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<SenYapiDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=senyapi.db";
        options.UseSqlite(sqliteConnection);
    }
});

// 2. Identity Configuration
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;

    // Temporary Lockout Configuration (Default: 3 failed attempts -> 15 min lock)
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
})
.AddEntityFrameworkStores<SenYapiDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
});

// Oturum geçerliliğini ve rol değişikliklerini her istekte anında doğrula (SecurityStamp)
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// 3. Unicode & Turkish Characters Safe HTML Encoding
// Türkçe harflerin (ç, ğ, ı, ö, ş, ü) ve emojilerin HTML entity'sine (&#231;) dönüşmesini önler;
// XSS korumasını (<, >, &, ", ') eksiksiz şekilde koruyarak doğrudan doğru karakterlerle gösterilmesini sağlar.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

// 4. Business & Data Layer Services
builder.Services.AddBusinessServices();

// 5. Controllers & Views
builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Add("/Views/Admin/{0}.cshtml");
});

var app = builder.Build();

// 5. Seed Database on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<SenYapiDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        await DataSeeder.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı başlatılırken veya tohum veriler eklenirken bir hata oluştu.");
    }
}

// 6. HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// Engellenen (IsBlocked) veya hesabı silinmiş kullanıcıların açık oturumlarında işlem yapmasını anında engelle
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userManager = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user == null || user.IsBlocked)
        {
            var signInManager = context.RequestServices.GetRequiredService<SignInManager<AppUser>>();
            await signInManager.SignOutAsync();
            context.Response.Redirect("/Account/Login");
            return;
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

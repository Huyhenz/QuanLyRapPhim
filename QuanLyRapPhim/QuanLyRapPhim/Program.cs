using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Service.Momo;
using QuanLyRapPhim.Service.VNPay;
using System.Globalization;
using QuanLyRapPhim.Service;

var builder = WebApplication.CreateBuilder(args);

// =============================
// HTTP Client services
// =============================
builder.Services.AddHttpClient<GroqService>();

builder.Services.AddScoped<GroqService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new GroqService(httpClient, config);
});

// =============================
// LOCALIZATION
// =============================
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new List<CultureInfo>
    {
        new CultureInfo("vi"),
        new CultureInfo("en")
    };

    options.DefaultRequestCulture = new RequestCulture("vi");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Ưu tiên cookie trước
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// =============================
// MVC / Razor
// =============================
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

// =============================
// Momo & VNPay
// =============================
builder.Services.Configure<MomoOptionModel>(builder.Configuration.GetSection("MomoAPI"));
builder.Services.AddScoped<IMomoService, MomoService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();

// =============================
// DbContext
// =============================
builder.Services.AddDbContext<DBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DB")));

// =============================
// Identity
// =============================
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddRoles<IdentityRole>()
.AddDefaultTokenProviders()
.AddEntityFrameworkStores<DBContext>();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddRazorPages();

// Build app
var app = builder.Build();

// =============================
// Tạo Roles mặc định
// =============================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "Admin", "User", "Manager" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

// =============================
// Pipeline
// =============================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets();

// 👉 Localization phải đặt TRƯỚC UseRouting()
app.UseRequestLocalization();

// Routing
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

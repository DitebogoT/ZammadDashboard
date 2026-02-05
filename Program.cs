using ZammadDashboard.Models;
using ZammadDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

//Zammad configuration settings
builder.Services.Configure<ZammadConfig>(builder.Configuration.GetSection("Zammad"));

//Register Zammad Dashboard Service
builder.Services.AddSingleton<IZammadDashboardService, ZammadDashboardService>();

builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors();
app.UseAuthorization();

app.UseStaticFiles();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting Zammad Dashboard Application at {Time}", DateTime.Now);

app.Run();

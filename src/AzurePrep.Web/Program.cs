using AzurePrep.Application;
using AzurePrep.Infrastructure;
using AzurePrep.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Camadas da aplicação (Clean Architecture).
builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Garante a pasta do SQLite, aplica migrations e semeia o banco no startup.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AzurePrepDbContext>();
    await db.Database.MigrateAsync();
    await AzurePrepDbSeeder.SemearAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

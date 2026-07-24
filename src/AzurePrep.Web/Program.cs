using AzurePrep.Application;
using AzurePrep.Infrastructure;
using AzurePrep.Infrastructure.Persistence;
using AzurePrep.Web.Autenticacao;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Camadas da aplicação (Clean Architecture).
builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutenticacaoSocial(builder.Configuration);

var app = builder.Build();

// Garante a pasta do SQLite, aplica migrations e semeia o banco no startup.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AzurePrepDbContext>();
    await db.Database.MigrateAsync();

    // WAL: leitores deixam de bloquear o escritor. É gravado no próprio arquivo do banco,
    // então basta aplicar uma vez — repetir é barato e idempotente.
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

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

app.UseAuthentication();
app.UseAuthorization();

// MapStaticAssets mapeia wwwroot como ENDPOINTS, e a política de fallback (exige autenticação)
// vale para todo endpoint sem metadata de autorização — inclusive esses. Sem AllowAnonymous o
// CSS/JS responde 302 para a tela de login e a página carrega sem estilo nenhum.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

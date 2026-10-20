using ApiFoundation.Data;
using ApiFoundation.Integracoes;
using ApiFoundation.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ProcessadorArquivosProd>();
builder.Services.AddScoped<IArmazenamentoArquivos, ArmazenamentoArquivos>();
builder.Services.AddScoped<NotificadorLeomob>();
builder.Services.AddScoped<ILeomobApiCliente>(sp => new LeomobApiCliente(
    builder.Configuration.GetValue<double>("Leomob:TaxaFalha"),
    sp.GetRequiredService<ILogger<LeomobApiCliente>>()));
builder.Services.AddScoped<ConsultaPedidos>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

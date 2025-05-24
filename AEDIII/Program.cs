using AEDIII.Compactacao;
using AEDIII.Entidades;
using AEDIII.Interfaces;
using AEDIII.Repositorio;
using AEDIII.Service;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<Arquivo<Pais>>(sp => new Arquivo<Pais>("Pais"));
builder.Services.AddScoped<IPaisService, PaisService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<HuffmanCompressor>();
builder.Services.AddSingleton<LzwCompressor>();
builder.Services.AddSingleton<CompactacaoService>();

var app = builder.Build();

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

using OidcMock.Core.Configuration;
using OidcMock.Host;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJsonStores(HostConfigDirectory.DefaultOptions(builder.Environment));

var app = builder.Build();

app.Services.GetRequiredService<IConfigurationValidator>().Validate();

app.Run();

/// <summary>
/// Punto de entrada visible para WebApplicationFactory en las pruebas de integracion.
/// </summary>
public partial class Program;

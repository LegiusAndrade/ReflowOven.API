using AutoMapper;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog;
using System.Reflection;
using ReflowOvenAPI.ModelsDTO.Mappings;
using ReflowOven.API.Integration.Peripheral;
using ReflowOven.API.Integration.Peripheral.HostedServices;

var MyAllowSpecificOrigins = "_myAllowSpecificOriginsCORS";
var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHostedService<BackgroundWorkerService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(C =>
{
    C.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "v1",
        Title = "ReflowOvenAPI",
        Description = "API Web para uso com Produto Reflow Oven",

        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Lucas Andrade",
            Email = "lucasandrade730@gmail.com",
            Url = new Uri("https://github.com/LegiusAndrade/ReflowOvenAPI")
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "License MIT",
        },
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    C.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(MyAllowSpecificOrigins,
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
});

// Configura o AutoMapper para usar os DTOS
var mappingConfig = new MapperConfiguration(mc =>
{
    mc.AddProfile(new MappingProfile());
});
IMapper mapper = mappingConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

builder.Services.Configure<Settings>(configuration.GetSection("Settings"));

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true; // Assume a versão padrão quando não for informado no request
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1,0);
    options.ReportApiVersions = true; //Define no response do request a compatibilidade da versão
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsfot", LogEventLevel.Warning)
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console()
    .WriteTo.File(new CompactJsonFormatter(), "Log/log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Logger.Information("Logging is working fine");

builder.Host.UseSerilog();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

    app.UseSwagger();
    app.UseSwaggerUI(C =>
    {
        C.SwaggerEndpoint("/swagger/v1/swagger.json",
            "API Reflow Oven");
    });
    app.UseExceptionHandler("/error-development");

}
else
{
    app.UseExceptionHandler("/error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//Adiciona middleware para redirecionar para https
app.UseHttpsRedirection();

//Adiciona o uso do sistema de Cors
app.UseCors(MyAllowSpecificOrigins);

//Adiciona middleware que habilita a autorização
app.UseAuthorization();

app.MapControllers();

app.Run();


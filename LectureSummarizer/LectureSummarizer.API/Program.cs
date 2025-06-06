using Amazon.BedrockRuntime;
using LectureSummarizer.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AWS services
builder.Services.AddAWSService<IAmazonBedrockRuntime>();

// Add custom services
builder.Services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IBedrockService, BedrockService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:5235", "https://localhost:7185",  // Razor Web
                "http://localhost:5252", "https://localhost:7108"   // Blazor SPA
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWebApp");
app.UseAuthorization();
app.MapControllers();

app.Run();

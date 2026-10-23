using System.Text.Json;
using EvalLab.Api.Classification;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ClassifierExceptionHandler>();

builder.Services.AddOptions<ClassifierServiceOptions>()
    .Bind(builder.Configuration.GetSection(ClassifierServiceOptions.SectionName))
    .PostConfigure<IConfiguration>((options, configuration) =>
    {
        var overrideUrl = configuration[ClassifierServiceOptions.BaseUrlEnvironmentVariable];
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            options.BaseUrl = overrideUrl;
        }
    })
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "ClassifierService:BaseUrl must be an absolute URL.")
    .Validate(options => options.TimeoutSeconds > 0, "ClassifierService:TimeoutSeconds must be greater than 0.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IClassifierClient, ClassifierClient>((services, httpClient) =>
{
    var options = services.GetRequiredService<IOptions<ClassifierServiceOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

app.UseExceptionHandler();

app.MapControllers();

app.Run();

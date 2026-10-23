using System.Text.Json;
using EvalLab.Api.Classification;
using EvalLab.Api.Datasets;
using EvalLab.Api.Evaluation;
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

// Relative directories are resolved against the content root, not the process working directory.
builder.Services.AddOptions<DatasetOptions>()
    .Bind(builder.Configuration.GetSection(DatasetOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Directory), "Datasets:Directory is required.")
    .PostConfigure<IHostEnvironment>((options, environment) =>
    {
        if (!string.IsNullOrWhiteSpace(options.Directory))
        {
            options.Directory = Path.GetFullPath(options.Directory, environment.ContentRootPath);
        }
    })
    .ValidateOnStart();

builder.Services.AddOptions<RunStoreOptions>()
    .Bind(builder.Configuration.GetSection(RunStoreOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Directory), "Runs:Directory is required.")
    .PostConfigure<IHostEnvironment>((options, environment) =>
    {
        if (!string.IsNullOrWhiteSpace(options.Directory))
        {
            options.Directory = Path.GetFullPath(options.Directory, environment.ContentRootPath);
        }
    })
    .ValidateOnStart();

builder.Services.AddOptions<EvaluationOptions>()
    .Bind(builder.Configuration.GetSection(EvaluationOptions.SectionName))
    .Validate(options => options.MaxParallelism > 0, "Evaluation:MaxParallelism must be greater than 0.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatasetResolver>();
builder.Services.AddSingleton<IRunStore, JsonFileRunStore>();
builder.Services.AddTransient<EvaluationRunner>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapControllers();

app.Run();

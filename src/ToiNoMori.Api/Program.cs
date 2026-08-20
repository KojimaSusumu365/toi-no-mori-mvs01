using ToiNoMori.Api;

var app = AppHost.Build(new WebApplicationOptions
{
    Args = args,
    ApplicationName = typeof(AppHost).Assembly.FullName
});

await app.RunAsync();

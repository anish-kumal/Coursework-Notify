using Coursework.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Coursework
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            
            // Register services with proper dependency injection
            builder.Services.AddSingleton<IUserService, UserService>();
            builder.Services.AddSingleton<ITagService, TagService>();
            builder.Services.AddSingleton<IMoodService, MoodService>();
            builder.Services.AddSingleton<IJournalService, JournalService>();

            var app = builder.Build();

            return app;
        }
    }
}
